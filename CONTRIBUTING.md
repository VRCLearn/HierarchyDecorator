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
- Keep asset, assembly, namespace and settings identities independent from upstream.
  In Unity, removing an upstream installation requires explicit migration.
- Explain what changed and why, and link related issues when applicable. State
  which Unity version and checks you used, along with anything left unverified.

## Validation

Run `python -m unittest discover -s .github/scripts -v` for distribution changes.
CI builds the archives on Windows, macOS and Linux and checks C# compilation with
Unity 2022.3.22f1 and 6000.3.13f1. Native Unity checks require activation credentials
and are skipped for fork pull requests. Validate editor behavior in Unity when
changing it; compilation checks do not exercise interactive UI.

To check native prefab override markers in a disposable graphical Editor:

```sh
python .github/unity-ci/prepare_project.py --unity-version 2022.3.22f1 --project-path .unity-ci~/prefab-markers --prefab-override-probe
```

Launch that project with the matching Unity executable and
`-executeMethod HierarchyDecoratorCI.PrefabOverrideProbe.Run` (add
`-prefabProbeLight` to check the light skin). The probe compares
the real Hierarchy's native marker pixels with and without decoration, checks
active-toggle input, and saves its report and window screenshots under
`Logs/PrefabOverrideProbe` before exiting. It creates fixtures only in that
disposable project and does not require editing a user's scene.

For VRChat component icons, copy `.github/unity-ci/VrcComponentIconProbe.cs` into
`Assets/Editor` in a disposable Unity 2022.3 project and run
`-executeMethod HierarchyDecoratorCI.VrcComponentIconProbe.Run`. Run once without
the SDK, then in separate projects with the real VRChat Avatars and Worlds SDKs.
Use `-vrcIconProbeSdk` for both, and also `-vrcIconProbeWorlds` for the Worlds
project. SDK projects need the usual Unity built-in modules and Unity Test
Framework required by the SDK.
Add `-vrcIconProbeGraphic` in a graphical Editor to capture the real Hierarchy
with normal and stacked icons using the Editor's current native theme.
Reports and screenshots are saved under `Logs/VrcComponentIconProbe`.

## AI-assisted development

AI-assisted development is welcome. Contributors are responsible for understanding,
reviewing, and validating the code they submit, including its correctness and
maintainability. The same contribution standards apply regardless of the tools used.

## License

Contributions are made under the project's [MIT license](LICENSE.md).
