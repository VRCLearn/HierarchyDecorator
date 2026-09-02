[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = (& git rev-parse --show-toplevel).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($repositoryRoot)) {
    throw "Unable to locate the Git repository root."
}

$editorRoot = Join-Path $repositoryRoot "HierarchyDecorator/Scripts/Editor"
$localizationPath = Join-Path $editorRoot "SettingsLocalization.cs"
if (-not (Test-Path -LiteralPath $localizationPath -PathType Leaf)) {
    throw "Localization source not found: $localizationPath"
}

$source = Get-Content -Raw -LiteralPath $localizationPath
$csharpStringPattern = '"(?<value>(?:\\.|[^"\\])*)"'
$entryPattern = '(?m)^\s*\{\s*"(?<key>(?:\\.|[^"\\])*)"\s*,\s*new\[\]\s*\{(?<values>.*)\}\s*\},\s*$'
$entryMatches = [regex]::Matches($source, $entryPattern)
if ($entryMatches.Count -lt 100) {
    throw "Parsed only $($entryMatches.Count) localization entries; expected at least 100."
}

$keys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
foreach ($entryMatch in $entryMatches) {
    $key = [regex]::Unescape($entryMatch.Groups["key"].Value)
    if (-not $keys.Add($key)) {
        throw "Duplicate localization key: $key"
    }

    $translations = @(
        [regex]::Matches($entryMatch.Groups["values"].Value, $csharpStringPattern) |
            ForEach-Object { [regex]::Unescape($_.Groups["value"].Value) }
    )
    if ($translations.Count -ne 4) {
        throw "Localization key '$key' must contain exactly four language values."
    }
    if ($translations.Where({ [string]::IsNullOrWhiteSpace($_) }).Count -gt 0) {
        throw "Localization key '$key' contains an empty language value."
    }

    $expectedPlaceholders = @(
        [regex]::Matches($translations[0], '\{[0-9]+(?:[^{}]*)\}') |
            ForEach-Object Value |
            Sort-Object
    )
    foreach ($translation in $translations[1..3]) {
        $actualPlaceholders = @(
            [regex]::Matches($translation, '\{[0-9]+(?:[^{}]*)\}') |
                ForEach-Object Value |
                Sort-Object
        )
        if (($expectedPlaceholders -join "`n") -ne ($actualPlaceholders -join "`n")) {
            throw "Format placeholders do not match for localization key: $key"
        }
    }
}

$missingKeys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
$editorFiles = Get-ChildItem -LiteralPath $editorRoot -Filter "*.cs" -File -Recurse
foreach ($editorFile in $editorFiles) {
    $editorSource = Get-Content -Raw -LiteralPath $editorFile.FullName
    $textCalls = [regex]::Matches($editorSource, 'Text\(\s*"(?<key>(?:\\.|[^"\\])*)"\s*\)')
    foreach ($textCall in $textCalls) {
        $key = [regex]::Unescape($textCall.Groups["key"].Value)
        if (-not $keys.Contains($key)) {
            [void] $missingKeys.Add($key)
        }
    }
}

$mappingMatches = [regex]::Matches(
    $source,
    '\{\s*"(?:\\.|[^"\\])*"\s*,\s*"(?<key>(?:Property|Tooltip)\.(?:\\.|[^"\\])*)"\s*\}'
)
foreach ($mappingMatch in $mappingMatches) {
    $key = [regex]::Unescape($mappingMatch.Groups["key"].Value)
    if (-not $keys.Contains($key)) {
        [void] $missingKeys.Add($key)
    }
}

if ($missingKeys.Count -gt 0) {
    $formattedKeys = ($missingKeys | Sort-Object | ForEach-Object { "- $_" }) -join "`n"
    throw "Code references missing localization keys:`n$formattedKeys"
}

Write-Host "Validated $($keys.Count) four-language entries across $($editorFiles.Count) editor files."
