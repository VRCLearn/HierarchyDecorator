[CmdletBinding()]
param(
    [string] $OutputDirectory = "dist/vpm",
    [string] $Repository = $(
        if ([string]::IsNullOrWhiteSpace($env:GITHUB_REPOSITORY)) {
            "VRCLearn/HierarchyDecorator"
        } else {
            $env:GITHUB_REPOSITORY
        }
    ),
    [string] $Revision = "",
    [switch] $RequireStableVersion
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Add-OrReplaceProperty {
    param(
        [Parameter(Mandatory)]
        [psobject] $Object,

        [Parameter(Mandatory)]
        [string] $Name,

        [AllowNull()]
        [object] $Value
    )

    if ($null -eq $Object.PSObject.Properties[$Name]) {
        $Object | Add-Member -MemberType NoteProperty -Name $Name -Value $Value
    } else {
        $Object.$Name = $Value
    }
}

$repositoryRoot = (& git rev-parse --show-toplevel).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($repositoryRoot)) {
    throw "Unable to locate the Git repository root."
}

if ([string]::IsNullOrWhiteSpace($Revision)) {
    $Revision = (& git -C $repositoryRoot rev-parse HEAD).Trim()
}
if ($Repository -notmatch "^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$") {
    throw "Invalid GitHub repository '$Repository'. Expected owner/name."
}
if ($Revision -notmatch "^[0-9a-fA-F]{40}$") {
    throw "Invalid Git revision '$Revision'."
}

$manifestPath = Join-Path $repositoryRoot "package.json"
$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json

$requiredProperties = @(
    "name",
    "displayName",
    "version",
    "author",
    "license",
    "vpmDependencies"
)

foreach ($propertyName in $requiredProperties) {
    if ($null -eq $manifest.PSObject.Properties[$propertyName]) {
        throw "package.json is missing the required '$propertyName' property."
    }
}

if ([string]::IsNullOrWhiteSpace($manifest.author.name) -or
    [string]::IsNullOrWhiteSpace($manifest.author.email)) {
    throw "package.json author.name and author.email must not be empty."
}

if ($manifest.name -notmatch "^[a-z0-9]+(?:[._-][a-z0-9]+)+$") {
    throw "The package name '$($manifest.name)' is not a valid lowercase package ID."
}

if ($manifest.version -notmatch "^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$") {
    throw "The package version '$($manifest.version)' is not valid SemVer."
}
if ($RequireStableVersion -and
    $manifest.version -notmatch "^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$") {
    throw "Stable releases require an X.Y.Z package version; found '$($manifest.version)'."
}

$releaseTag = $manifest.version
$artifactName = "$($manifest.name)-$($manifest.version).zip"
$repositoryUrl = "https://github.com/$Repository"
$artifactUrl = "$repositoryUrl/releases/download/$releaseTag/$artifactName"
$stagedManifest = $manifest | ConvertTo-Json -Depth 100 | ConvertFrom-Json
Add-OrReplaceProperty -Object $stagedManifest -Name "url" -Value $artifactUrl
Add-OrReplaceProperty -Object $stagedManifest -Name "licensesUrl" -Value "$repositoryUrl/blob/$releaseTag/LICENSE.md"
Add-OrReplaceProperty -Object $stagedManifest -Name "changelogUrl" -Value "$repositoryUrl/releases/tag/$releaseTag"
Add-OrReplaceProperty -Object $stagedManifest -Name "repository" -Value ([pscustomobject] @{
    type = "git"
    url = "$repositoryUrl.git"
    revision = $Revision.ToLowerInvariant()
})

$packageEntries = @(
    "package.json",
    "package.json.meta",
    "LICENSE.md",
    "LICENSE.md.meta",
    "README.md",
    "README.md.meta",
    "CHANGELOG.md",
    "CHANGELOG.md.meta",
    "HierarchyDecorator.meta",
    "HierarchyDecorator"
)

$trackedFiles = @(
    & git -C $repositoryRoot ls-files -- $packageEntries |
        Where-Object {
            -not [string]::IsNullOrWhiteSpace($_) -and
            [System.IO.Path]::GetFileName($_) -ne ".gitignore"
        }
)

if ($LASTEXITCODE -ne 0 -or $trackedFiles.Count -eq 0) {
    throw "Unable to enumerate tracked package files."
}

$resolvedOutputDirectory = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    [System.IO.Path]::GetFullPath($OutputDirectory)
} else {
    [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputDirectory))
}

[System.IO.Directory]::CreateDirectory($resolvedOutputDirectory) | Out-Null

$zipPath = Join-Path $resolvedOutputDirectory $artifactName
$publishedManifestPath = Join-Path $resolvedOutputDirectory "package.json"
$stagingDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "hierarchy-decorator-vpm-$([guid]::NewGuid().ToString('N'))"

try {
    [System.IO.Directory]::CreateDirectory($stagingDirectory) | Out-Null

    foreach ($relativePath in $trackedFiles) {
        $normalizedRelativePath = $relativePath.Replace("/", [System.IO.Path]::DirectorySeparatorChar)
        $sourcePath = Join-Path $repositoryRoot $normalizedRelativePath
        $destinationPath = Join-Path $stagingDirectory $normalizedRelativePath
        $destinationDirectory = Split-Path -Parent $destinationPath

        [System.IO.Directory]::CreateDirectory($destinationDirectory) | Out-Null
        Copy-Item -LiteralPath $sourcePath -Destination $destinationPath
    }

    $stagedManifest |
        ConvertTo-Json -Depth 100 |
        Set-Content -LiteralPath (Join-Path $stagingDirectory "package.json") -Encoding utf8NoBOM

    Copy-Item `
        -LiteralPath (Join-Path $stagingDirectory "package.json") `
        -Destination $publishedManifestPath `
        -Force

    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory(
        $stagingDirectory,
        $zipPath,
        [System.IO.Compression.CompressionLevel]::Optimal,
        $false
    )
} finally {
    if (Test-Path -LiteralPath $stagingDirectory) {
        Remove-Item -LiteralPath $stagingDirectory -Recurse -Force
    }
}

$archive = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    $manifestEntry = $archive.Entries |
        Where-Object { $_.FullName -eq "package.json" } |
        Select-Object -First 1

    if ($null -eq $manifestEntry) {
        throw "The VPM archive does not contain package.json at its root."
    }

    $reader = [System.IO.StreamReader]::new($manifestEntry.Open())
    try {
        $archivedManifest = $reader.ReadToEnd() | ConvertFrom-Json
    } finally {
        $reader.Dispose()
    }

    if ($archivedManifest.name -ne $manifest.name -or
        $archivedManifest.version -ne $manifest.version -or
        $archivedManifest.url -ne $artifactUrl -or
        $archivedManifest.repository.revision -ne $Revision.ToLowerInvariant()) {
        throw "The archived manifest does not match the source package manifest."
    }
} finally {
    $archive.Dispose()
}

$sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $zipPath).Hash.ToLowerInvariant()
$outputs = [ordered]@{
    package_name = $manifest.name
    display_name = $manifest.displayName
    version = $manifest.version
    tag = $releaseTag
    artifact_name = $artifactName
    manifest_path = $publishedManifestPath
    zip_path = $zipPath
    sha256 = $sha256
}

if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_OUTPUT)) {
    foreach ($output in $outputs.GetEnumerator()) {
        Add-Content -LiteralPath $env:GITHUB_OUTPUT -Value "$($output.Key)=$($output.Value)"
    }
}

$outputs | ConvertTo-Json
