"""Check archive contents, asset identity, migration scope and release provenance."""
import hashlib
import json
from pathlib import Path
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
                 {"legacyPackages": ["another.package"]}, {"zipSHA256": "a" * 64},
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
        for tag in ("v" + version, "0.12.1", "main", None):
            with self.subTest(tag=tag), self.assertRaisesRegex(ValueError, "tag"):
                prepare_release.prepare(self.root / "release", tag, builder.REPOSITORY, "ab" * 20)
        prepare_release.prepare(self.root / "release", version, builder.REPOSITORY, "ab" * 20)
        notes = (self.root / "release/release-notes.md").read_text(encoding="utf-8")
        self.assertIn("### Added", notes)
        self.assertIn("com.vrclearn.hierarchydecorator", notes)
        self.assertNotIn("## Unreleased", notes)
        self.assertNotIn("## v0.12.0", notes)
        (self.package / "CHANGELOG.md").write_text("## Unreleased\n\n- Future work.\n", encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "Changelog"):
            prepare_release.prepare(self.root / "missing-notes", version, builder.REPOSITORY, "ab" * 20)
        self.assertFalse((self.root / "missing-notes").exists())

    def test_listing_preserves_repository_identity_and_both_package_ids(self):
        source = json.loads((builder.ROOT / ".github/vpm/source.json").read_text(encoding="utf-8"))
        self.assertEqual(source["id"], "io.github.vrclearn.hierarchydecorator")
        self.assertEqual(source["url"], "https://vrclearn.github.io/HierarchyDecorator/index.json")
        self.assertEqual(source["packages"], {builder.PACKAGE_ID: builder.REPOSITORY,
                                            "com.wooshii.hierarchydecorator": builder.REPOSITORY})
        self.assertNotIn("Assets/HierarchyDecorator", self.manifest["legacyFolders"])

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
