# Contributing

Bug fixes, features, documentation improvements, and translations are welcome.

## Issues

Check existing issues and pull requests before opening a new one. For bug reports,
include your Unity version, steps to reproduce, expected and actual behavior, and
relevant logs or screenshots. For feature requests, describe the use case and the
behavior you would like to see.

## Pull requests

Start from the latest `main` and open your pull request against
[VRCLearn/HierarchyDecorator](https://github.com/VRCLearn/HierarchyDecorator).

- Keep changes focused and follow the existing code style, using four spaces for
  indentation.
- Preserve existing Unity asset GUIDs and include `.meta` files for new assets.
- Review your changes and validate the affected behavior in Unity as appropriate.
- Explain what changed and why, and link related issues when applicable. State
  which Unity version and checks you used, along with anything left unverified.

The C# compile workflow runs on all pull requests, including forks, pushes to
`main`, and manual dispatch. It uses Unity's bundled Roslyn compiler and reference
assemblies for Unity 2022.3.22f1 and 6000.3.13f1 to compile the Windows, macOS, and
Linux editor branches. It needs no license or secrets and never launches the
Editor. It does not check package import, assembly loading, or editor behavior.
The approach follows [unity-mcp's compile check](https://github.com/CoplayDev/unity-mcp/blob/main/tools/compile-check.sh).
The reference list in `.github/unity-ci/csharp-references.txt` and version symbols
in `compile-csharp.sh` should be checked when updating Unity versions or dependencies.

The Unity compile workflow checks the package in an isolated UPM project using
Unity 2022.3.22f1 and 6000.3.13f1. It runs on repository pull requests, pushes to
`main`, and manual dispatch, and uploads a compilation report. It checks editor
compilation, without building a player or testing interactive UI behavior.

Maintainers configure `UNITY_EMAIL` and `UNITY_PASSWORD` as Actions secrets for
GameCI's online Personal activation. Unity jobs are skipped for fork pull requests,
which do not receive these secrets.

## VPM releases

The maintained package uses the `VRCLearn.HierarchyDecorator` namespace and
`VRCLearn.HierarchyDecorator.Editor` assembly. Keep its asset GUIDs, menus and
settings separate from upstream. Upstream coexistence must work without removing
the original package; old identities are used only for optional settings import.
Extensions targeting this fork must reference these independent identities.

Publishing follows the VRCLearn convention: immutable GitHub Release assets and
an official VRChat template hosted on GitHub Pages. See
[publishing instructions](.github/docs/PUBLISHING.md) for local builds, validation,
version tags, and the existing package ID migration.

Run `python -m unittest discover -s .github/scripts -v` before changing distribution
code. The package workflow runs these tests and builds the archives on Windows,
macOS and Linux with Python 3.10 and 3.12. Keep the existing Unity compilation
checks for C# changes. Distribution checks do not test interactive editor behavior.

## AI-assisted development

AI-assisted development is welcome. Contributors are responsible for understanding,
reviewing, and validating the code they submit, including its correctness and
maintainability. The same contribution standards apply regardless of the tools used.

## License

Contributions are made under the project's [MIT license](LICENSE.md).
