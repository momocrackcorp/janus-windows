import tempfile
import unittest
from pathlib import Path

from known_folder_migrator.core import SafetyError, build_plan, folder_stats, format_bytes, validate_destination


class CoreTests(unittest.TestCase):
    def test_format_bytes(self):
        self.assertEqual(format_bytes(0), "0 B")
        self.assertEqual(format_bytes(1024**3), "1.0 GB")

    def test_stats(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp); (root / "a").write_bytes(b"123")
            self.assertEqual(folder_stats(root), (3, 1))

    def test_rejects_root(self):
        with self.assertRaises(SafetyError):
            validate_destination(Path(Path.cwd().anchor), [])

    def test_rejects_nested_destination(self):
        with tempfile.TemporaryDirectory() as temp:
            source = Path(temp) / "source"; source.mkdir()
            with self.assertRaises(SafetyError):
                validate_destination(source / "inside", [source], Path(temp) / "fake-windows")

    def test_existing_content_blocks_plan(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp); source = root / "src"; source.mkdir()
            base = root / "dest"; occupied = base / "Desktop"; occupied.mkdir(parents=True)
            (occupied / "file.txt").write_text("x")
            with self.assertRaises(SafetyError):
                build_plan(base, ["Escritorio"], {"Escritorio": str(source)})


if __name__ == "__main__":
    unittest.main()
