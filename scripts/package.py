#!/usr/bin/env python3
"""Validate a stable release and package only plugin-owned files."""

import argparse
import datetime
import os
from pathlib import Path
import re
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[1]
PROJECT = "UnityVietnameseFontSync"


def release_notes(changelog, version):
    match = re.search(
        rf"^## \[{re.escape(version)}\] - (\d{{4}}-\d{{2}}-\d{{2}})\s*\n(.*?)(?=^## |\Z)",
        changelog,
        re.MULTILINE | re.DOTALL,
    )
    if not match:
        raise ValueError(f"Add a dated [{version}] entry to CHANGELOG.md before tagging.")
    datetime.date.fromisoformat(match.group(1))
    notes = match.group(2).strip()
    if not notes or not re.search(r"^- \S", notes, re.MULTILINE):
        raise ValueError("Release notes must contain a human-readable change entry.")
    return notes + "\n"


def package(root, tag, dll, output, repository_url=None):
    if not re.fullmatch(r"v(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)", tag):
        raise ValueError("Use a stable SemVer tag such as v1.0.0.")
    version = tag[1:]
    project = ET.parse(root / "src" / f"{PROJECT}.csproj")
    if project.findtext(".//Version") != version:
        raise ValueError("Release tag must match the project Version.")
    notes = release_notes((root / "CHANGELOG.md").read_text(encoding="utf-8"), version)
    plugin_id = project.findtext(".//BepInExPluginGuid")
    if not plugin_id or not re.fullmatch(r"[A-Za-z0-9_]+(?:[.-][A-Za-z0-9_]+)*", plugin_id):
        raise ValueError("Project BepInExPluginGuid must be a safe directory name.")
    prefix = f"plugins/{plugin_id}"
    # An explicit allowlist prevents game DLLs, fonts, local notes, or build dependencies leaking.
    files = {
        f"{prefix}/{PROJECT}.dll": dll,
        f"{prefix}/README.md": root / "README.md",
        f"{prefix}/CHANGELOG.md": root / "CHANGELOG.md",
        f"{prefix}/LICENSE": root / "LICENSE",
    }
    for source in files.values():
        if not source.is_file():
            raise ValueError(f"Missing package input: {source}")
    output.mkdir(parents=True, exist_ok=True)
    archive = output / f"{PROJECT}-{version}.zip"
    with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_DEFLATED) as bundle:
        for name, source in files.items():
            entry = zipfile.ZipInfo(name, date_time=(1980, 1, 1, 0, 0, 0))
            entry.compress_type = zipfile.ZIP_DEFLATED
            entry.external_attr = 0o100644 << 16
            content = source.read_bytes()
            if source == root / "README.md":
                # Guides stay in the repository, not the install archive. Avoid broken local links.
                def documentation_link(match):
                    label, path = match.groups()
                    if repository_url:
                        return f"[{label}]({repository_url.rstrip('/')}/blob/{tag}/{path})"
                    return f"{label} (see the project repository)"
                content = re.sub(r"\[([^\]]+)\]\((docs/[^)]+)\)", documentation_link,
                                 content.decode("utf-8")).encode("utf-8")
            bundle.writestr(entry, content)
    (output / "notes.md").write_text(notes, encoding="utf-8")
    return archive


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--tag", required=True)
    parser.add_argument("--dll", type=Path, required=True)
    parser.add_argument("--output", type=Path, default=ROOT / "artifacts/release")
    args = parser.parse_args()
    try:
        repository = os.environ.get("GITHUB_REPOSITORY")
        server = os.environ.get("GITHUB_SERVER_URL", "https://github.com")
        repository_url = f"{server}/{repository}" if repository else None
        print(package(ROOT, args.tag, args.dll, args.output, repository_url))
    except ValueError as error:
        parser.exit(1, f"{error}\n")


if __name__ == "__main__":
    main()
