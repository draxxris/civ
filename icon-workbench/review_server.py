"""Serve a local review page for the mapped icon sheets."""

import argparse
from datetime import datetime, timezone
import json
import mimetypes
import os
from pathlib import Path
import re
import tempfile
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from threading import Lock
from urllib.parse import parse_qs, urlsplit

WORKBENCH = Path(__file__).resolve().parent
REPOSITORY = WORKBENCH.parent
MODS_ROOT = (REPOSITORY / "src/Mods").resolve()
REVISIONS_ROOT = WORKBENCH / "revisions"
OVERLAY_ROOT = REVISIONS_ROOT / "logo-overlays"
REVISION_MANIFEST_FILE = OVERLAY_ROOT / "manifest.json"
STATE_FILE = WORKBENCH / "review-selections.json"
CATALOG_SUFFIX_PATTERN = re.compile(r"^[a-z0-9][a-z0-9_-]*$")
BATCH_NAMES = ("cities-001", "cities-002", "cities-003", "cities-004")
MAX_REQUEST_BYTES = 1_000_000
STATE_LOCK = Lock()
STATIC_FILES = {
    "/": WORKBENCH / "review.html",
    "/review.html": WORKBENCH / "review.html",
    "/review.css": WORKBENCH / "review.css",
    "/review.js": WORKBENCH / "review.js",
}


class RequestError(Exception):
    def __init__(self, status, message):
        super().__init__(message)
        self.status = status
        self.message = message


def resolve_original(relative_path):
    if not isinstance(relative_path, str) or not relative_path.startswith("src/Mods/"):
        raise ValueError("Invalid original path in batch manifest")

    path = (REPOSITORY / relative_path).resolve()
    if not path.is_relative_to(MODS_ROOT) or not path.is_file():
        raise ValueError("Original path is missing or outside src/Mods")
    return path


def load_catalog():
    batches = {}
    catalog_ids = set()
    original_paths = set()

    for batch_name in BATCH_NAMES:
        batch_dir = WORKBENCH / "batches" / batch_name
        manifest_path = batch_dir / "cells.json"
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        cells = manifest.get("cells")
        if manifest.get("rows") != 12 or manifest.get("columns") != 12:
            raise ValueError(f"Unexpected grid size in {manifest_path}")
        if not isinstance(cells, list) or len(cells) != 144:
            raise ValueError(f"Expected 144 cells in {manifest_path}")

        prepared = []
        for index, cell in enumerate(cells):
            row = index // 12 + 1
            column = index % 12 + 1
            if cell.get("row") != row or cell.get("column") != column:
                raise ValueError(f"Cell order does not match {manifest_path}")

            catalog_id = cell.get("catalog_id")
            source_path = cell.get("path")
            label = cell.get("label")
            subject = cell.get("subject")
            if not isinstance(catalog_id, str) or catalog_id in catalog_ids:
                raise ValueError(f"Duplicate or invalid catalog ID in {manifest_path}")
            if not isinstance(source_path, str) or source_path in original_paths:
                raise ValueError(f"Duplicate or invalid original path in {manifest_path}")
            if not isinstance(label, str) or not isinstance(subject, str):
                raise ValueError(f"Missing label or subject in {manifest_path}")

            original_paths.add(source_path)
            catalog_ids.add(catalog_id)
            candidate = batch_dir / "review-crops" / f"r{row:02}-c{column:02}.png"
            if not candidate.is_file() or not candidate.resolve().is_relative_to(batch_dir.resolve()):
                raise ValueError(f"Review crop is missing for {catalog_id}")

            prepared.append({
                "catalog_id": catalog_id,
                "label": label,
                "subject": subject,
                "row": row,
                "column": column,
                "original_file": resolve_original(source_path),
                "candidate_file": candidate.resolve(),
            })
        batches[batch_name] = prepared

    return batches, catalog_ids


def resolve_overlay_file(relative_file, catalog_id):
    suffix = catalog_id.partition(":")[2]
    if not CATALOG_SUFFIX_PATTERN.fullmatch(suffix):
        raise ValueError("A revised candidate has an invalid catalog ID")

    expected_file = f"revisions/logo-overlays/{suffix}.png"
    if relative_file != expected_file:
        raise ValueError("A revised candidate has an invalid file path")

    workbench_root = WORKBENCH.resolve()
    revisions_root = REVISIONS_ROOT.resolve()
    overlay_root = OVERLAY_ROOT.resolve()
    if not revisions_root.is_relative_to(workbench_root):
        raise ValueError("The revisions directory is outside the workbench")
    if not overlay_root.is_relative_to(revisions_root):
        raise ValueError("The overlay directory is outside the revisions directory")

    path = (WORKBENCH / relative_file).resolve(strict=True)
    if not path.is_relative_to(overlay_root) or path.suffix.lower() != ".png" or not path.is_file():
        raise ValueError("A revised candidate is outside the overlay directory")
    with path.open("rb") as image:
        header = image.read(24)
    if (
        len(header) != 24
        or header[:8] != b"\x89PNG\r\n\x1a\n"
        or header[12:16] != b"IHDR"
        or int.from_bytes(header[16:20], "big") != 64
        or int.from_bytes(header[20:24], "big") != 64
    ):
        raise ValueError("A revised candidate must be a 64×64 PNG image")
    return path


def load_candidate_overrides(catalog_ids):
    manifest_path = REVISION_MANIFEST_FILE
    if not manifest_path.exists():
        return {}

    workbench_root = WORKBENCH.resolve()
    revisions_root = REVISIONS_ROOT.resolve()
    overlay_root = OVERLAY_ROOT.resolve()
    if not revisions_root.is_relative_to(workbench_root):
        raise ValueError("The revisions directory is outside the workbench")
    if not overlay_root.is_relative_to(revisions_root):
        raise ValueError("The overlay directory is outside the revisions directory")
    if not manifest_path.resolve().is_relative_to(overlay_root):
        raise ValueError("The revision manifest is outside the overlay directory")

    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    if not isinstance(manifest, dict) or set(manifest) != {"version", "candidates"}:
        raise ValueError("The revision manifest has an unsupported format")
    if type(manifest.get("version")) is not int or manifest["version"] != 1:
        raise ValueError("The revision manifest has an unsupported version")
    candidates = manifest.get("candidates")
    if not isinstance(candidates, dict):
        raise ValueError("The revision manifest candidates are invalid")

    overrides = {}
    for catalog_id, entry in candidates.items():
        if not isinstance(catalog_id, str) or catalog_id not in catalog_ids:
            raise ValueError("The revision manifest contains an unknown catalog ID")
        required_fields = {"file", "badge", "revision"}
        allowed_fields = required_fields | {"base_candidate", "prior_rejection_note"}
        if (
            not isinstance(entry, dict)
            or not required_fields.issubset(entry)
            or not set(entry).issubset(allowed_fields)
        ):
            raise ValueError("A revised candidate has an unsupported format")
        if "base_candidate" in entry and not isinstance(entry["base_candidate"], str):
            raise ValueError("A revised candidate has invalid base-candidate metadata")
        if "prior_rejection_note" in entry and (
            not isinstance(entry["prior_rejection_note"], str)
            or len(entry["prior_rejection_note"]) > 1000
        ):
            raise ValueError("A revised candidate has an invalid prior rejection note")
        badge = entry["badge"]
        revision = entry["revision"]
        if any(
            not isinstance(value, str) or not value.strip() or len(value) > 100
            for value in (badge, revision)
        ):
            raise ValueError("A revised candidate has invalid display metadata")
        overrides[catalog_id] = {
            "file": resolve_overlay_file(entry["file"], catalog_id),
            "badge": badge,
            "revision": revision,
        }
    return overrides


def load_state(catalog_ids):
    if not STATE_FILE.exists():
        return {
            "version": 1,
            "saved_at": None,
            "approved_catalog_ids": [],
            "rejected_catalog_notes": {},
        }
    try:
        state = json.loads(STATE_FILE.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise ValueError("Saved selections are not valid JSON") from error

    if not isinstance(state, dict):
        raise ValueError("Saved selections have an unsupported format")
    approved = state.get("approved_catalog_ids")
    rejected = state.get("rejected_catalog_notes", {})
    if state.get("version") != 1 or not isinstance(approved, list):
        raise ValueError("Saved selections have an unsupported format")
    if any(not isinstance(item, str) or item not in catalog_ids for item in approved):
        raise ValueError("Saved selections contain an unknown catalog ID")
    if len(set(approved)) != len(approved):
        raise ValueError("Saved selections contain a duplicate catalog ID")
    if not isinstance(rejected, dict):
        raise ValueError("Saved rejection notes have an unsupported format")
    if any(
        not isinstance(item, str)
        or item not in catalog_ids
        or not isinstance(note, str)
        or not note.strip()
        or len(note) > 1000
        for item, note in rejected.items()
    ):
        raise ValueError("Saved rejection notes contain invalid data")
    if set(approved) & rejected.keys():
        raise ValueError("A catalog ID cannot be approved and rejected")

    saved_at = state.get("saved_at")
    if saved_at is not None and not isinstance(saved_at, str):
        raise ValueError("Saved selections have an invalid time")
    return {
        "version": 1,
        "saved_at": saved_at,
        "approved_catalog_ids": approved,
        "rejected_catalog_notes": rejected,
    }

def write_state(state):
    encoded = (json.dumps(state, indent=2, ensure_ascii=False) + "\n").encode("utf-8")
    descriptor, temporary_name = tempfile.mkstemp(
        prefix=".review-selections-", suffix=".tmp", dir=WORKBENCH
    )
    try:
        with os.fdopen(descriptor, "wb") as output:
            output.write(encoded)
            output.flush()
            os.fsync(output.fileno())
        os.replace(temporary_name, STATE_FILE)
    except Exception:
        try:
            os.unlink(temporary_name)
        except FileNotFoundError:
            pass
        raise


def require_query(query, required_keys):
    try:
        values = parse_qs(query, keep_blank_values=True, strict_parsing=True)
    except ValueError as error:
        raise RequestError(400, "Invalid query string") from error
    if set(values) != set(required_keys) or any(len(items) != 1 for items in values.values()):
        raise RequestError(400, "Invalid query parameters")
    return {key: values[key][0] for key in required_keys}


def require_batch(batch_name, batches):
    if batch_name not in batches:
        raise RequestError(404, "Unknown batch")
    return batches[batch_name]


def read_request_json(handler):
    if handler.headers.get("Transfer-Encoding"):
        raise RequestError(400, "Chunked requests are not accepted")
    if handler.headers.get_content_type() != "application/json":
        raise RequestError(415, "Send JSON data")

    raw_length = handler.headers.get("Content-Length")
    if raw_length is None:
        raise RequestError(411, "Content-Length is required")
    try:
        length = int(raw_length)
    except ValueError as error:
        raise RequestError(400, "Invalid Content-Length") from error
    if length < 1:
        raise RequestError(400, "Request body is empty")
    if length > MAX_REQUEST_BYTES:
        raise RequestError(413, "Request body is too large")

    body = handler.rfile.read(length)
    if len(body) != length:
        raise RequestError(400, "Request body is incomplete")
    try:
        return json.loads(body)
    except (UnicodeDecodeError, json.JSONDecodeError) as error:
        raise RequestError(400, "Request body is not valid JSON") from error


def validate_approved_ids(approved, valid_ids):
    if not isinstance(approved, list) or len(approved) > len(valid_ids):
        raise RequestError(400, "Invalid approved list")
    if any(not isinstance(item, str) or item not in valid_ids for item in approved):
        raise RequestError(400, "An approval does not belong to this batch")
    if len(set(approved)) != len(approved):
        raise RequestError(400, "The approved list contains a duplicate")
    return set(approved)


def validate_rejection_notes(rejected, valid_ids):
    if not isinstance(rejected, list) or len(rejected) > len(valid_ids):
        raise RequestError(400, "Invalid rejected list")

    notes = {}
    for item in rejected:
        if not isinstance(item, dict) or set(item) != {"catalog_id", "note"}:
            raise RequestError(400, "Each rejection needs a catalog ID and note")
        catalog_id = item["catalog_id"]
        note = item["note"]
        if not isinstance(catalog_id, str) or catalog_id not in valid_ids:
            raise RequestError(400, "A rejection does not belong to this batch")
        if not isinstance(note, str) or not note.strip() or len(note) > 1000:
            raise RequestError(400, "Each rejection needs a note of 1 to 1000 characters")
        if catalog_id in notes:
            raise RequestError(400, "The rejected list contains a duplicate")
        notes[catalog_id] = note.strip()
    return notes


def validate_submission(payload, batches):
    if not isinstance(payload, dict) or set(payload) != {"batch", "approved", "rejected"}:
        raise RequestError(400, "Expected a batch, approved list, and rejected list")

    batch_name = payload.get("batch")
    if not isinstance(batch_name, str):
        raise RequestError(400, "Invalid batch")
    batch_cells = require_batch(batch_name, batches)
    valid_ids = {cell["catalog_id"] for cell in batch_cells}
    approved_ids = validate_approved_ids(payload.get("approved"), valid_ids)
    rejection_notes = validate_rejection_notes(payload.get("rejected"), valid_ids)
    if approved_ids & rejection_notes.keys():
        raise RequestError(400, "An item cannot be approved and rejected")
    if approved_ids | rejection_notes.keys() != valid_ids:
        raise RequestError(400, "Choose approval or rejection for every item")
    return batch_name, valid_ids, approved_ids, rejection_notes

def update_state(batch_ids, approved_ids, rejection_notes, catalog_ids):
    with STATE_LOCK:
        state = load_state(catalog_ids)
        retained_approved = set(state["approved_catalog_ids"]) - batch_ids
        retained_rejected = {
            catalog_id: note
            for catalog_id, note in state["rejected_catalog_notes"].items()
            if catalog_id not in batch_ids
        }
        merged_approved = sorted(retained_approved | approved_ids)
        merged_rejected = {**retained_rejected, **rejection_notes}
        saved_at = datetime.now(timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z")
        updated = {
            "version": 1,
            "saved_at": saved_at,
            "approved_catalog_ids": merged_approved,
            "rejected_catalog_notes": merged_rejected,
        }
        write_state(updated)
    return saved_at

class ReviewHandler(BaseHTTPRequestHandler):
    server_version = "IconReview/1"

    def send_headers(self, status, content_type, length):
        self.send_response(status)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(length))
        self.send_header("Cache-Control", "no-store")
        self.send_header("X-Content-Type-Options", "nosniff")
        self.send_header("Referrer-Policy", "no-referrer")
        self.send_header(
            "Content-Security-Policy",
            "default-src 'self'; img-src 'self'; style-src 'self'; "
            "script-src 'self'; connect-src 'self'; form-action 'self'; "
            "base-uri 'none'; frame-ancestors 'none'",
        )
        self.end_headers()

    def send_json(self, status, payload):
        encoded = json.dumps(payload, ensure_ascii=False).encode("utf-8")
        self.send_headers(status, "application/json; charset=utf-8", len(encoded))
        self.wfile.write(encoded)

    def send_file(self, path):
        content = path.read_bytes()
        content_type = mimetypes.guess_type(path.name)[0] or "application/octet-stream"
        if content_type.startswith("text/") or content_type == "application/javascript":
            content_type += "; charset=utf-8"
        self.send_headers(200, content_type, len(content))
        self.wfile.write(content)

    def send_request_error(self, error):
        self.send_json(error.status, {"error": error.message})

    def do_GET(self):
        parsed = urlsplit(self.path)
        try:
            if parsed.path in STATIC_FILES and not parsed.query:
                self.send_file(STATIC_FILES[parsed.path])
                return
            if parsed.path == "/api/batches":
                self.get_batches(parsed.query)
                return
            if parsed.path == "/api/review":
                self.get_review(parsed.query)
                return
            if parsed.path in ("/media/original", "/media/candidate"):
                self.get_image(parsed.path, parsed.query)
                return
            raise RequestError(404, "Not found")
        except RequestError as error:
            self.send_request_error(error)
        except (OSError, ValueError, json.JSONDecodeError):
            self.send_json(500, {"error": "The review data could not be read"})

    def get_batches(self, query):
        if query:
            raise RequestError(400, "This route does not accept query parameters")
        batches = self.server.batches
        with STATE_LOCK:
            state = load_state(self.server.catalog_ids)
        approved = set(state["approved_catalog_ids"])
        rejected = set(state["rejected_catalog_notes"])
        result = [
            {
                "name": name,
                "count": len(cells),
                "approved_count": sum(cell["catalog_id"] in approved for cell in cells),
                "rejected_count": sum(cell["catalog_id"] in rejected for cell in cells),
            }
            for name, cells in batches.items()
        ]
        self.send_json(200, {"batches": result, "saved_at": state["saved_at"]})

    def get_review(self, query):
        params = require_query(query, ("batch",))
        batch_cells = require_batch(params["batch"], self.server.batches)
        with STATE_LOCK:
            state = load_state(self.server.catalog_ids)
        approved = set(state["approved_catalog_ids"])
        rejected = state["rejected_catalog_notes"]
        overrides = load_candidate_overrides(self.server.catalog_ids)
        cells = []
        for index, cell in enumerate(batch_cells):
            catalog_id = cell["catalog_id"]
            revision_data = overrides.get(catalog_id)
            cells.append({
                "catalog_id": catalog_id,
                "label": cell["label"],
                "subject": cell["subject"],
                "row": cell["row"],
                "column": cell["column"],
                "approved": catalog_id in approved,
                "rejected": catalog_id in rejected,
                "rejection_note": rejected.get(catalog_id, ""),
                "revised_candidate": revision_data is not None,
                "revision_text": "Revised candidate" if revision_data else None,
                "revision_badge": revision_data["badge"] if revision_data else None,
                "revision": revision_data["revision"] if revision_data else None,
                "original_url": f"/media/original?batch={params['batch']}&index={index}",
                "candidate_url": f"/media/candidate?batch={params['batch']}&index={index}",
            })
        self.send_json(200, {
            "batch": params["batch"],
            "cells": cells,
            "saved_at": state["saved_at"],
            "approved_count": sum(cell["approved"] for cell in cells),
            "rejected_count": sum(cell["catalog_id"] in rejected for cell in batch_cells),
        })
    def get_image(self, route, query):
        params = require_query(query, ("batch", "index"))
        batch_cells = require_batch(params["batch"], self.server.batches)
        try:
            index = int(params["index"])
        except ValueError as error:
            raise RequestError(400, "Invalid cell index") from error
        if index < 0 or index >= len(batch_cells):
            raise RequestError(404, "Unknown cell")

        cell = batch_cells[index]
        if route == "/media/original":
            image_path = cell["original_file"]
        else:
            overrides = load_candidate_overrides(self.server.catalog_ids)
            revision = overrides.get(cell["catalog_id"])
            image_path = revision["file"] if revision else cell["candidate_file"]
        self.send_file(image_path)
    def do_POST(self):
        parsed = urlsplit(self.path)
        if parsed.path != "/api/selections" or parsed.query:
            self.send_request_error(RequestError(404, "Not found"))
            return
        try:
            payload = read_request_json(self)
            batch_name, batch_ids, approved_ids, rejection_notes = validate_submission(
                payload, self.server.batches
            )
            saved_at = update_state(
                batch_ids, approved_ids, rejection_notes, self.server.catalog_ids
            )
            self.send_json(200, {
                "saved": True,
                "batch": batch_name,
                "approved_count": len(approved_ids),
                "rejected_count": len(rejection_notes),
                "saved_at": saved_at,
            })
        except RequestError as error:
            self.send_request_error(error)
        except (OSError, ValueError, json.JSONDecodeError):
            self.send_json(500, {"error": "Selections could not be saved"})


class ReviewServer(ThreadingHTTPServer):
    allow_reuse_address = True
    daemon_threads = True

    def __init__(self, address, batches, catalog_ids):
        self.batches = batches
        self.catalog_ids = catalog_ids
        super().__init__(address, ReviewHandler)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--port", type=int, default=0, help="Port to use. Zero selects an available port.")
    args = parser.parse_args()
    if not 0 <= args.port <= 65535:
        parser.error("port must be between 0 and 65535")

    batches, catalog_ids = load_catalog()
    with ReviewServer(("0.0.0.0", args.port), batches, catalog_ids) as server:
        port = server.server_address[1]
        print(f"Listening on 0.0.0.0:{port}", flush=True)
        print(f"Open http://127.0.0.1:{port}/ in your browser.", flush=True)
        try:
            server.serve_forever()
        except KeyboardInterrupt:
            print("Review server stopped.", flush=True)


if __name__ == "__main__":
    main()
