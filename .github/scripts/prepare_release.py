"""Build release artifacts and notes for the exact numeric package version."""
from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import re

from build_package import build, PACKAGE, PACKAGE_ID, REPOSITORY, ROOT


def release_notes(version: str) -> str:
    changelog = (PACKAGE / "CHANGELOG.md").read_text(encoding="utf-8-sig")
    match = re.search(
        r"^##[ \t]+\[?" + re.escape(version) +
        r"\]?(?:[ \t]+-[^\n]*)?[ \t]*\r?\n(.*?)(?=^##[ \t]|^\[[^\]\r\n]+\]:[ \t]|\Z)",
        changelog, re.MULTILINE | re.DOTALL)
    if match is None or not match.group(1).strip():
        raise ValueError("Changelog must contain release notes for the exact package version.")
    return (f"Hierarchy Decorator {version}\n\n" + match.group(1).strip() +
            "\n\nInstall from the [VRCLearn VPM repository](https://vrclearn.github.io/HierarchyDecorator/). "
            "Select **HierarchyDecorator** (`com.vrclearn.hierarchydecorator`) in VCC/ALCOMD. "
            "This replaces the former `com.wooshii.hierarchydecorator` VPM package.\n\n"
            f"[Installation and migration instructions](https://github.com/{REPOSITORY}#installation).\n")


def prepare(output: Path, tag: str, repository: str, revision: str) -> dict:
    manifest = json.loads((PACKAGE / "package.json").read_text(encoding="utf-8-sig"))
    version = manifest["version"]
    if tag != version:
        raise ValueError("Release tag must match the package version exactly, without a v prefix.")
    if repository != REPOSITORY:
        raise ValueError("Release repository must identify " + REPOSITORY + ".")
    notes = release_notes(version)
    url = f"https://github.com/{repository}/releases/download/{version}/{PACKAGE_ID}-{version}.zip"
    result = build(output, url, repository=repository, revision=revision)
    (output / "release-notes.md").write_text(notes, encoding="utf-8", newline="\n")
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=ROOT / "Output~/Release")
    args = parser.parse_args()
    print(json.dumps(prepare(args.output, os.environ.get("RELEASE_TAG"),
                             os.environ.get("RELEASE_REPOSITORY"), os.environ.get("RELEASE_REVISION")), indent=4))
