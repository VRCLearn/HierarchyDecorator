# VRCLearn VPM distribution

This fork adds multilingual settings, automated upstream synchronization, and
a VPM community repository to HierarchyDecorator.

## Installation

Add this community repository to VRChat Creator Companion:

```text
https://vrclearn.github.io/HierarchyDecorator/index.json
```

Then add **HierarchyDecorator** (`com.wooshii.hierarchydecorator`) to the
desired project.

## Fork policy

- The fork's `master` branch follows upstream `master`, including unreleased
  changes.
- Published packages use an exact upstream GitHub Release tag plus the
  effective VRCLearn fork overlay; they are never built implicitly from the
  current `master` tree.
- Upstream prereleases with plain `vX.Y.Z` tags are published as stable
  VRCLearn distributions after the fork validations pass.
- Package authorship remains attributed to Wooshii and the upstream
  contributors. The upstream MIT license is retained.

The settings interface is available in English, Japanese, Traditional
Chinese, and Simplified Chinese.

## Versioning

The first VRCLearn release is `0.12.1`, based on upstream commit
`162012a36d4486547a6c43b0c1b64612b069fadf`. If upstream later publishes the
same version from a different commit, automation stops for manual review
instead of replacing the existing release.

Release tags use the plain `<version>` value from `package.json` and are immutable. Existing releases and assets
are never overwritten.

## Automated upstream updates

The fork checks upstream `master` every hour. Ordinary changes are merged into
a temporary branch and validated before the merge is promoted to the fork's
`master`. Upstream workflow changes require a maintainer to use GitHub's
**Sync fork → Update branch** action. That user-authorized update can modify
workflow files and its push to `master` starts validation again.

After the main synchronization succeeds, automation enumerates non-draft
upstream Releases, including prereleases. Missing plain semantic versions are
processed in publication order. Each package snapshot starts from the exact
upstream tag and receives only the current effective fork overlay. VPM and
UnityPackage assets are built from the same candidate commit and published with
the staged `package.json` as a stable VRCLearn Release after validation. The
release tag, asset names, and three-file asset set follow the official VRChat
package template.

Merge, overlay, localization, packaging, and listing failures stop the
affected track. When repository Issues are enabled, repeated checks of the
same failure update no additional notifications and the failure issue closes
automatically after recovery.

## Generated package metadata

The VPM archive contains the tracked package files at its root. During
staging, its `package.json` receives the immutable release asset URL, release
changelog URL, license URL, and exact candidate Git revision without changing
the tracked source manifest. The generated ZIP, UnityPackage, and staged
`package.json` are attached to the GitHub Release.
