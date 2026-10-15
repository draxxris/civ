import hashlib
import json
from pathlib import Path
import shutil
import sys

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))
sys.path.insert(0, str(ROOT / 'extraction'))
from extraction.extract_icons import extract_sheet

OUT = Path(__file__).resolve().parent
TARGET = ROOT / 'revisions/logo-overlays'
manifest_path = TARGET / 'manifest.json'
state_path = ROOT / 'review-selections.json'
state_bytes = state_path.read_bytes()
state = json.loads(state_bytes)
manifest = json.loads(manifest_path.read_text())
old_hashes = {key: hashlib.sha256((ROOT / entry['file']).read_bytes()).hexdigest()
              for key, entry in manifest['candidates'].items()}
for suffix in ('burningman', 'railgun_turret'):
    catalog_id = 'cities:' + suffix
    assert catalog_id in state['rejected_catalog_notes']
    source = next(OUT.glob(suffix + '-*.png'))
    extraction = OUT / (suffix + '-extracted')
    # Single-square source images avoid introducing grid distortion here.
    # Use the shared trim/resize path with no trimming of their intact frames.
    extract_sheet(source, extraction, rows=1, cols=1, crop_size=1024,
                  cut_edges=0, search_radius=0)
    output = TARGET / (suffix + '.png')
    shutil.copyfile(extraction / 'icons/icon_r00_c00.png', output)
    manifest['candidates'][catalog_id] = {
        'file': str(output.relative_to(ROOT)),
        'badge': 'Artwork correction',
        'revision': 'artwork-fix-v2',
        'base_candidate': str(source.relative_to(ROOT)),
        'prior_rejection_note': state['rejected_catalog_notes'][catalog_id],
    }
manifest_path.write_text(json.dumps(manifest, indent=2) + '\n')
assert state_path.read_bytes() == state_bytes
changed = [key for key, entry in manifest['candidates'].items()
           if hashlib.sha256((ROOT / entry['file']).read_bytes()).hexdigest() != old_hashes[key]]
assert set(changed) == {'cities:burningman', 'cities:railgun_turret'}
(OUT / 'review.json').write_text(json.dumps({
    'changed_candidates': changed,
    'inspection': 'Visible flames and exactly two arms on the effigy; rectangular railgun housing, barrel and muzzle.',
    'approved_candidates_unchanged': True,
    'saved_decisions_unchanged': True,
    'extraction': 'Shared splitter helper, 1x1 source, full square crop, no border trimming required.',
}, indent=2) + '\n')
print('Published only the two rejected candidates. Saved decisions unchanged.')
