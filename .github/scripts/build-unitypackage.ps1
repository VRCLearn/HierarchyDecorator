[CmdletBinding()]
param(
    [string] $OutputDirectory = "dist"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = (& git rev-parse --show-toplevel).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($repositoryRoot)) {
    throw "Unable to locate the Git repository root."
}

$manifestPath = Join-Path $repositoryRoot "package.json"
$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($manifest.displayName) -or
    [string]::IsNullOrWhiteSpace($manifest.version)) {
    throw "package.json displayName and version must not be empty."
}

if ($manifest.displayName -notmatch "^[0-9A-Za-z._-]+$") {
    throw "The package display name '$($manifest.displayName)' cannot be used in an artifact filename."
}

if ($manifest.version -notmatch "^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$") {
    throw "The package version '$($manifest.version)' is not valid SemVer."
}

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

$metaFiles = @($trackedFiles | Where-Object { $_.EndsWith(".meta", [System.StringComparison]::OrdinalIgnoreCase) })
$assetFiles = @($trackedFiles | Where-Object { -not $_.EndsWith(".meta", [System.StringComparison]::OrdinalIgnoreCase) })
$assetFileSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($assetFile in $assetFiles) {
    $assetFileSet.Add($assetFile) | Out-Null
}

$resolvedOutputDirectory = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    [System.IO.Path]::GetFullPath($OutputDirectory)
} else {
    [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputDirectory))
}

[System.IO.Directory]::CreateDirectory($resolvedOutputDirectory) | Out-Null

$artifactName = "$($manifest.displayName).v$($manifest.version).unitypackage"
$unitypackagePath = Join-Path $resolvedOutputDirectory $artifactName
$stagingDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "hierarchy-decorator-unitypackage-$([guid]::NewGuid().ToString('N'))"
$rootFolderMetaPath = Join-Path $repositoryRoot ".github/unitypackage/HierarchyDecorator.meta"
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)

function Add-UnityPackageEntry {
    param(
        [Parameter(Mandatory)]
        [string] $MetaPath,

        [Parameter(Mandatory)]
        [string] $PackagePath,

        [string] $AssetPath
    )

    $guidMatch = Select-String -LiteralPath $MetaPath -Pattern "^guid: ([0-9a-fA-F]{32})$" |
        Select-Object -First 1
    if ($null -eq $guidMatch) {
        throw "The meta file '$MetaPath' does not contain a valid Unity GUID."
    }

    $guid = $guidMatch.Matches[0].Groups[1].Value.ToLowerInvariant()
    $entryDirectory = Join-Path $stagingDirectory $guid
    if (Test-Path -LiteralPath $entryDirectory) {
        throw "Duplicate Unity GUID '$guid' found while adding '$PackagePath'."
    }

    [System.IO.Directory]::CreateDirectory($entryDirectory) | Out-Null
    Copy-Item -LiteralPath $MetaPath -Destination (Join-Path $entryDirectory "asset.meta")
    [System.IO.File]::WriteAllText((Join-Path $entryDirectory "pathname"), $PackagePath, $utf8NoBom)

    if (-not [string]::IsNullOrWhiteSpace($AssetPath)) {
        Copy-Item -LiteralPath $AssetPath -Destination (Join-Path $entryDirectory "asset")
    }
}

try {
    [System.IO.Directory]::CreateDirectory($stagingDirectory) | Out-Null

    Add-UnityPackageEntry `
        -MetaPath $rootFolderMetaPath `
        -PackagePath "Assets/HierarchyDecorator"

    foreach ($relativeMetaPath in $metaFiles) {
        $relativeAssetPath = $relativeMetaPath.Substring(0, $relativeMetaPath.Length - ".meta".Length)
        $normalizedAssetPath = $relativeAssetPath.Replace("/", [System.IO.Path]::DirectorySeparatorChar)
        $sourceAssetPath = Join-Path $repositoryRoot $normalizedAssetPath
        $sourceMetaPath = "$sourceAssetPath.meta"

        if (-not (Test-Path -LiteralPath $sourceAssetPath)) {
            throw "The meta file '$relativeMetaPath' has no corresponding asset."
        }

        $packagePath = "Assets/HierarchyDecorator/$($relativeAssetPath.Replace('\', '/'))"
        $isDirectory = (Get-Item -LiteralPath $sourceAssetPath).PSIsContainer
        if (-not $isDirectory -and -not $assetFileSet.Contains($relativeAssetPath)) {
            throw "The asset '$relativeAssetPath' is not tracked by Git."
        }

        $entryAssetPath = if ($isDirectory) { $null } else { $sourceAssetPath }
        Add-UnityPackageEntry `
            -MetaPath $sourceMetaPath `
            -PackagePath $packagePath `
            -AssetPath $entryAssetPath
    }

    foreach ($relativeAssetPath in $assetFiles) {
        if (-not $metaFiles.Contains("$relativeAssetPath.meta")) {
            throw "The tracked asset '$relativeAssetPath' has no tracked meta file."
        }
    }

    if (Test-Path -LiteralPath $unitypackagePath) {
        Remove-Item -LiteralPath $unitypackagePath -Force
    }

    $entryNames = @(
        Get-ChildItem -LiteralPath $stagingDirectory -Directory |
            Sort-Object Name |
            Select-Object -ExpandProperty Name
    )
    & tar -czf $unitypackagePath -C $stagingDirectory @entryNames
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to create the UnityPackage archive."
    }

    $archiveEntries = @(& tar -tzf $unitypackagePath)
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to read the UnityPackage archive."
    }

    foreach ($entryName in $entryNames) {
        if (-not $archiveEntries.Contains("$entryName/pathname") -or
            -not $archiveEntries.Contains("$entryName/asset.meta")) {
            throw "The UnityPackage archive is missing files for GUID '$entryName'."
        }
    }
} finally {
    if (Test-Path -LiteralPath $stagingDirectory) {
        Remove-Item -LiteralPath $stagingDirectory -Recurse -Force
    }
}

$sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $unitypackagePath).Hash.ToLowerInvariant()

$outputs = [ordered]@{
    artifact_name = $artifactName
    unitypackage_path = $unitypackagePath
    sha256 = $sha256
}

if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_OUTPUT)) {
    foreach ($output in $outputs.GetEnumerator()) {
        Add-Content -LiteralPath $env:GITHUB_OUTPUT -Value "$($output.Key)=$($output.Value)"
    }
}

$outputs | ConvertTo-Json
