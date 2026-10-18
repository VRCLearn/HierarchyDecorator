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

The Unity compile workflow checks the package in an isolated UPM project using
Unity 2022.3.22f1 and 6000.3.13f1. It runs on repository pull requests, pushes to
`main`, and manual dispatch, and uploads a compilation report. It checks editor
compilation, without building a player or testing interactive UI behavior.

Maintainers configure `UNITY_EMAIL` and `UNITY_PASSWORD` as Actions secrets for
GameCI's online Personal activation. Unity jobs are skipped for fork pull requests,
which do not receive these secrets.

## AI-assisted development

AI-assisted development is welcome. Contributors are responsible for understanding,
reviewing, and validating the code they submit, including its correctness and
maintainability. The same contribution standards apply regardless of the tools used.

## License

Contributions are made under the project's [MIT license](LICENSE.md).
