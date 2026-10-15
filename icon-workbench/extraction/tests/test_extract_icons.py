import sys
import tempfile
import unittest
from pathlib import Path

from PIL import Image

EXTRACTION_DIR = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(EXTRACTION_DIR))
from extract_icons import extract_sheet  # noqa: E402


class ExtractSheetTests(unittest.TestCase):
    def test_known_grid_offsets_trim_and_outputs_64px_images(self):

        with tempfile.TemporaryDirectory(dir=EXTRACTION_DIR) as temp_dir:
            root = Path(temp_dir)
            sheet_path = root / "grid.png"
            sheet = Image.new("RGB", (40, 40), (16, 24, 32))
            sheet.save(sheet_path)

            result = extract_sheet(
                sheet_path,
                root / "out",
                rows=2,
                cols=2,
                crop_size=12,
                cut_edges=2,
                manual_offsets={(0, 0): (3, 2)},
                search_radius=0,
            )

            first = result["icons"][0]
            self.assertEqual(first["nominal_center"], [10.0, 10.0])
            self.assertEqual(first["final_center"], [13.0, 8.0])
            self.assertEqual(first["crop_box"], [7, 2, 19, 14])
            self.assertEqual(first["trimmed_source_box"], [9, 4, 17, 12])
            self.assertEqual(len(result["icons"]), 4)
            for item in result["icons"]:
                output = root / "out" / item["file"]
                with Image.open(output) as icon:
                    self.assertEqual(icon.size, (64, 64))
                    self.assertEqual(icon.mode, "RGBA")
            self.assertTrue((root / "out" / "geometry.json").is_file())
            self.assertTrue((root / "out" / "_debug_crop_boxes.png").is_file())
            self.assertTrue((root / "out" / "_debug_background_mask.png").is_file())


if __name__ == "__main__":
    unittest.main()
