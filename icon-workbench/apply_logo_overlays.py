import json
from collections import deque
from pathlib import Path
from PIL import Image, ImageDraw
from logo_geometry import BADGE_OFFSETS, clean_badge, fit_badge

ROOT = Path(__file__).resolve().parent
LOGOS = ROOT / 'logos'
OUT = ROOT / 'revisions/logo-overlays'
OUT.mkdir(parents=True, exist_ok=True)
sheet = Image.open(LOGOS / 'simple-v1/simple-badges-20261015-124531-001.png').convert('RGBA')
symbols = json.loads((LOGOS / 'symbols.json').read_text())
# Checked whitespace boundaries in this generated sheet; its final rows drift
# upward. Equal divisions would clip the last row of religious symbols.
ROW_EDGES = (0, 136, 260, 376, 500, 624, 748, 858, 1024)

def transparent_tile(tile):
    pixels = tile.load()
    queue = deque([(x, y) for x in range(tile.width) for y in (0, tile.height-1)] + [(x, y) for y in range(tile.height) for x in (0, tile.width-1)])
    visited = set()
    while queue:
        x, y = queue.popleft()
        if (x, y) in visited or not (0 <= x < tile.width and 0 <= y < tile.height):
            continue
        visited.add((x, y))
        if max(pixels[x,y][:3]) > 55:
            continue
        pixels[x,y] = (0,0,0,0)
        queue.extend([(x-1,y),(x+1,y),(x,y-1),(x,y+1)])
    bounds = tile.getbbox()
    if bounds is None:
        raise ValueError('Empty badge')
    return tile.crop(bounds)

badges = {}
for i, (key, _) in enumerate(symbols):
    if key.startswith('blank'):
        continue
    row, col = divmod(i,6)
    tile = sheet.crop((col*sheet.width//6,ROW_EDGES[row],(col+1)*sheet.width//6,ROW_EDGES[row+1]))
    badge = clean_badge(transparent_tile(tile))
    badge.save(LOGOS / f'{key}.png')
    badges[key] = badge

# This established symbol replaces the incorrect generated hammer.
badges['asatru'] = clean_badge(Image.open(LOGOS / 'valknut.png').convert('RGBA'))
badges['asatru'].save(LOGOS / 'asatru.png')

rules = [
('hindu','hindu'),('asatru','asatru'),('buddhist','buddhist'),('christian','christian'),('islam','islam'),('jewish','jewish'),('shinto','shinto'),('taijitu','taoist'),
('circular "c"','c'),('sushi','sushi'),('"e" logo','ethanol'),('traffic cone','construction'),('circular "m"','mining'),('include m logo','mining'),('"a" logo','aluminum'),('diamond','diamond'),('palm tree','palm'),('beaker','beaker'),('music','music'),('bread','bread'),('anvil','anvil'),('hamburger','burger'),('bull head','bull'),('handsaw','saw'),('pen+quill','quill'),('cloth bolt','cloth'),('"ec"','ec'),('e-c logo','ec'),('horseshoe','horseshoe'),('e-w-c','ewc'),('dress','dress'),('fish logo','fish'),('glass jar','jar'),('crown','crown'),('apple','apple'),('star logo','star'),('map logo','map'),('roman pillar','pillar'),('textile','textile'),('airplane','airplane'),('goofy cow','cow'),('coffee','coffee'),('red curtain','curtain'),('paw print','paw'),('soldier','soldier'),('wonka logo','confectionery'),('briefcase','briefcase')]
state = json.loads((ROOT / 'review-selections.json').read_text())
previous_manifest = json.loads((OUT / 'manifest.json').read_text()) if (OUT / 'manifest.json').exists() else {'candidates': {}}
# Retain revised candidates even after the user approves them.
artwork_fixes = {catalog_id: item for catalog_id, item in previous_manifest['candidates'].items() if item['revision'].startswith('artwork-fix-')}
rejections = {catalog_id: item['prior_rejection_note'] for catalog_id, item in previous_manifest['candidates'].items() if catalog_id not in artwork_fixes}
rejections.update({catalog_id: note for catalog_id, note in state['rejected_catalog_notes'].items() if 'logo' in note.lower() and catalog_id not in artwork_fixes})
backing = Image.new('RGBA', (112,112), (0,0,0,0))
backing_draw = ImageDraw.Draw(backing)
backing_draw.ellipse((1,1,110,110), fill=(11,23,39,255), outline=(163,183,199,255), width=4)
backing = backing.resize((28,28), Image.Resampling.LANCZOS)
lookup = {}
for batch in sorted((ROOT/'batches').glob('cities-*')):
    for cell in json.loads((batch/'cells.json').read_text())['cells']:
        lookup[cell['catalog_id']] = (batch, cell)
manifest = {'version':1,'candidates':dict(artwork_fixes)}
contact = Image.new('RGB',(9*144,9*112),(23,29,38))
draw = ImageDraw.Draw(contact)
for catalog_id, note in rejections.items():
    if 'logo' not in note.lower():
        continue
    matches = [key for phrase,key in rules if phrase in note.lower()]
    if not matches:
        raise ValueError(f'No badge mapping: {catalog_id}: {note}')
    key = matches[0]
    batch, cell = lookup[catalog_id]
    source = batch/'review-crops'/f'r{cell["row"]:02}-c{cell["column"]:02}.png'
    base = Image.open(source).convert('RGBA')
    badge = fit_badge(badges[key], key)
    base.alpha_composite(backing,(3,3))
    dx, dy = BADGE_OFFSETS.get(key, (0, 0))
    base.alpha_composite(badge,(3+(28-badge.width)//2+dx,3+(28-badge.height)//2+dy))
    target = OUT/f'{catalog_id.split(":",1)[1]}.png'
    base.save(target)
    revision = 'logo-overlay-v5-optical-offset' if key in BADGE_OFFSETS else 'logo-overlay-v4-simple-centered-valknut'
    if key == 'asatru':
        revision = 'logo-overlay-v6-larger-valknut'
    manifest['candidates'][catalog_id] = {'file':str(target.relative_to(ROOT)),'badge':key,'revision':revision,'base_candidate':str(source.relative_to(ROOT)),'prior_rejection_note':note}
    i = len(manifest['candidates'])-1
    x,y = (i%9)*144,(i//9)*112
    contact.paste(base.resize((88,88)),(x,y))
    draw.text((x,y+90),key,fill='white')
(OUT/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
contact.save(OUT/'contact.png')
print('Revised candidates:',len(manifest['candidates']))
