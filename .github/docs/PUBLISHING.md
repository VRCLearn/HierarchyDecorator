# VPM publishing

Hierarchy Decorator follows the VRCLearn distribution pattern: versioned GitHub
Release assets, an official VRChat template on GitHub Pages, and a VPM index built
from stable numeric releases. No additional hosting service or cross-repository
token is required.

## Installation and migration

- Website: https://vrclearn.github.io/HierarchyDecorator/
- Index: https://vrclearn.github.io/HierarchyDecorator/index.json
- Repository ID: `io.github.vrclearn.hierarchydecorator` (preserved from the old listing).
- Maintained package: `com.vrclearn.hierarchydecorator`.

The listing retains the former `com.wooshii.hierarchydecorator` 0.12.1 package and
its original release assets. The maintained package has an independent assembly
`VRCLearn.HierarchyDecorator.Editor`, `HierarchyDecorator.Editor` namespaces,
asset GUIDs, menus and settings. Do not restore `legacyPackages` replacement of
upstream: both plugins must be able to remain installed and usable independently.
Do not edit or replace the old release or move its tag.

The UnityPackage format installs beneath `Assets/com.vrclearn.hierarchydecorator`,
using its existing package ID as the directory name and retaining the
`HierarchyDecorator` source folder. Only `Assets/com.vrclearn.hierarchydecorator`
is declared in `legacyFolders`, with its distribution folder GUID, so installing
the VPM format can remove that duplicate distribution. The
wooshii package and Assets folders must not appear in `legacyPackages` or
`legacyFolders`: VPM applies those rules during installation, before a user can
choose whether to migrate.

Settings are stored at `UserSettings/HierarchyDecoratorSettings.asset`, using the
object name `HierarchyDecorator Settings`. The preset window title is
`HierarchyDecorator`; **Tools > HierarchyDecorator > Choose Preset**
reopens it. Choosing a preset or closing the window leaves wooshii installed.
The assembly, namespace, package ID and internal preference keys remain
independent. Preferences use the `HierarchyDecorator` path. Publisher identity
remains in package and assembly identifiers; it is not added as a product prefix
or menu group.

Migration resolves exactly one source: the current Settings GUID recorded in
wooshii's `EditorPrefs` key `Application.productName + "_HD_GUID"`. Do not scan
`Assets` for settings, fall back to default settings paths, present a candidate
list, select another copy, or merge configurations. If wooshii is installed but
its recorded current reference is invalid, prompt the user to restore that
reference through the old plugin. The migration entry point re-reads the current
reference when invoked rather than trusting a cached UI path. Only the explicit
migration action copies this source over the new local settings. No compatibility
lookup for former VRCLearn local settings paths is added.

Migration copies the current source settings and saves before any cleanup. Non-VPM
Assets cleanup locates the known `Assets/HierarchyDecorator` root and the nested
`Assets/HierarchyDecorator/HierarchyDecorator` layout, plus renamed roots
identified by known folder or distributed file GUIDs. It uses the distributed
relative file paths within identified roots as well as known file GUIDs, so
regenerated file GUIDs do not leave the original scripts behind. Cleanup removes
recognized distribution files, the current source settings asset and empty plugin
directories. It retains extra user files and other settings assets. UPM dependencies
use `Client.Remove` and retain external source directories. Embedded packages are
removed only from the confirmed project-local package directory. Import/save
failures prevent cleanup; later cleanup failures are reported as incomplete
migration.

For a VPM-managed wooshii installation, migration imports settings only. Keep the
old package installation, current source settings asset, `Packages/manifest.json`
and `Packages/vpm-manifest.json` unchanged. The user can remove wooshii through
VCC/ALCOMD after importing. The tool must not invoke Resolver APIs, edit package
records, or preflight package dependencies for VPM removal.

## Validate and build

Python 3.10 or later is required. From the repository root:

```powershell
python -m unittest discover -s .github/scripts -v
python .github/scripts/build_package.py --output Output~/Release --repository VRCLearn/HierarchyDecorator --revision <full-source-commit-sha>
```

The builder stages a manifest and creates a VPM ZIP, UnityPackage and
`SHA256SUMS.txt`. Both formats preserve VRCLearn asset GUIDs and the upstream MIT
license. The manifest records the source repository, full source revision and exact
versioned download URL. `zipSHA256` belongs in the index, not inside `package.json`.
Development files and Unity-ignored names are excluded, including their metadata;
explicit ZIP directory entries preserve empty asset folders. Building does not
modify source files.

The package workflow tests and builds on Windows, macOS and Linux with Python 3.10
and 3.12. The existing C# and Unity workflows remain responsible for compilation;
distribution tests do not check interactive editor behavior.

## Publish a version

1. Update `package.json` and its download URL. Add the exact version's changes to
   `CHANGELOG.md`, using `## [VERSION] - YYYY-MM-DD` and the Asia/Shanghai date.
   Keep an `Unreleased` section above released versions. The tagged changelog is
   the source of GitHub Release notes; preparation fails if its section is absent.
2. Run the distribution tests and relevant Unity compilation checks. Inspect the
   staged archives, license, source revision and independent settings and installation identities.
3. Check remote history, tags and releases for duplicates, commit to a feature
   branch, and open a pull request against `main`. After review, merge and verify
   validation on `main`, then push a three-part numeric tag matching the manifest
   exactly, such as `1.0.0`, without a `v` prefix.
4. `release.yml` validates the archives, creates the Release and explicitly
   dispatches `vpm-listing.yml` on `main`. Existing releases keep their assets on
   retry. Never overwrite a published release or move its tag as a routine retry.
5. Verify the public download checksums, manifest and source revision, then verify
   the live index includes the new version and preserves historical 0.12.1.

## Listing and Pages

`.github/vpm/source.json` selects both the maintained and historical package IDs.
The listing workflow checks out pinned official `template-package` and
`package-list-action` revisions, applies VRCLearn's banner, **Add to VCC/ALCOMD**
button sizing and description escaping, and validates the rendered JavaScript.
The final index is generated from the actual public ZIP manifests and SHA-256
values. API credentials are never sent to public asset download hosts.

Published releases and release edits request a refresh. A 15-minute metadata-only
check rebuilds the site when releases change and skips unchanged indexes. Before
the first maintained release it leaves the historical listing intact. A keepalive
commit after 45 days of repository inactivity keeps scheduled updates active.

GitHub Pages must use GitHub Actions as its build source. Deployment needs
`pages: write` and `id-token: write`; release publication needs `contents: write`
and workflow dispatch needs `actions: write`.

## Coexistence verification

The distribution tests keep a committed snapshot of upstream 0.12.1 identities
and reject shared GUIDs, assembly names, namespaces or unresolved internal assembly
references. To prepare a real Unity project containing both packages:

```powershell
python .github/unity-ci/prepare_project.py --unity-version 2022.3.22f1 --project-path .unity-ci~/coexistence-project --upstream-package <local-upstream-package-path>
```

`HierarchyDecoratorCI.CompileCheck.Run` also verifies the two assemblies and
Settings types when the old package is present. `CoexistenceProbe.cs` exercises
independent settings, menus, callbacks and settings copies in an isolated
project. The migration probes cover explicit removal for non-VPM installations,
retention of unrelated files, and settings-only import with unchanged VPM
installation and manifest files. Never run these fixture-producing
probes against a user project.

To run the migration probe, prepare a fresh disposable project and launch its Editor:

```powershell
python .github/unity-ci/prepare_migration_project.py --unity-version 2022.3.22f1 --project-path .unity-ci~/migration-assets --upstream-package <local-upstream-package-path> --mode assets
& "<path-to-Unity.exe>" -batchmode -nographics -projectPath "$PWD/.unity-ci~/migration-assets" -executeMethod HierarchyDecoratorCI.MigrationProbe.Run -logFile "$PWD/.unity-ci~/migration-assets/editor.log"
```

Use `--mode local`, `--mode embedded`, or `--mode vpm` with a fresh project path
to exercise the other installation routes. VPM mode verifies settings import
while retaining the old installation, current source settings and both manifest files;
it does not require a Resolver package. The preparation script uses local packages
and does not download them. Do not add `-quit`: the probe waits for asynchronous
package operations where needed and exits when finished. Read
`Logs/migration-result.json` inside the disposable project for the result.

## Windows settings-save regression

Prepare a disposable project, copy the save probe, and add its explicit marker:

```powershell
python .github/unity-ci/prepare_project.py --unity-version 2022.3.22f1 --project-path .unity-ci~/settings-save
Copy-Item .github/unity-ci/SettingsSaveProbe.cs .unity-ci~/settings-save/Assets/Editor/
New-Item .unity-ci~/settings-save/ProjectSettings/SettingsSaveProbe.enabled -ItemType File
& "<path-to-Unity.exe>" -batchmode -nographics -projectPath "$PWD/.unity-ci~/settings-save" -executeMethod HierarchyDecoratorCI.SettingsSaveProbe.Run -logFile "$PWD/.unity-ci~/settings-save/editor.log"
```

Run this probe on Windows without `-quit`. It checks 600 saves of the real settings
singleton, including immediate consecutive writes and writes across editor updates,
and reads back fields and font/script references. Directory, read-only and locked-file
failures must preserve existing contents and dirty state; each must recover after
the fault is removed. Read `Logs/settings-save-result.json` for the result.
