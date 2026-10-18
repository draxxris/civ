"""Serve a local review page for the mapped icon sheets."""

import argparse
from datetime import datetime, timezone
import json
import mimetypes
import os
from pathlib import Path

import tempfile
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from threading import Lock
from urllib.parse import parse_qs, urlencode, urlsplit

from PIL import Image

if __package__:
    from . import prepare_review
else:
    import prepare_review
REVIEWER = Path(__file__).resolve().parent
WORKBENCH = REVIEWER.parent
STATE_FILE = REVIEWER / "review-selections-8x8.json"
MAX_REQUEST_BYTES = 1_000_000
STATE_LOCK = Lock()
STATIC_FILES = {
    "/": REVIEWER / "review.html",
    "/review.html": REVIEWER / "review.html",
    "/review.css": REVIEWER / "review.css",
    "/review.js": REVIEWER / "review.js",
}


class RequestError(Exception):
    def __init__(self, status, message):
        super().__init__(message)
        self.status = status
        self.message = message


def prepare_cell(batch_dir, cell):
    row, column = cell["row"], cell["column"]
    candidate = batch_dir / "review-crops" / f"r{row:02}-c{column:02}.png"
    if not candidate.is_file() or not candidate.resolve().is_relative_to(batch_dir.resolve()):
        raise ValueError(f"Review crop is missing or outside the batch: {cell['catalog_id']}")
    with Image.open(candidate) as image:
        if image.format != "PNG" or image.size != (64, 64):
            raise ValueError("A review crop must be a 64x64 PNG")
    return {
        "catalog_id": cell["catalog_id"], "label": cell["label"],
        "subject": cell["subject"], "row": row, "column": column,
        "original_file": prepare_review.resolve_original(cell["path"]),
        "candidate_file": candidate.resolve(),
    }


def load_catalog():
    batches = {}
    catalog_ids, original_paths = set(), set()
    for name in prepare_review.discover_batch_names():
        batch_dir, _, cells = prepare_review.load_batch(name, catalog_ids, original_paths)
        batches[name] = [prepare_cell(batch_dir, cell) for cell in cells]
    return batches, catalog_ids

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
        cells = []
        for index, cell in enumerate(batch_cells):
            catalog_id = cell["catalog_id"]

            cells.append({
                "catalog_id": catalog_id,
                "label": cell["label"],
                "subject": cell["subject"],
                "row": cell["row"],
                "column": cell["column"],
                "approved": catalog_id in approved,
                "rejected": catalog_id in rejected,
                "rejection_note": rejected.get(catalog_id, ""),
                "original_url": "/media/original?" + urlencode({"batch": params["batch"], "index": index}),
                "candidate_url": "/media/candidate?" + urlencode({"batch": params["batch"], "index": index}),
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
            image_path = cell["candidate_file"]
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
