"""Publish checked artwork corrections as review-only candidates."""
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageDraw
from logo_geometry import fit_badge

ROOT = Path(__file__).resolve().parent
SOURCE = ROOT / 'revisions/artwork-fixes-001'
TARGET = ROOT / 'revisions/logo-overlays'
state_path = ROOT / 'review-selections.json'
state_bytes = state_path.read_bytes()
state = json.loads(state_bytes)
manifest_path = TARGET / 'manifest.json'
manifest = json.loads(manifest_path.read_text())
cells = json.loads((SOURCE / 'cells.json').read_text())
sheet = Image.open(next(SOURCE.glob('corrections-*.png'))).convert('RGBA')
backup = SOURCE / 'previous-review-manifest.json'
if not backup.exists():
    backup.write_bytes(manifest_path.read_bytes())
old_approved = {catalog_id: hashlib.sha256((ROOT / item['file']).read_bytes()).hexdigest()
                for catalog_id, item in manifest['candidates'].items()
                if catalog_id in state['approved_catalog_ids']}
backing = Image.new('RGBA', (112,112))
draw = ImageDraw.Draw(backing)
draw.ellipse((1,1,110,110),fill=(11,23,39,255),outline=(163,183,199,255),width=4)
backing = backing.resize((28,28),Image.Resampling.LANCZOS)
for cell in cells:
    catalog_id = cell['catalog_id']
    if catalog_id not in state['rejected_catalog_notes']:
        raise ValueError(f'Not a currently rejected candidate: {catalog_id}')
    row, column = cell['row'] - 1, cell['column'] - 1
    box = (column*sheet.width//6,row*sheet.height//6,(column+1)*sheet.width//6,(row+1)*sheet.height//6)
    candidate = sheet.crop(box).resize((64,64),Image.Resampling.LANCZOS)
    suffix = catalog_id.split(':',1)[1]
    (SOURCE / 'crops').mkdir(exist_ok=True)
    base = SOURCE / 'crops' / f'{suffix}.png'
    candidate.save(base)
    # Retain exactly one hamburger: the approved identifying overlay, not a
    # giant hamburger sculpture on the headquarters building.
    if suffix == 'corporation_burgerworld_hq':
        badge = fit_badge(Image.open(ROOT/'logos/burger.png').convert('RGBA'),'burger')
        candidate.alpha_composite(backing,(3,3))
        candidate.alpha_composite(badge,(3+(28-badge.width)//2,3+(28-badge.height)//2))
    output = TARGET / f'{suffix}.png'
    candidate.save(output)
    manifest['candidates'][catalog_id] = {
        'file':str(output.relative_to(ROOT)),
        'badge':'Artwork correction',
        'revision':'artwork-fix-v1',
        'base_candidate':str(base.relative_to(ROOT)),
        'prior_rejection_note':state['rejected_catalog_notes'][catalog_id],
    }
manifest_path.write_text(json.dumps(manifest,indent=2)+'\n')
assert state_path.read_bytes() == state_bytes
for catalog_id, old_hash in old_approved.items():
    assert hashlib.sha256((ROOT/manifest['candidates'][catalog_id]['file']).read_bytes()).hexdigest()==old_hash
(SOURCE/'review.json').write_text(json.dumps({
    'status':'ready_for_user_review',
    'candidate_count':len(cells),
    'observed_rows':6,'observed_columns':6,
    'inspection':'All 31 corrected subjects inspected in the source sheet before publication. Five filler cells excluded.',
    'saved_decisions_unchanged':True,'approved_candidates_unchanged':True,
},indent=2)+'\n')
print(f'Published {len(cells)} corrected candidates. Approved candidates and saved decisions unchanged.')
