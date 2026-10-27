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
    parser.add_argument("--prefab-override-probe", action="store_true",
                        help="Include the graphical prefab override regression probe in this disposable project.")
    parser.add_argument("--upstream-package", type=Path,
                        help="Also install a local upstream package to verify both editor assemblies coexist.")
    args = parser.parse_args()
    if not re.fullmatch(r"\d+\.\d+\.\d+[abfp]\d+", args.unity_version):
        parser.error("Expected a complete Unity Editor version, such as 2022.3.22f1.")

    repository = Path(__file__).resolve().parents[2]
    package = json.loads((repository / "package.json").read_text(encoding="utf-8"))
    upstream = None
    if args.upstream_package:
        upstream = args.upstream_package.resolve()
        upstream_manifest = json.loads((upstream / "package.json").read_text(encoding="utf-8"))
        if upstream_manifest.get("name") != "com.wooshii.hierarchydecorator":
            parser.error("Expected an upstream com.wooshii.hierarchydecorator package.")
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
    if upstream:
        relative_upstream = os.path.relpath(upstream, packages).replace(os.sep, "/")
        manifest["dependencies"]["com.wooshii.hierarchydecorator"] = "file:" + relative_upstream
    (packages / "manifest.json").write_text(json.dumps(manifest, indent=4) + "\n", encoding="utf-8")
    (settings / "ProjectVersion.txt").write_text("m_EditorVersion: " + args.unity_version + "\n", encoding="utf-8")
    shutil.copyfile(Path(__file__).with_name("CompileCheck.cs"), editor / "CompileCheck.cs")
    if args.prefab_override_probe:
        shutil.copyfile(Path(__file__).with_name("PrefabOverrideProbe.cs"), editor / "PrefabOverrideProbe.cs")
        (settings / "PrefabOverrideProbe.enabled").touch()
    print(f"Prepared Unity {args.unity_version} project at {project}")


if __name__ == "__main__":
    main()
