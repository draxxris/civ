import json
from pathlib import Path
import sys
import tempfile
import threading
import unittest
from urllib.error import HTTPError
from urllib.request import urlopen

WORKBENCH_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(WORKBENCH_DIR))
import review_server


class ReviewOverlayTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.batches, cls.catalog_ids = review_server.load_catalog()
        cls.batch_name = "cities-001"
        cls.cell = cls.batches[cls.batch_name][0]
        cls.catalog_id = cls.cell["catalog_id"]
        cls.suffix = cls.catalog_id.partition(":")[2]

    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="review-overlay-test-", dir=WORKBENCH_DIR)
        self.addCleanup(self.temporary.cleanup)
        self.temp_root = Path(self.temporary.name)
        self.original_paths = {
            name: getattr(review_server, name)
            for name in ("WORKBENCH", "REVISIONS_ROOT", "OVERLAY_ROOT", "REVISION_MANIFEST_FILE", "STATE_FILE")
        }
        self.addCleanup(self.restore_paths)
        review_server.WORKBENCH = self.temp_root
        review_server.REVISIONS_ROOT = self.temp_root / "revisions"
        review_server.OVERLAY_ROOT = review_server.REVISIONS_ROOT / "logo-overlays"
        review_server.REVISION_MANIFEST_FILE = review_server.OVERLAY_ROOT / "manifest.json"
        review_server.STATE_FILE = self.temp_root / "review-selections.json"
        review_server.OVERLAY_ROOT.mkdir(parents=True)

        self.saved_state = {
            "version": 1,
            "saved_at": "2026-01-01T00:00:00Z",
            "approved_catalog_ids": [],
            "rejected_catalog_notes": {self.catalog_id: "Prior rejection note."},
        }
        review_server.STATE_FILE.write_text(json.dumps(self.saved_state), encoding="utf-8")
        self.saved_state_bytes = review_server.STATE_FILE.read_bytes()

        self.server = review_server.ReviewServer(
            ("127.0.0.1", 0), self.batches, self.catalog_ids
        )
        self.thread = threading.Thread(target=self.server.serve_forever, daemon=True)
        self.thread.start()
        self.addCleanup(self.stop_server)
        self.base_url = f"http://127.0.0.1:{self.server.server_address[1]}"

    def restore_paths(self):
        for name, path in self.original_paths.items():
            setattr(review_server, name, path)

    def stop_server(self):
        self.server.shutdown()
        self.server.server_close()
        self.thread.join(timeout=2)

    def request(self, path):
        try:
            with urlopen(self.base_url + path) as response:
                return response.status, response.read()
        except HTTPError as error:
            try:
                return error.code, error.read()
            finally:
                error.close()
    def write_manifest(self, file_value=None, catalog_id=None):
        overlay_relative = f"revisions/logo-overlays/{self.suffix}.png"
        overlay_path = self.temp_root / overlay_relative
        overlay_path.parent.mkdir(parents=True, exist_ok=True)
        overlay_path.write_bytes(self.cell["candidate_file"].read_bytes())
        entry = {
            "file": file_value or overlay_relative,
            "badge": "badge-key",
            "revision": "logo-overlay-v1",
        }
        manifest = {
            "version": 1,
            "candidates": {catalog_id or self.catalog_id: entry},
        }
        review_server.REVISION_MANIFEST_FILE.write_text(json.dumps(manifest), encoding="utf-8")
        return overlay_path

    def review_cell(self):
        status, body = self.request(f"/api/review?batch={self.batch_name}")
        self.assertEqual(status, 200)
        payload = json.loads(body)
        return next(cell for cell in payload["cells"] if cell["catalog_id"] == self.catalog_id)

    def test_optional_overlay_changes_candidate_only_and_keeps_rejection_state(self):
        overlay_path = self.write_manifest()
        before = self.cell["candidate_file"].read_bytes()
        revised = self.review_cell()

        self.assertTrue(revised["revised_candidate"])
        self.assertEqual(revised["revision_text"], "Revised candidate")
        self.assertEqual(revised["revision_badge"], "badge-key")
        self.assertEqual(revised["revision"], "logo-overlay-v1")
        self.assertFalse(revised["approved"])
        self.assertTrue(revised["rejected"])
        self.assertEqual(revised["rejection_note"], "Prior rejection note.")

        candidate_status, candidate_image = self.request(revised["candidate_url"])
        original_status, original_image = self.request(revised["original_url"])
        self.assertEqual(candidate_status, 200)
        self.assertEqual(candidate_image, overlay_path.read_bytes())
        self.assertEqual(original_status, 200)
        self.assertEqual(original_image, self.cell["original_file"].read_bytes())
        self.assertEqual(self.cell["candidate_file"].read_bytes(), before)
        self.assertEqual(review_server.STATE_FILE.read_bytes(), self.saved_state_bytes)

    def test_missing_manifest_keeps_base_candidate_available(self):
        revised = self.review_cell()

        self.assertFalse(revised["revised_candidate"])
        self.assertIsNone(revised["revision_text"])
        status, candidate_image = self.request(revised["candidate_url"])
        self.assertEqual(status, 200)
        self.assertEqual(candidate_image, self.cell["candidate_file"].read_bytes())
        self.assertEqual(review_server.STATE_FILE.read_bytes(), self.saved_state_bytes)

    def test_manifest_path_outside_expected_overlay_is_rejected(self):
        escaped_file = "revisions/logo-overlays/../outside.png"
        (self.temp_root / "revisions" / "outside.png").write_bytes(b"not an overlay")
        self.write_manifest(file_value=escaped_file)

        status, _ = self.request(f"/api/review?batch={self.batch_name}")

        self.assertEqual(status, 500)
        self.assertEqual(review_server.STATE_FILE.read_bytes(), self.saved_state_bytes)


if __name__ == "__main__":
    unittest.main()
