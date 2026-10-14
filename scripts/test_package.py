from pathlib import Path
import tempfile
import unittest
import zipfile

from package import package, release_notes


class PackageTests(unittest.TestCase):
    def test_notes_require_dated_nonempty_release(self):
        """Verify dated release notes are extracted and missing, invalid, or empty entries are rejected."""
        self.assertEqual("### Added\n- Example.\n", release_notes(
            "## [1.0.0] - 2026-10-15\n\n### Added\n- Example.\n\n## [0.9.0] - 2026-09-01\n", "1.0.0"))
        for text in ("## [Unreleased]\n- Example.", "## [1.0.0] - 2026-99-99\n- Example.",
                     "## [1.0.0] - 2026-10-15\n"):
            with self.assertRaises(ValueError):
                release_notes(text, "1.0.0")

    def test_exact_layout_version_and_reproducibility(self):
        """Verify archive contents, reproducibility, guide links, and rejection of invalid or mismatched tags."""
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "src").mkdir()
            (root / "docs").mkdir()
            (root / "src/UnityVietnameseFontSync.csproj").write_text(
                "<Project><PropertyGroup><Version>1.0.0</Version>"
                "<BepInExPluginGuid>zeldrisho.UnityVietnameseFontSync</BepInExPluginGuid>"
                "</PropertyGroup></Project>")
            (root / "CHANGELOG.md").write_text("## [1.0.0] - 2026-10-15\n### Added\n- Example.\n")
            for name in ("README.md", "LICENSE", "docs/configuration.md", "docs/development.md"):
                (root / name).write_text("example")
            (root / "README.md").write_text("See [configuration](docs/configuration.md).")
            (root / "private.ttf").write_text("must not ship")
            dll = root / "UnityVietnameseFontSync.dll"
            dll.write_bytes(b"test fixture")
            first = package(root, "v1.0.0", dll, root / "out1")
            second = package(root, "v1.0.0", dll, root / "out2")
            self.assertEqual(first.name, "UnityVietnameseFontSync-1.0.0.zip")
            self.assertEqual(first.read_bytes(), second.read_bytes())
            prefix = "plugins/zeldrisho.UnityVietnameseFontSync/"
            with zipfile.ZipFile(first) as bundle:
                self.assertEqual(set(bundle.namelist()), {
                    prefix + name for name in ("UnityVietnameseFontSync.dll", "README.md", "CHANGELOG.md", "LICENSE")
                })
                self.assertNotIn(b"docs/configuration.md", bundle.read(prefix + "README.md"))
            self.assertEqual({file.name for file in first.parent.iterdir()}, {first.name, "notes.md"})
            hosted = package(root, "v1.0.0", dll, root / "hosted", "https://github.com/example/project")
            with zipfile.ZipFile(hosted) as bundle:
                self.assertIn(b"https://github.com/example/project/blob/v1.0.0/docs/configuration.md",
                              bundle.read(prefix + "README.md"))
            for tag in ("v1.0.1", "v1.0.0-beta", "../../evil", "v01.0.0"):
                with self.assertRaises(ValueError):
                    package(root, tag, dll, root / "bad")


if __name__ == "__main__":
    unittest.main()
