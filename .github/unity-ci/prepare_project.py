import argparse
import json
import os
import re
import shutil
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description="Create an isolated Unity package compilation project.")
    parser.add_argument("--unity-version", required=True)
    parser.add_argument("--project-path", required=True, type=Path)
    args = parser.parse_args()
    if not re.fullmatch(r"\d+\.\d+\.\d+[abfp]\d+", args.unity_version):
        parser.error("Expected a complete Unity Editor version, such as 2022.3.22f1.")

    repository = Path(__file__).resolve().parents[2]
    package = json.loads((repository / "package.json").read_text(encoding="utf-8"))
    project = args.project_path.resolve()
    if project.exists():
        raise FileExistsError(f"Refusing to overwrite an existing project: {project}")

    packages = project / "Packages"
    editor = project / "Assets" / "Editor"
    settings = project / "ProjectSettings"
    for directory in (packages, editor, settings):
        directory.mkdir(parents=True, exist_ok=True)

    relative_package = os.path.relpath(repository, packages).replace(os.sep, "/")
    manifest = {"dependencies": {package["name"]: "file:" + relative_package}}
    (packages / "manifest.json").write_text(json.dumps(manifest, indent=4) + "\n", encoding="utf-8")
    (settings / "ProjectVersion.txt").write_text("m_EditorVersion: " + args.unity_version + "\n", encoding="utf-8")
    shutil.copyfile(Path(__file__).with_name("CompileCheck.cs"), editor / "CompileCheck.cs")
    print(f"Prepared Unity {args.unity_version} project at {project}")


if __name__ == "__main__":
    main()
