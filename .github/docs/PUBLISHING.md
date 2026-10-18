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
`VRCLearn.HierarchyDecorator.Editor`, `VRCLearn.HierarchyDecorator` namespaces,
asset GUIDs, menus and settings. Do not restore `legacyPackages` replacement of
upstream: both plugins must be able to remain installed and usable independently.
Do not edit or replace the old release or move its tag.

The UnityPackage format installs beneath `Assets/VRCLearn/HierarchyDecorator`,
with the existing `HierarchyDecorator` source folder. Only this maintained
distribution folder is declared in `legacyFolders`, with a stable GUID. Upstream
Assets folders are not deleted. Settings are stored independently at
`UserSettings/VRCLearnHierarchyDecoratorSettings.asset`. The first-run chooser and
**Tools > VRCLearn > Hierarchy Decorator > Import Settings** optionally import old
settings, leaving their files and installation intact. Live upstream settings objects are copied field by field;
recognized old GUID YAML files can be imported without the old assembly loaded.
Unknown binary files require the original plugin to save a text asset first.

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
independent settings, menus, callbacks and non-destructive settings imports in an
isolated project. Never run this fixture-producing probe against a user project.
