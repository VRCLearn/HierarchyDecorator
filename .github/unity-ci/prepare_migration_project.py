"""Prepare new disposable projects for native migration checks. Never modifies existing projects."""
import argparse
import json
import os
import re
import shutil
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--unity-version", required=True)
    parser.add_argument("--project-path", required=True, type=Path)
    parser.add_argument("--upstream-package", required=True, type=Path)
    parser.add_argument("--mode", required=True, choices=("assets", "local", "embedded", "vpm"))
    parser.add_argument("--fail-save", action="store_true")
    parser.add_argument("--vpm-resolver", type=Path, help="Use an already downloaded official resolver package; no download is performed.")
    parser.add_argument("--vpm-dependent", action="store_true", help="Add a VPM consumer that prevents upstream removal.")
    parser.add_argument("--expect-preflight-failure", action="store_true")
    args = parser.parse_args()
    if not re.fullmatch(r"\d+\.\d+\.\d+[abfp]\d+", args.unity_version):
        parser.error("Expected a complete Unity Editor version.")
    upstream = args.upstream_package.resolve()
    if json.loads((upstream / "package.json").read_text(encoding="utf-8"))["name"] != "com.wooshii.hierarchydecorator":
        parser.error("Expected the upstream com.wooshii.hierarchydecorator package.")
    resolver_manifest = None
    if args.vpm_resolver:
        resolver_manifest = json.loads((args.vpm_resolver / "package.json").read_text(encoding="utf-8"))
        if resolver_manifest["name"] != "com.vrchat.core.vpm-resolver":
            parser.error("Expected an official com.vrchat.core.vpm-resolver package.")
    if args.mode != "vpm" and (args.vpm_resolver or args.vpm_dependent or args.expect_preflight_failure):
        parser.error("VPM options require --mode vpm.")
    project = args.project_path.resolve()
    if project.exists():
        raise FileExistsError(f"Refusing to overwrite an existing project: {project}")
    repository = Path(__file__).resolve().parents[2]
    new_name = json.loads((repository / "package.json").read_text(encoding="utf-8"))["name"]
    packages = project / "Packages"
    editor = project / "Assets" / "Editor"
    settings = project / "ProjectSettings"
    for directory in (packages, editor, settings):
        directory.mkdir(parents=True, exist_ok=True)
    dependencies = {new_name: "file:" + os.path.relpath(repository, packages).replace(os.sep, "/")}
    if args.mode == "assets":
        old_root = project / "Assets" / "RenamedWooshiiPlugin"
        shutil.copytree(upstream / "HierarchyDecorator", old_root)
        shutil.copyfile(upstream / "HierarchyDecorator.meta", old_root.with_suffix(".meta"))
        (old_root / "UserNote.txt").write_text("This file belongs to the user, not the plugin.\n", encoding="utf-8")
    elif args.mode in ("embedded", "vpm"):
        old_root = packages / "com.wooshii.hierarchydecorator"
        shutil.copytree(upstream, old_root)
    else:
        # A separate source fixture outside Assets/Packages ensures Client.Remove must not delete it.
        old_root = project / "ExternalSource~"
        shutil.copytree(upstream, old_root)
        dependencies["com.wooshii.hierarchydecorator"] = "file:" + os.path.relpath(old_root, packages).replace(os.sep, "/")
    if args.mode == "vpm":
        version = json.loads((upstream / "package.json").read_text(encoding="utf-8"))["version"]
        vpm = {
            "dependencies": {"com.wooshii.hierarchydecorator": {"version": version}},
            "locked": {"com.wooshii.hierarchydecorator": {"version": version, "dependencies": {}}},
        }
        if resolver_manifest:
            resolver_id = resolver_manifest["name"]
            shutil.copytree(args.vpm_resolver.resolve(), packages / resolver_id)
            vpm["dependencies"][resolver_id] = {"version": resolver_manifest["version"]}
            vpm["locked"][resolver_id] = {"version": resolver_manifest["version"], "dependencies": {}}
        if args.vpm_dependent:
            consumer_id = "dev.vrclearn.migration-consumer"
            consumer = packages / consumer_id
            consumer.mkdir()
            (consumer / "package.json").write_text(json.dumps({
                "name": consumer_id,
                "version": "0.0.1",
                "vpmDependencies": {"com.wooshii.hierarchydecorator": version},
            }, indent=4) + "\n", encoding="utf-8")
            vpm["dependencies"][consumer_id] = {"version": "0.0.1"}
            vpm["locked"][consumer_id] = {"version": "0.0.1", "dependencies": {"com.wooshii.hierarchydecorator": version}}
        (packages / "vpm-manifest.json").write_text(json.dumps(vpm, indent=4) + "\n", encoding="utf-8")
    (packages / "manifest.json").write_text(json.dumps({"dependencies": dependencies}, indent=4) + "\n", encoding="utf-8")
    (settings / "ProjectVersion.txt").write_text("m_EditorVersion: " + args.unity_version + "\n", encoding="utf-8")
    (settings / "MigrationProbe.enabled").write_text(json.dumps({
        "mode": "embedded" if args.mode == "vpm" else args.mode,
        "upstreamRoot": str(old_root),
        "failSave": args.fail_save,
        "vpm": args.mode == "vpm",
        "expectPreflightFailure": args.expect_preflight_failure,
    }, indent=4) + "\n", encoding="utf-8")
    shutil.copyfile(Path(__file__).with_name("MigrationProbe.cs"), editor / "MigrationProbe.cs")
    print(f"Prepared {args.mode} migration fixture at {project}")


if __name__ == "__main__":
    main()
