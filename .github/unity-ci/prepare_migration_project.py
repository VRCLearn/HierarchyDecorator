"""Prepare new disposable projects for native migration checks. Never modifies existing projects."""
import argparse
import json
import os
import re
import shutil
import uuid
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--unity-version", required=True)
    parser.add_argument("--project-path", required=True, type=Path)
    parser.add_argument("--upstream-package", required=True, type=Path)
    parser.add_argument("--mode", required=True, choices=("assets", "local", "embedded", "settings-only", "empty"))
    parser.add_argument("--fail-save", action="store_true")
    parser.add_argument("--active-guid", choices=("valid", "missing", "stale", "wrong-type"), default="valid")
    parser.add_argument("--source-in-package", action="store_true", help="Store the selected settings inside the fixture package.")
    parser.add_argument("--assets-default-path", action="store_true", help="Install Assets fixtures at the upstream default path instead of a renamed folder.")
    parser.add_argument("--empty-assets-installation", action="store_true", help="Place an Assets installation without user content under a user-owned parent folder.")
    parser.add_argument("--regenerate-all-guids", action="store_true", help="Regenerate all Assets distribution and root folder GUIDs to isolate default-path detection.")
    args = parser.parse_args()
    if not re.fullmatch(r"\d+\.\d+\.\d+[abfp]\d+", args.unity_version):
        parser.error("Expected a complete Unity Editor version.")
    upstream = args.upstream_package.resolve()
    if json.loads((upstream / "package.json").read_text(encoding="utf-8"))["name"] != "com.wooshii.hierarchydecorator":
        parser.error("Expected the upstream com.wooshii.hierarchydecorator package.")
    if args.mode == "empty":
        args.active_guid = "missing"
    if args.fail_save and args.active_guid != "valid":
        parser.error("--fail-save requires a valid active configuration.")
    if args.source_in_package and args.mode not in ("embedded", "settings-only"):
        parser.error("--source-in-package requires --mode embedded or settings-only.")
    if args.assets_default_path and args.mode != "assets":
        parser.error("--assets-default-path requires --mode assets.")
    if args.empty_assets_installation and (args.mode != "assets" or args.assets_default_path):
        parser.error("--empty-assets-installation requires --mode assets without --assets-default-path.")
    if args.regenerate_all_guids and args.mode != "assets":
        parser.error("--regenerate-all-guids requires --mode assets.")
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
    regenerated_asset = ""
    regenerated_guid = ""
    if args.mode == "assets":
        old_root = project / "Assets" / ("HierarchyDecorator" if args.assets_default_path else "RenamedWooshiiPlugin")
        if args.empty_assets_installation:
            old_root = project / "Assets" / "UserTools" / "RenamedWooshiiPlugin"
        shutil.copytree(upstream / "HierarchyDecorator", old_root)
        shutil.copyfile(upstream / "HierarchyDecorator.meta", old_root.with_suffix(".meta"))
        if not args.empty_assets_installation:
            (old_root / "UserNote.txt").write_text("This file belongs to the user, not the plugin.\n", encoding="utf-8")
        # The code still compiles, but GUID-only cleanup cannot identify this distributed script.
        regenerated = old_root / "Scripts" / "Editor" / "Util" / "GUIHelper.cs"
        script_metadata = regenerated.with_suffix(".cs.meta")
        metadata_files = list(old_root.rglob("*.meta")) + [old_root.with_suffix(".meta")] if args.regenerate_all_guids else [script_metadata]
        for metadata in metadata_files:
            new_guid = uuid.uuid4().hex
            value, replaced = re.subn(r"(?m)^guid: [0-9a-fA-F]{32}[ \t]*$", "guid: " + new_guid,
                                      metadata.read_text(encoding="utf-8"), count=1)
            if replaced != 1:
                raise ValueError(f"Expected a GUID in the upstream fixture: {metadata}")
            metadata.write_text(value, encoding="utf-8")
            if metadata == script_metadata:
                regenerated_guid = new_guid
        regenerated_asset = regenerated.relative_to(project).as_posix()
    elif args.mode in ("embedded", "settings-only", "empty"):
        package_id = "com.wooshii.hierarchydecorator" if args.mode == "embedded" else "dev.vrclearn.migration-source"
        old_root = packages / package_id
        shutil.copytree(upstream, old_root)
        if args.mode != "embedded":
            # Supply the old Settings type without identifying an installation to remove.
            manifest_path = old_root / "package.json"
            manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
            manifest["name"] = package_id
            manifest_path.write_text(json.dumps(manifest, indent=4) + "\n", encoding="utf-8")
    else:
        # A separate source fixture outside Assets/Packages ensures Client.Remove must not delete it.
        old_root = project / "ExternalSource~"
        shutil.copytree(upstream, old_root)
        dependencies["com.wooshii.hierarchydecorator"] = "file:" + os.path.relpath(old_root, packages).replace(os.sep, "/")
    (packages / "manifest.json").write_text(json.dumps({"dependencies": dependencies}, indent=4) + "\n", encoding="utf-8")
    (settings / "ProjectVersion.txt").write_text("m_EditorVersion: " + args.unity_version + "\n", encoding="utf-8")
    (settings / "MigrationProbe.enabled").write_text(json.dumps({
        "mode": args.mode,
        "upstreamRoot": str(old_root),
        "failSave": args.fail_save,
        "emptyAssets": args.empty_assets_installation,
        "activeGuid": args.active_guid,
        "sourcePath": (old_root.relative_to(project) / "CurrentSettings.asset").as_posix() if args.source_in_package else "Assets/MigrationSettings/CurrentRenamed.asset",
        "regeneratedAsset": regenerated_asset,
        "regeneratedGuid": regenerated_guid,
        "regeneratedAllGuids": args.regenerate_all_guids,
    }, indent=4) + "\n", encoding="utf-8")
    shutil.copyfile(Path(__file__).with_name("MigrationProbe.cs"), editor / "MigrationProbe.cs")
    print(f"Prepared {args.mode} migration fixture at {project}")


if __name__ == "__main__":
    main()
