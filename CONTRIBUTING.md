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

## AI-assisted development

AI-assisted development is welcome. Contributors are responsible for understanding,
reviewing, and validating the code they submit, including its correctness and
maintainability. The same contribution standards apply regardless of the tools used.

## License

Contributions are made under the project's [MIT license](LICENSE.md).
