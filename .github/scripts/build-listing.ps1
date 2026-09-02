[CmdletBinding()]
param(
    [string] $SourcePath = ".github/source.json",
    [Parameter(Mandatory)]
    [string] $SitePath,
    [string] $OutputDirectory = "dist/vpm-site",
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

function Get-VpmReleaseAssets {
    param(
        [Parameter(Mandatory)]
        [string] $Repository,

        [Parameter(Mandatory)]
        [string] $PackageName,

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
    $assets = [System.Collections.Generic.List[object]]::new()
    while ($true) {
        $uri = "https://api.github.com/repos/$Repository/releases?per_page=100&page=$page"
        $response = Invoke-RestMethod -Uri $uri -Headers $headers

        foreach ($release in $response) {
            if ($release.draft -or $release.prerelease) {
                continue
            }
            if ($release.tag_name -notmatch "^(?<version>(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*))$") {
                continue
            }

            $version = $Matches["version"]
            $expectedAssetName = "$PackageName-$version.zip"
            foreach ($asset in @($release.assets)) {
                if ($asset.name -ceq $expectedAssetName) {
                    $assets.Add([pscustomobject] @{
                        ReleaseTag = $release.tag_name
                        Url = $asset.browser_download_url
                    })
                }
            }
        }

        if ($response.Count -lt 100) {
            break
        }
        $page++
    }

    return $assets
}

function Get-PackageManifestFromZip {
    param(
        [Parameter(Mandatory)]
        [string] $Url,

        [Parameter(Mandatory)]
        [string] $ExpectedPackageName
    )

    $temporaryZip = Join-Path ([System.IO.Path]::GetTempPath()) "hierarchy-decorator-listing-$([guid]::NewGuid().ToString('N')).zip"

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
                throw "$Url has no package.json at the ZIP root."
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

        if ($manifest.name -ne $ExpectedPackageName) {
            throw "$Url contains unexpected package '$($manifest.name)'."
        }
        if ($manifest.version -notmatch "^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$") {
            throw "$Url contains invalid stable version '$($manifest.version)'."
        }
        if ([string]::IsNullOrWhiteSpace($manifest.displayName) -or
            [string]::IsNullOrWhiteSpace($manifest.author.name) -or
            [string]::IsNullOrWhiteSpace($manifest.author.email)) {
            throw "$Url is missing required VPM metadata."
        }
        if ($null -eq $manifest.PSObject.Properties["vpmDependencies"]) {
            $manifest | Add-Member -MemberType NoteProperty -Name "vpmDependencies" -Value ([ordered] @{})
        }

        $manifest | Add-Member -MemberType NoteProperty -Name "url" -Value $Url -Force
        $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $temporaryZip).Hash.ToLowerInvariant()
        $manifest | Add-Member -MemberType NoteProperty -Name "zipSHA256" -Value $hash -Force
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
$resolvedSitePath = Resolve-RepositoryPath -RepositoryRoot $repositoryRoot -Path $SitePath
$resolvedOutputDirectory = Resolve-RepositoryPath -RepositoryRoot $repositoryRoot -Path $OutputDirectory
$source = Get-Content -Raw -LiteralPath $resolvedSourcePath | ConvertFrom-Json

foreach ($propertyName in @(
    "name",
    "id",
    "author",
    "url",
    "repository",
    "packageName"
)) {
    if ($null -eq $source.PSObject.Properties[$propertyName]) {
        throw "source.json is missing the required '$propertyName' property."
    }
}
if ([string]::IsNullOrWhiteSpace($source.author.name)) {
    throw "source.json author.name must not be empty."
}

$assetParameters = @{
    Repository = $source.repository
    PackageName = $source.packageName
    Token = $GitHubToken
}
$assets = Get-VpmReleaseAssets @assetParameters
$packageVersions = [ordered] @{}

foreach ($asset in $assets) {
    Write-Host "Reading VPM release: $($asset.Url)"
    $manifest = Get-PackageManifestFromZip `
        -Url $asset.Url `
        -ExpectedPackageName $source.packageName
    $expectedTag = $manifest.version
    if ($asset.ReleaseTag -ne $expectedTag) {
        throw "Release '$($asset.ReleaseTag)' contains package version '$($manifest.version)'."
    }
    if ($packageVersions.Contains($manifest.version)) {
        throw "Duplicate release found for $($source.packageName) $($manifest.version)."
    }
    $packageVersions[$manifest.version] = $manifest
}

$packages = [ordered] @{}
if ($packageVersions.Count -gt 0) {
    $packages[$source.packageName] = [ordered] @{
        versions = $packageVersions
    }
}

$listing = [ordered] @{
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

if (Test-Path -LiteralPath $resolvedOutputDirectory) {
    Remove-Item -LiteralPath $resolvedOutputDirectory -Recurse -Force
}
[System.IO.Directory]::CreateDirectory($resolvedOutputDirectory) | Out-Null
Copy-Item -Path (Join-Path $resolvedSitePath "*") -Destination $resolvedOutputDirectory -Recurse

$indexPath = Join-Path $resolvedOutputDirectory "index.json"
$listing | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $indexPath -Encoding utf8NoBOM

$outputs = [ordered] @{
    package_count = $packageVersions.Count
    index_path = $indexPath
}
if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_OUTPUT)) {
    foreach ($output in $outputs.GetEnumerator()) {
        Add-Content -LiteralPath $env:GITHUB_OUTPUT -Value "$($output.Key)=$($output.Value)"
    }
}

$outputs | ConvertTo-Json
