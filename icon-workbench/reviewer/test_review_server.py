"""Offline behavior tests for generation, exact crops, and HTTP review."""

import copy

import json
from pathlib import Path
import sys
import tempfile
import threading
import unittest
from unittest.mock import patch
from urllib.error import HTTPError
from urllib.request import Request, urlopen

from PIL import Image, ImageDraw

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from reviewer import prepare_review, review_server
import generate_batch


class WorkbenchTestCase(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory(prefix='icon-workflow-')
        self.addCleanup(temporary.cleanup)
        self.repo = Path(temporary.name)
        self.root = self.repo / 'icon-workbench'
        self.root.mkdir()
        self.mods = self.repo / 'src/Mods'
        self.mods.mkdir(parents=True)
        for module, values in (
            (prepare_review, {'WORKBENCH': self.root, 'REPOSITORY': self.repo, 'MODS_ROOT': self.mods}),
            (generate_batch, {'ROOT': self.root}),
            (review_server, {'WORKBENCH': self.root,
                             'STATE_FILE': self.root / review_server.STATE_FILE.name}),
        ):
            for key, value in values.items():
                patcher = patch.object(module, key, value)
                patcher.start()
                self.addCleanup(patcher.stop)
        self.entries = []
        for index in range(65):
            relative = f'src/Mods/Cities/art/icons/icon-{index}.png'
            original = self.repo / relative
            original.parent.mkdir(parents=True, exist_ok=True)
            Image.new('RGB', (64, 64), (index, 30, 40)).save(original)
            self.entries.append({
                'catalog_id': f'cities:icon-{index}', 'path': relative,
                'label': f'Institution {index}', 'subject': f'Stone institution number {index}',
                'original_subject': f'Original institution {index}',
                'rejection_reasons': ['Use a clearer silhouette'], 'badge': 'future-badge',
            })
        self.subjects = self.root / 'subjects.json'
        self.subjects.write_text(json.dumps({'version': 1, 'cells': self.entries}))

    def write_layout(self):
        layout = self.root / 'layout'
        layout.mkdir()
        source = Image.new('RGBA', (816, 816), (255, 0, 255, 255))
        mask = Image.new('RGBA', (816, 816), (255, 255, 255, 255))
        for index in range(64):
            left, top = 13 + index % 8 * 100, 13 + index // 8 * 100
            box = (left, top, left + 89, top + 89)
            ImageDraw.Draw(source).rectangle(box, fill=(18, 31, 48, 255))
            ImageDraw.Draw(mask).rectangle(box, fill=(0, 0, 0, 0))
        source.save(layout / 'blank-layout.png')
        mask.save(layout / 'mask.png')

    def write_batch(self, name, entries):
        batch = self.root / 'batches' / name
        batch.mkdir(parents=True)
        cells = []
        sheet = Image.new('RGB', (816, 816), '#ff00ff')
        draw = ImageDraw.Draw(sheet)
        for index, entry in enumerate(entries):
            left, top = 13 + index % 8 * 100, 13 + index // 8 * 100
            cells.append(dict(entry, row=index // 8 + 1, column=index % 8 + 1,
                              edit_bounds=[left, top, left + 90, top + 90]))
            draw.rectangle((left, top, left + 89, top + 89), fill=(20 + index, 60, 90))
            draw.rectangle((left + 45, top, left + 89, top + 44), fill=(90, 120, 150))
            draw.rectangle((left, top, left + 89, top), fill='#ff00ff')
            draw.rectangle((left, top + 89, left + 89, top + 89), fill='#ff00ff')
            draw.rectangle((left, top, left, top + 89), fill='#ff00ff')
            draw.rectangle((left + 89, top, left + 89, top + 89), fill='#ff00ff')
        sheet.save(batch / 'render.png')
        # A stale filename must not determine the sheet that gets cropped.
        Image.new('RGB', (816, 816), 'red').save(batch / 'sheet-stale.png')
        manifest = {
            'rows': 8, 'columns': 8, 'sheet_file': 'render.png',
            'geometry': {'size': 816, 'icon_size': 90, 'gutter': 10,
                         'outer_margin': 13},
            'cells': cells,
        }
        (batch / 'cells.json').write_text(json.dumps(manifest))
        return batch, manifest


class GenerationTests(WorkbenchTestCase):
    def run_cli(self, *args):
        with patch.object(sys, 'argv', ['generate_batch.py', str(self.subjects), *args]):
            generate_batch.main()

    def test_dry_run_selects_offset_preserves_history_and_leaves_partial_slots_blank(self):
        self.write_layout()
        with patch.object(generate_batch.subprocess, 'run', side_effect=AssertionError('Paid call')):
            self.run_cli('tail', '--start', '64', '--count', '64', '--dry-run')
        batch = self.root / 'batches/tail'
        manifest = json.loads((batch / 'cells.json').read_text())
        cell, = manifest['cells']
        self.assertEqual(cell['catalog_id'], 'cities:icon-64')
        self.assertEqual(cell['original_subject'], 'Original institution 64')
        self.assertEqual(cell['rejection_reasons'], ['Use a clearer silhouette'])
        self.assertEqual(cell['badge'], 'future-badge')
        self.assertEqual(cell['edit_bounds'], [13, 13, 103, 103])
        self.assertEqual((manifest['rows'], manifest['columns'], manifest['filler_slots']), (8, 8, 63))
        self.assertNotIn('sheet_file', manifest)
        prompt = (batch / 'prompt.txt').read_text()
        self.assertIn('Stone institution number 64', prompt)
        self.assertNotIn('Stone institution number 63', prompt)
        self.assertNotIn('future-badge', prompt)
        self.assertEqual(prompt.count('UNUSED SLOT'), 63)
        self.assertEqual(prepare_review.discover_batch_names(), [])

    def test_direct_edit_records_actual_cli_output_without_contacting_provider(self):
        self.write_layout()
        # Stand in for the external skill CLI, not for the workflow under test.
        offline_cli = self.root / 'offline_cli.py'
        offline_cli.write_text('''import json, sys
from pathlib import Path
from PIL import Image
args = sys.argv[1:]
out = Path(args[args.index('--output-dir') + 1])
Image.new('RGB', (816, 816), '#123456').save(out / 'actual-output.png')
(out / 'manifest.jsonl').write_text(json.dumps({'output': 'actual-output.png'}) + '\\n')
(out / 'receipt.json').write_text(json.dumps(args))
''')
        with patch.object(generate_batch, 'CLI', offline_cli):
            self.run_cli('new-sheet', '--start', '0', '--count', '64')
        batch = self.root / 'batches/new-sheet'
        metadata = json.loads((batch / 'cells.json').read_text())
        self.assertEqual(metadata['sheet_file'], 'actual-output.png')
        self.assertEqual(metadata['status'], 'awaiting_review')
        self.assertEqual(metadata['cells'][-1]['edit_bounds'], [713, 713, 803, 803])
        args = json.loads((batch / 'receipt.json').read_text())

        # --no-chain has no value, so inspect the value-bearing flags individually.
        for flag, expected in {
            '--mode': 'edit', '--model': 'gpt-image-2.5-flare', '--size': '816x816',
            '--quality': 'low', '--count': '1',
            '--image': str(self.root / 'layout/blank-layout.png'),
            '--mask': str(self.root / 'layout/mask.png'),
        }.items():
            self.assertEqual(args[args.index(flag) + 1], expected)
        self.assertNotIn('--reference', args)
        prepare_review.main()
        self.assertEqual(len(list((batch / 'review-crops').glob('*.png'))), 64)

    def test_invalid_selection_names_and_artwork_reference_never_create_batch(self):
        self.write_layout()
        for extra in (['--start', '-1'], ['--start', '65'], ['--count', '0'], ['--count', '65']):
            with self.subTest(extra=extra), self.assertRaises(ValueError):
                self.run_cli('invalid', *extra, '--dry-run')
        for name in ('.', '..', '../escape', 'nested/batch'):
            with self.subTest(name=name), self.assertRaises(ValueError):
                self.run_cli(name, '--dry-run')
        source = self.root / 'layout/blank-layout.png'
        with Image.open(source) as image:
            changed = image.copy()
        changed.putpixel((30, 30), (255, 0, 0, 255))
        changed.save(source)
        with self.assertRaisesRegex(ValueError, 'canonical'):
            self.run_cli('artwork-reference', '--dry-run')
        self.assertFalse((self.root / 'batches').exists())


class CropTests(WorkbenchTestCase):
    def test_one_pixel_inset_removes_edge_bleed_across_all_eight_rows(self):
        batch, manifest = self.write_batch('eight', self.entries[:64])
        prepare_review.create_crops('eight', batch, batch / 'render.png', manifest['cells'])
        for index in range(64):
            crop = batch / f'review-crops/r{index // 8 + 1:02}-c{index % 8 + 1:02}.png'
            with Image.open(crop) as image:
                self.assertEqual(image.size, (64, 64))
                self.assertNotEqual(image.getpixel((0, 0)), (255, 0, 255))
                self.assertNotEqual(image.getpixel((63, 0)), (255, 0, 255))
                self.assertNotEqual(image.getpixel((0, 63)), (255, 0, 255))
                self.assertNotEqual(image.getpixel((63, 63)), (255, 0, 255))
                self.assertEqual(image.getpixel((0, 0)), (20 + index, 60, 90))
                self.assertEqual(image.getpixel((63, 0)), (90, 120, 150))
                self.assertEqual(image.getpixel((63, 63)), (20 + index, 60, 90))
                self.assertEqual(image.getpixel((16, 16)), (20 + index, 60, 90))
                self.assertEqual(image.getpixel((48, 16)), (90, 120, 150))
        geometry = json.loads((batch / 'review-crops/geometry.json').read_text())
        self.assertEqual(geometry['source_sheet'], 'render.png')

    def test_manifest_and_symlink_escapes_fail_before_cropping(self):
        batch, manifest = self.write_batch('unsafe', self.entries[:2])
        variants = []
        duplicate = copy.deepcopy(manifest)
        duplicate['cells'][1]['catalog_id'] = duplicate['cells'][0]['catalog_id']
        variants.append(duplicate)
        outside = copy.deepcopy(manifest)
        outside['cells'][0]['path'] = 'src/Mods/../../../outside.png'
        variants.append(outside)
        bounds = copy.deepcopy(manifest)
        bounds['cells'][0]['edit_bounds'] = [0, 0, 90, 90]
        variants.append(bounds)
        sheet_escape = copy.deepcopy(manifest)
        sheet_escape['sheet_file'] = '../outside.png'
        variants.append(sheet_escape)
        (self.root / 'batches/outside.png').write_bytes((batch / 'render.png').read_bytes())
        for variant in variants:
            (batch / 'cells.json').write_text(json.dumps(variant))
            with self.subTest(variant=variant), self.assertRaises(ValueError):
                prepare_review.main()
        (batch / 'cells.json').write_text(json.dumps(manifest))
        external = self.repo / 'external-crops'
        external.mkdir()
        (batch / 'review-crops').symlink_to(external, target_is_directory=True)
        with self.assertRaises(ValueError):
            prepare_review.main()
        self.assertEqual(list(external.iterdir()), [])


class ReviewHttpTests(WorkbenchTestCase):
    def setUp(self):
        super().setUp()
        self.batch, _ = self.write_batch('fresh 8x8', self.entries[:64])
        self.write_batch('second', self.entries[64:])
        legacy = self.root / 'batches/legacy'
        legacy.mkdir()
        (legacy / 'cells.json').write_text(json.dumps({'rows': 12, 'columns': 12, 'cells': []}))
        nested = self.batch / 'nested-experiment'
        nested.mkdir()
        (nested / 'cells.json').write_text('{"rows": 8, "columns": 8}')
        self.historical = self.root / 'review-selections.json'
        self.historical.write_text(json.dumps({
            'version': 1, 'approved_catalog_ids': ['cities:icon-0'],
            'rejected_catalog_notes': {'cities:icon-1': 'Historical rejection'},
        }))
        self.history_bytes = self.historical.read_bytes()
        overlay = self.root / 'revisions/logo-overlays'
        overlay.mkdir(parents=True)
        Image.new('RGB', (64, 64), 'red').save(overlay / 'icon-0.png')
        (overlay / 'manifest.json').write_text(json.dumps({'version': 1, 'candidates': {
            'cities:icon-0': {'file': 'revisions/logo-overlays/icon-0.png',
                              'badge': 'old badge', 'revision': 'old revision'},
        }}))
        prepare_review.main()
        batches, ids = review_server.load_catalog()
        self.server = review_server.ReviewServer(('127.0.0.1', 0), batches, ids)
        self.thread = threading.Thread(target=self.server.serve_forever, daemon=True)
        self.thread.start()
        self.addCleanup(self.stop_server)
        self.url = f'http://127.0.0.1:{self.server.server_address[1]}'

    def stop_server(self):
        self.server.shutdown()
        self.server.server_close()
        self.thread.join(timeout=2)

    def request(self, path, payload=None):
        data = None if payload is None else json.dumps(payload).encode()
        request = Request(self.url + path, data=data, headers={'Content-Type': 'application/json'})
        try:
            with urlopen(request, timeout=5) as response:
                return response.status, response.read(), response.headers
        except HTTPError as error:
            with error:
                return error.code, error.read(), error.headers

    def submission(self):
        return {'batch': 'fresh 8x8', 'approved': [e['catalog_id'] for e in self.entries[:63]],
                'rejected': [{'catalog_id': 'cities:icon-63', 'note': '  Too much detail  '}]}

    def test_fresh_review_ignores_history_and_serves_exact_new_crop(self):
        status, body, _ = self.request('/api/batches')
        self.assertEqual(status, 200)
        self.assertEqual([(b['name'], b['count']) for b in json.loads(body)['batches']],
                         [('fresh 8x8', 64), ('second', 1)])
        status, body, _ = self.request('/api/review?batch=fresh%208x8')
        self.assertEqual(status, 200)
        review = json.loads(body)
        self.assertEqual((review['approved_count'], review['rejected_count']), (0, 0))
        cell = review['cells'][0]
        status, image_bytes, headers = self.request(cell['candidate_url'])
        self.assertEqual(status, 200)
        self.assertEqual(image_bytes, (self.batch / 'review-crops/r01-c01.png').read_bytes())
        self.assertEqual(headers['X-Content-Type-Options'], 'nosniff')
        self.assertIn("default-src 'self'", headers['Content-Security-Policy'])
        status, original_bytes, _ = self.request(cell['original_url'])
        self.assertEqual(status, 200)
        self.assertEqual(original_bytes, (self.repo / self.entries[0]['path']).read_bytes())
        self.assertEqual(self.historical.read_bytes(), self.history_bytes)
        self.assertFalse(review_server.STATE_FILE.exists())

    def test_save_rejection_notes_round_trips_and_preserves_other_batch(self):
        second = {'batch': 'second', 'approved': ['cities:icon-64'], 'rejected': []}
        self.assertEqual(self.request('/api/selections', second)[0], 200)
        status, body, _ = self.request('/api/selections', self.submission())
        self.assertEqual(status, 200)
        self.assertEqual(json.loads(body)['rejected_count'], 1)
        state = json.loads(review_server.STATE_FILE.read_text())
        self.assertIn('cities:icon-64', state['approved_catalog_ids'])
        self.assertEqual(state['rejected_catalog_notes'], {'cities:icon-63': 'Too much detail'})
        status, body, _ = self.request('/api/review?batch=fresh%208x8')
        self.assertEqual(status, 200)
        cell = json.loads(body)['cells'][-1]
        self.assertTrue(cell['rejected'])
        self.assertEqual(cell['rejection_note'], 'Too much detail')
        self.assertEqual(self.historical.read_bytes(), self.history_bytes)

    def test_invalid_http_submissions_and_media_queries_do_not_change_state(self):
        self.assertEqual(self.request('/api/selections', self.submission())[0], 200)
        before = review_server.STATE_FILE.read_bytes()
        cases = []
        for note in ('', '   ', 'x' * 1001, 42):
            payload = self.submission()
            payload['rejected'][0]['note'] = note
            cases.append(payload)
        for approved in (['cities:icon-64'], ['cities:icon-0'] * 64, [], ['cities:icon-63']):
            payload = self.submission()
            payload['approved'] = approved
            cases.append(payload)
        for payload in cases:
            with self.subTest(payload=payload):
                self.assertEqual(self.request('/api/selections', payload)[0], 400)
        for query, expected in (
            ('batch=../outside&index=0', 404), ('batch=fresh%208x8&index=-1', 404),
            ('batch=fresh%208x8&index=64', 404), ('batch=second&index=no', 400),
            ('batch=second&batch=second&index=0', 400),
        ):
            with self.subTest(query=query):
                self.assertEqual(self.request('/media/candidate?' + query)[0], expected)
        self.assertEqual(review_server.STATE_FILE.read_bytes(), before)

    def test_invalid_saved_state_is_not_silently_replaced(self):
        invalid_states = [
            {'version': 1, 'approved_catalog_ids': ['unknown'], 'rejected_catalog_notes': {}},
            {'version': 1, 'approved_catalog_ids': ['cities:icon-0'] * 2, 'rejected_catalog_notes': {}},
            {'version': 1, 'approved_catalog_ids': ['cities:icon-0'],
             'rejected_catalog_notes': {'cities:icon-0': 'Conflict'}},
            {'version': 1, 'approved_catalog_ids': [], 'rejected_catalog_notes': {'cities:icon-0': ''}},
        ]
        for state in invalid_states:
            review_server.STATE_FILE.write_text(json.dumps(state))
            before = review_server.STATE_FILE.read_bytes()
            with self.subTest(state=state):
                self.assertEqual(self.request('/api/batches')[0], 500)
                self.assertEqual(self.request('/api/selections', self.submission())[0], 500)
                self.assertEqual(review_server.STATE_FILE.read_bytes(), before)


if __name__ == '__main__':
    unittest.main()
