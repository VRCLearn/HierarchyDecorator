[CmdletBinding()]
param(
    [string] $SourcePath = ".github/source.json",
    [string] $OutputDirectory = ".github/Website",
    [string] $GitHubToken = $env:GITHUB_TOKEN
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

Add-Type -AssemblyName System.IO.Compression.FileSystem

function Resolve-RepositoryPath {
    param(
        [Parameter(Mandatory)]
        [string] $RepositoryRoot,

        [Parameter(Mandatory)]
        [string] $Path
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $RepositoryRoot $Path))
}

function Get-GitHubReleaseZipUrls {
    param(
        [Parameter(Mandatory)]
        [string] $Repository,

        [string] $Token
    )

    if ($Repository -notmatch "^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$") {
        throw "Invalid GitHub repository '$Repository'. Expected owner/name."
    }

    $headers = @{
        Accept = "application/vnd.github+json"
        "User-Agent" = "VRCLearn-HierarchyDecorator-VPM"
        "X-GitHub-Api-Version" = "2022-11-28"
    }

    if (-not [string]::IsNullOrWhiteSpace($Token)) {
        $headers.Authorization = "Bearer $Token"
    }

    $page = 1
    $releaseUrls = [System.Collections.Generic.List[string]]::new()

    while ($true) {
        $uri = "https://api.github.com/repos/$Repository/releases?per_page=100&page=$page"
        $response = Invoke-RestMethod -Uri $uri -Headers $headers
        $releases = [System.Collections.Generic.List[object]]::new()

        foreach ($release in $response) {
            $releases.Add($release)
        }

        foreach ($release in $releases) {
            if ($release.draft) {
                continue
            }

            foreach ($asset in @($release.assets)) {
                if ($asset.name.EndsWith(".zip", [System.StringComparison]::OrdinalIgnoreCase)) {
                    $releaseUrls.Add($asset.browser_download_url)
                }
            }
        }

        if ($releases.Count -lt 100) {
            break
        }

        $page++
    }

    return $releaseUrls
}

function Get-PackageManifestFromZip {
    param(
        [Parameter(Mandatory)]
        [string] $Url
    )

    $temporaryZip = Join-Path ([System.IO.Path]::GetTempPath()) "vpm-listing-$([guid]::NewGuid().ToString('N')).zip"

    try {
        Invoke-WebRequest `
            -Uri $Url `
            -OutFile $temporaryZip `
            -Headers @{ "User-Agent" = "VRCLearn-HierarchyDecorator-VPM" }

        $archive = [System.IO.Compression.ZipFile]::OpenRead($temporaryZip)
        try {
            $manifestEntry = $archive.Entries |
                Where-Object { $_.FullName -eq "package.json" } |
                Select-Object -First 1

            if ($null -eq $manifestEntry) {
                Write-Warning "Skipping '$Url' because package.json is not at the ZIP root."
                return $null
            }

            $reader = [System.IO.StreamReader]::new($manifestEntry.Open())
            try {
                $manifest = $reader.ReadToEnd() | ConvertFrom-Json
            } finally {
                $reader.Dispose()
            }
        } finally {
            $archive.Dispose()
        }

        if ([string]::IsNullOrWhiteSpace($manifest.name) -or
            [string]::IsNullOrWhiteSpace($manifest.version)) {
            Write-Warning "Skipping '$Url' because its manifest has no package name or version."
            return $null
        }

        if ($manifest.version -notmatch "^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$") {
            Write-Warning "Skipping '$Url' because '$($manifest.version)' is not valid SemVer."
            return $null
        }

        if ($null -eq $manifest.PSObject.Properties["vpmDependencies"]) {
            $manifest | Add-Member -MemberType NoteProperty -Name "vpmDependencies" -Value ([ordered]@{})
        }

        $sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $temporaryZip).Hash.ToLowerInvariant()
        $manifest | Add-Member -MemberType NoteProperty -Name "url" -Value $Url -Force
        $manifest | Add-Member -MemberType NoteProperty -Name "zipSHA256" -Value $sha256 -Force

        return $manifest
    } finally {
        if (Test-Path -LiteralPath $temporaryZip) {
            Remove-Item -LiteralPath $temporaryZip -Force
        }
    }
}

$repositoryRoot = (& git rev-parse --show-toplevel).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($repositoryRoot)) {
    throw "Unable to locate the Git repository root."
}

$resolvedSourcePath = Resolve-RepositoryPath -RepositoryRoot $repositoryRoot -Path $SourcePath
$resolvedOutputDirectory = Resolve-RepositoryPath -RepositoryRoot $repositoryRoot -Path $OutputDirectory
$source = Get-Content -Raw -LiteralPath $resolvedSourcePath | ConvertFrom-Json

foreach ($propertyName in @("name", "id", "author", "url")) {
    if ($null -eq $source.PSObject.Properties[$propertyName]) {
        throw "source.json is missing the required '$propertyName' property."
    }
}

if ([string]::IsNullOrWhiteSpace($source.author.name)) {
    throw "source.json author.name must not be empty."
}

$releaseUrls = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::OrdinalIgnoreCase
)

if ($null -ne $source.PSObject.Properties["githubRepos"]) {
    foreach ($repository in @($source.githubRepos)) {
        foreach ($url in Get-GitHubReleaseZipUrls -Repository $repository -Token $GitHubToken) {
            $releaseUrls.Add($url) | Out-Null
        }
    }
}

if ($null -ne $source.PSObject.Properties["packages"]) {
    foreach ($packageSource in @($source.packages)) {
        foreach ($url in @($packageSource.releases)) {
            $releaseUrls.Add($url) | Out-Null
        }
    }
}

$packageVersions = [ordered]@{}

foreach ($releaseUrl in $releaseUrls) {
    Write-Host "Reading VPM release: $releaseUrl"
    $manifest = Get-PackageManifestFromZip -Url $releaseUrl

    if ($null -eq $manifest) {
        continue
    }

    if (-not $packageVersions.Contains($manifest.name)) {
        $packageVersions[$manifest.name] = [ordered]@{}
    }

    $versions = $packageVersions[$manifest.name]
    if ($versions.Contains($manifest.version)) {
        throw "Duplicate release found for $($manifest.name) $($manifest.version)."
    }

    $versions[$manifest.version] = $manifest
}

$packages = [ordered]@{}
foreach ($packageName in $packageVersions.Keys) {
    $packages[$packageName] = [ordered]@{
        versions = $packageVersions[$packageName]
    }
}

$listing = [ordered]@{
    name = $source.name
    id = $source.id
    url = $source.url
    author = $source.author.name
    packages = $packages
}

foreach ($optionalProperty in @("description", "infoLink")) {
    if ($null -ne $source.PSObject.Properties[$optionalProperty]) {
        $listing[$optionalProperty] = $source.$optionalProperty
    }
}

if ($null -ne $source.author.PSObject.Properties["url"]) {
    $listing.authorUrl = $source.author.url
}

[System.IO.Directory]::CreateDirectory($resolvedOutputDirectory) | Out-Null
$indexPath = Join-Path $resolvedOutputDirectory "index.json"
$listing | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $indexPath -Encoding utf8NoBOM

$packageCount = $packageVersions.Count
if ($packageCount -eq 0) {
    Write-Warning "No VPM release ZIP was found. The Pages deployment will be skipped."
} else {
    Write-Host "Wrote $packageCount package(s) to $indexPath."
}

if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_OUTPUT)) {
    Add-Content -LiteralPath $env:GITHUB_OUTPUT -Value "package_count=$packageCount"
    Add-Content -LiteralPath $env:GITHUB_OUTPUT -Value "index_path=$indexPath"
}
