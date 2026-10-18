"""Validate a published release before treating an automated retry as complete."""
import hashlib
import json
from pathlib import Path
import sys


def validate(release: dict, output: Path) -> None:
    manifest = json.loads((output / "package.json").read_text(encoding="utf-8"))
    if release.get("draft") or release.get("prerelease") or release.get("tag_name") != manifest["version"]:
        raise ValueError("Existing release is not the expected stable published version; assets were not changed.")
    files = [output / (manifest["name"] + "-" + manifest["version"] + suffix) for suffix in (".zip", ".unitypackage")]
    files += [output / "package.json", output / "SHA256SUMS.txt"]
    for path in files:
        matches = [asset for asset in release.get("assets", []) if asset.get("name") == path.name]
        if len(matches) != 1 or matches[0].get("state") != "uploaded":
            raise ValueError("Existing release has an incomplete asset: " + path.name + "; assets were not changed.")
        asset = matches[0]
        if asset.get("size") != path.stat().st_size:
            raise ValueError("Existing release asset size differs from the build: " + path.name)
        if asset.get("digest") and asset["digest"] != "sha256:" + hashlib.sha256(path.read_bytes()).hexdigest():
            raise ValueError("Existing release asset digest differs from the build: " + path.name)


if __name__ == "__main__":
    validate(json.loads(Path(sys.argv[1]).read_text(encoding="utf-8")), Path(sys.argv[2]))
