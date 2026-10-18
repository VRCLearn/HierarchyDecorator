#!/usr/bin/env bash
# Like unity-mcp's tools/compile-check.sh, invoke Roslyn without launching Unity.
set -euo pipefail
shopt -s nullglob

fail() { echo "::error::$*" >&2; exit 1; }

# Native Windows dotnet needs Windows paths when running this script in Git Bash.
absolute_path() { (cd "$1" && (pwd -W 2>/dev/null || pwd)); }

repo=$(absolute_path "$(dirname "${BASH_SOURCE[0]}")/../..")
unity_data=$(absolute_path "${UNITY_DATA:-/opt/unity/Editor/Data}")
output="$repo/.unity-ci~/csharp"
sources_dir="$repo/VRCLearnHierarchyDecorator/Scripts/Editor"
assembly=VRCLearn.HierarchyDecorator.Editor
compiler="$unity_data/DotNetSdkRoslyn/csc.dll"
dotnet="$unity_data/NetCoreRuntime/dotnet"
if [[ -f "$dotnet.exe" ]]; then dotnet="$dotnet.exe"; fi
[[ -f "$compiler" && -f "$dotnet" ]] || fail "Unity's bundled Roslyn compiler or dotnet runtime is missing."
[[ ${UNITY_VERSION:-} =~ ^([0-9]+)\.([0-9]+)\.([0-9]+)[abfp][0-9]+$ ]] || fail 'Set UNITY_VERSION to a complete Editor version.'
major=${BASH_REMATCH[1]}
minor=${BASH_REMATCH[2]}
patch=${BASH_REMATCH[3]}

mapfile -d '' sources < <(find "$sources_dir" -type f -name '*.cs' -print0 | sort -z)
[[ ${#sources[@]} -gt 0 ]] || fail 'No package C# sources found.'
mapfile -d '' asmdefs < <(find "$repo/VRCLearnHierarchyDecorator" -name '*.asmdef' -print0)
[[ ${#asmdefs[@]} -eq 1 && ${asmdefs[0]} == "$sources_dir/$assembly.asmdef" ]] || fail 'Update the compile check for the changed assembly layout.'

# These match the framework and engine reference directories used by Unity's
# Editor compilation. Do not reference every DLL under Editor/Data: it also
# contains compiler/build tools with conflicting types.
references=()
while IFS= read -r pattern; do
    pattern=${pattern%$'\r'}
    [[ -n "$pattern" ]] || continue
    # Expand only the filename glob; keep spaces in the installation path intact.
    directory="$unity_data/${pattern%/*}"
    filename=${pattern##*/}
    matches=("$directory"/$filename)
    [[ ${#matches[@]} -gt 0 ]] || fail "Missing reference pattern: $pattern"
    for reference in "${matches[@]}"; do
        [[ -f "$reference" ]] || fail "Missing reference: $reference"
        references+=("$reference")
    done
done < "$repo/.github/unity-ci/csharp-references.txt"

defines=(UNITY_EDITOR UNITY_EDITOR_64 UNITY_64 UNITY_STANDALONE PLATFORM_STANDALONE
    PLATFORM_ARCH_64 ENABLE_MONO NET_4_6 NET_UNITY_4_8 DEBUG TRACE UNITY_ASSERTIONS
    UNITY_EDITOR_ONLY_COMPILATION CSHARP_7_OR_LATER CSHARP_7_3_OR_NEWER
    "UNITY_$major" "UNITY_${major}_$minor" "UNITY_${major}_${minor}_$patch")
# Unity's *_OR_NEWER symbols refer to released version lines, not every possible
# year/minor combination. Keep the list aligned with the workflow's matrix.
for release in 5.3 5.4 5.5 5.6 2017.1 2017.2 2017.3 2017.4 2018.1 2018.2 2018.3 2018.4 \
    2019.1 2019.2 2019.3 2019.4 2020.1 2020.2 2020.3 2021.1 2021.2 2021.3 \
    2022.1 2022.2 2022.3 2023.1 2023.2 2023.3 6000.0 6000.1 6000.2 6000.3; do
    release_major=${release%.*}
    release_minor=${release#*.}
    if (( release_major < major || (release_major == major && release_minor <= minor) )); then
        defines+=("UNITY_${release_major}_${release_minor}_OR_NEWER")
    fi
done

mkdir -p "$output"
for platform in WIN OSX LINUX; do
    platform_output="$output/$platform"
    mkdir -p "$platform_output"
    dll="$platform_output/$assembly.dll"
    response="$platform_output/compiler.rsp"
    # Remove previous output so a failed run cannot reuse a stale assembly.
    rm -f "$dll"
    {
        printf '%s\n' -nologo -target:library -langversion:9.0 -nostdlib+ \
            -deterministic+ -optimize+ -utf8output -preferreduilang:en-US -nowarn:1701,1702
        printf -- '-out:"%s"\n' "$dll"
        printf -- '-define:%s\n' "${defines[@]}" "UNITY_EDITOR_$platform" \
            "UNITY_STANDALONE_$platform" "PLATFORM_STANDALONE_$platform"
        printf -- '-r:"%s"\n' "${references[@]}"
        printf '"%s"\n' "${sources[@]}"
    } > "$response"
    echo "Compiling ${#sources[@]} sources against Unity $UNITY_VERSION [$platform]"
    "$dotnet" "$compiler" -noconfig "@$response" 2>&1 | tee "$platform_output/compiler.log"
    [[ -s "$dll" ]] || fail "Compiler produced no assembly for $platform."
    echo "C# compilation passed: Unity $UNITY_VERSION [$platform]"
done
