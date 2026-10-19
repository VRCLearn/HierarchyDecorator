"""Check archive contents, asset identity, migration scope and release provenance."""
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
import zipfile

import build_package as builder
import check_existing_release
import prepare_release


class DistributionTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.package = self.root / "source"
        self.package.mkdir()
        self.manifest = json.loads((builder.ROOT / "package.json").read_text(encoding="utf-8"))
        for filename in builder.ROOT_FILES:
            (self.package / filename).write_bytes((builder.ROOT / filename).read_bytes())
            (self.package / (filename + ".meta")).write_bytes((builder.ROOT / (filename + ".meta")).read_bytes())
        self.asset("HierarchyDecorator", folder=True)
        self.asset("HierarchyDecorator/Empty", folder=True)
        self.asset("HierarchyDecorator/Example.cs", b"// Package fixture\n")
        for module in (builder, prepare_release):
            patcher = patch.object(module, "PACKAGE", self.package)
            patcher.start()
            self.addCleanup(patcher.stop)

    def asset(self, relative, content=b"", folder=False):
        path = self.package / relative
        if folder:
            path.mkdir(parents=True, exist_ok=True)
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(content)
        path.with_name(path.name + ".meta").write_bytes(builder.generated_metadata(relative, folder))
        return path

    def write_manifest(self, **overrides):
        manifest = dict(self.manifest, **overrides)
        (self.package / "package.json").write_text(json.dumps(manifest), encoding="utf-8")

    def build(self, directory="release"):
        return builder.build(self.root / directory, repository=builder.REPOSITORY, revision="ab" * 20)

    def test_archives_preserve_asset_guids_and_empty_folders_without_source_changes(self):
        original = {path.relative_to(self.package).as_posix(): path.read_bytes()
                    for path in self.package.rglob("*") if path.is_file()}
        result = self.build()
        manifest = json.loads(Path(result["manifest"]).read_bytes())
        self.assertEqual(manifest["sourceRevision"], "ab" * 20)
        self.assertEqual(manifest["repository"]["revision"], "ab" * 20)
        self.assertEqual(manifest["legacyPackages"], ["com.wooshii.hierarchydecorator"])
        self.assertEqual(manifest["legacyFolders"], {builder.UNITY_ROOT: builder.ROOT_GUID})
        self.assertNotIn("zipSHA256", manifest)
        zipped = builder.validate_zip(Path(result["archive"]), manifest)
        imported = builder.validate_unitypackage(Path(result["unitypackage"]), manifest)
        with zipfile.ZipFile(result["archive"]) as archive:
            self.assertIn("HierarchyDecorator/Empty/", archive.namelist())
        for relative, content in original.items():
            if relative != "package.json":
                self.assertEqual(zipped[relative], content)
        asset = imported[builder.UNITY_ROOT + "/HierarchyDecorator/Example.cs"]
        self.assertEqual(asset["asset.meta"], original["HierarchyDecorator/Example.cs.meta"])
        self.assertEqual(asset["asset"], original["HierarchyDecorator/Example.cs"])
        self.assertEqual(original, {path.relative_to(self.package).as_posix(): path.read_bytes()
                                    for path in self.package.rglob("*") if path.is_file()})

    def test_build_is_reproducible_and_checksums_match(self):
        first, second = self.build("first"), self.build("second")
        for name in ("archive", "unitypackage"):
            self.assertEqual(Path(first[name]).read_bytes(), Path(second[name]).read_bytes())
        checksums = (self.root / "first/SHA256SUMS.txt").read_text(encoding="utf-8")
        for name in ("archive", "unitypackage"):
            path = Path(first[name])
            self.assertIn(hashlib.sha256(path.read_bytes()).hexdigest() + "  " + path.name, checksums)

    def test_excludes_development_and_unity_ignored_names_with_their_metadata(self):
        for relative in ("HierarchyDecorator/.hidden", "HierarchyDecorator/Debug~", "HierarchyDecorator/__pycache__",
                         "HierarchyDecorator/CVS", "HierarchyDecorator/Scratch.tmp"):
            self.asset(relative, folder=True)
            self.asset(relative + "/Ignored.cs", b"// excluded")
        (self.package / ".github").mkdir()
        (self.package / ".github/secret.txt").write_text("excluded", encoding="utf-8")
        result = self.build()
        with zipfile.ZipFile(result["archive"]) as archive:
            names = archive.namelist()
        self.assertFalse(any(any(word in name for word in ("hidden", "Debug", "pycache", "CVS", "Scratch", ".github")) for name in names))

    def test_missing_metadata_and_duplicate_guids_fail_before_output(self):
        path = self.package / "HierarchyDecorator/Example.cs.meta"
        content = path.read_bytes()
        path.unlink()
        with self.assertRaisesRegex(ValueError, "missing existing Unity metadata"):
            self.build()
        path.write_bytes(content)
        other = self.asset("HierarchyDecorator/Other.cs")
        other.with_name(other.name + ".meta").write_bytes(content)
        with self.assertRaisesRegex(ValueError, "duplicate GUID"):
            self.build()
        self.assertFalse((self.root / "release").exists())

    def test_rejects_wrong_identity_license_migration_and_stale_download_url(self):
        cases = ({"name": "another.package"}, {"license": "unlicensed"}, {"version": "v1.0.0"},
                 {"vpmDependencies": {"com.vrchat.avatars": "1.0.0"}},
                 {"legacyPackages": ["another.package"]},
                 {"legacyPackages": []}, {"zipSHA256": "a" * 64},
                 {"legacyFolders": {"Assets/HierarchyDecorator": "a" * 32}},
                 {"url": "https://example.test/other.zip"})
        for override in cases:
            with self.subTest(override=override):
                self.write_manifest(**override)
                with self.assertRaises(ValueError):
                    self.build()

    def test_rejects_missing_license_notice_and_source_revision(self):
        for repository, revision in (("other/project", "ab" * 20), (builder.REPOSITORY, None),
                                     (builder.REPOSITORY, "short")):
            with self.subTest(repository=repository, revision=revision), self.assertRaises(ValueError):
                builder.build(self.root / "release", repository=repository, revision=revision)
        (self.package / "LICENSE.md").write_text("MIT", encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "MIT license"):
            self.build()

    def test_release_uses_exact_version_notes_and_rejects_other_tags(self):
        version = self.manifest["version"]
        release_text = "### Changes\n\n- Replace the former settings layout.\n- Keep custom icons."
        (self.package / "CHANGELOG.md").write_text(
            "## Unreleased\n\n- Future work.\n\n## [" + version + "] - 2026-10-19\n\n" +
            release_text + "\n\n## [0.0.1]\n\n- Previous release.\n", encoding="utf-8")
        for tag in ("v" + version, "0.12.1", "main", None):
            with self.subTest(tag=tag), self.assertRaisesRegex(ValueError, "tag"):
                prepare_release.prepare(self.root / "release", tag, builder.REPOSITORY, "ab" * 20)
        prepare_release.prepare(self.root / "release", version, builder.REPOSITORY, "ab" * 20)
        notes = (self.root / "release/release-notes.md").read_text(encoding="utf-8")
        self.assertEqual(notes, f"Hierarchy Decorator {version}\n\n" + release_text + "\n")
        (self.package / "CHANGELOG.md").write_text("## Unreleased\n\n- Future work.\n", encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "Changelog"):
            prepare_release.prepare(self.root / "missing-notes", version, builder.REPOSITORY, "ab" * 20)
        self.assertFalse((self.root / "missing-notes").exists())

    def test_listing_preserves_repository_identity_and_only_lists_maintained_package(self):
        source = json.loads((builder.ROOT / ".github/vpm/source.json").read_text(encoding="utf-8"))
        self.assertEqual(source["id"], "io.github.vrclearn.hierarchydecorator")
        self.assertEqual(source["url"], "https://vrclearn.github.io/HierarchyDecorator/index.json")
        self.assertEqual(source["packages"], {builder.PACKAGE_ID: builder.REPOSITORY})
        self.assertNotIn("Assets/HierarchyDecorator", self.manifest["legacyFolders"])

    def test_release_asset_and_assembly_identities_are_independent_from_upstream(self):
        # This committed snapshot is extracted from the existing 0.12.1 tag.
        # Keep the test offline and independent of shallow-clone Git history.
        fixture = json.loads((builder.ROOT / ".github/unity-ci/upstream-0.12.1-identities.json")
                             .read_text(encoding="utf-8"))
        with patch.object(builder, "PACKAGE", builder.ROOT):
            assets = builder.source_assets(self.manifest)
        upstream_guids = set(fixture["metadata"].values())
        collisions = {asset.path: builder.metadata_guid(asset.metadata) for asset in assets
                      if builder.metadata_guid(asset.metadata) in upstream_guids}
        self.assertEqual(collisions, {}, "New release must not reuse upstream asset GUIDs")
        definitions = [json.loads(asset.content) for asset in assets if asset.path.endswith(".asmdef")]
        assemblies = {definition["name"] for definition in definitions}
        self.assertEqual(assemblies, {"VRCLearn.HierarchyDecorator.Editor"})
        self.assertFalse(assemblies.intersection(fixture["assemblyNames"]))
        current_guids = {builder.metadata_guid(asset.metadata) for asset in assets}
        for definition in definitions:
            for reference in definition.get("references", []):
                if reference.startswith("GUID:"):
                    self.assertIn(reference.removeprefix("GUID:"), current_guids,
                                  "Internal assembly references must resolve within this standalone package")
        self.assertEqual(self.manifest["legacyPackages"], [fixture["packageName"]])

    def test_csharp_namespaces_do_not_export_upstream_types(self):
        for source in (builder.ROOT / "HierarchyDecorator").rglob("*.cs"):
            namespaces = re.findall(r"^\s*namespace\s+([\w.]+)", source.read_text(encoding="utf-8-sig"), re.MULTILINE)
            with self.subTest(source=source.relative_to(builder.ROOT).as_posix()):
                self.assertTrue(namespaces, "Each package source must use an explicit namespace")
                self.assertTrue(all(name == "HierarchyDecorator.Editor" or
                                    name.startswith("HierarchyDecorator.Editor.") for name in namespaces), namespaces)

    def test_unity_project_can_include_both_local_packages_without_replacement(self):
        # Relative Unity package paths require the project and repository to be
        # on the same drive; hosted Windows runners keep TEMP on another drive.
        temporary = tempfile.TemporaryDirectory(dir=builder.ROOT, prefix=".coexistence-test-")
        self.addCleanup(temporary.cleanup)
        root = Path(temporary.name).resolve()
        upstream = root / "upstream package"
        upstream.mkdir()
        (upstream / "package.json").write_text(json.dumps({"name": "com.wooshii.hierarchydecorator"}), encoding="utf-8")
        project = root / "coexistence project"
        prepared = subprocess.run([sys.executable, str(builder.ROOT / ".github/unity-ci/prepare_project.py"),
                        "--unity-version", "2022.3.22f1", "--project-path", str(project),
                        "--upstream-package", str(upstream)], capture_output=True, text=True)
        self.assertEqual(prepared.returncode, 0, prepared.stderr)
        manifest = json.loads((project / "Packages/manifest.json").read_text(encoding="utf-8"))
        self.assertEqual(set(manifest["dependencies"]),
                         {builder.PACKAGE_ID, "com.wooshii.hierarchydecorator"})
        for package_id, path in manifest["dependencies"].items():
            target = (project / "Packages" / path.removeprefix("file:")).resolve()
            expected = upstream if package_id == "com.wooshii.hierarchydecorator" else builder.ROOT
            self.assertEqual(target, expected.resolve())

        (upstream / "package.json").write_text(json.dumps({"name": "wrong.package"}), encoding="utf-8")
        rejected = root / "rejected project"
        result = subprocess.run([sys.executable, str(builder.ROOT / ".github/unity-ci/prepare_project.py"),
                                 "--unity-version", "2022.3.22f1", "--project-path", str(rejected),
                                 "--upstream-package", str(upstream)], capture_output=True, text=True)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("Expected an upstream", result.stderr)
        self.assertFalse(rejected.exists())

    def test_existing_release_requires_complete_matching_assets(self):
        result = self.build()
        output = Path(result["manifest"]).parent
        paths = [Path(result["archive"]), Path(result["unitypackage"]), output / "package.json", output / "SHA256SUMS.txt"]
        assets = [{"name": path.name, "state": "uploaded", "size": path.stat().st_size,
                   "digest": "sha256:" + hashlib.sha256(path.read_bytes()).hexdigest()} for path in paths]
        release = {"tag_name": self.manifest["version"], "draft": False, "prerelease": False, "assets": assets}
        check_existing_release.validate(release, output)
        for override in ({"draft": True}, {"prerelease": True}, {"tag_name": "0.12.1"}, {"assets": assets[:-1]}):
            with self.subTest(override=override), self.assertRaises(ValueError):
                check_existing_release.validate(dict(release, **override), output)
        assets[0]["digest"] = "sha256:" + "0" * 64
        with self.assertRaisesRegex(ValueError, "digest"):
            check_existing_release.validate(release, output)


if __name__ == "__main__":
    unittest.main()
