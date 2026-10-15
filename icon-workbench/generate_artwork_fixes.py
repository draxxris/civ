import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parent
OUT = ROOT / 'revisions/artwork-fixes-001'
OUT.mkdir(parents=True, exist_ok=True)
fixes = [
('corporation_burgerworld_hq','Modern fast-food company headquarters office, glass tower and small entrance. NO hamburger sculpture, food, signage or logo.'),
('courthouse','Large golden justice balance scale dominating the foreground, with a small courthouse silhouette behind it.'),
('egyptian_obelisk','One tall carved sandstone Egyptian obelisk alone. No buildings, settlement or landscape.'),
('global_stock_exchange','Modern trading exchange represented by a prominent connected internet globe above a rising market chart.'),
('goths_plunder','One open wooden treasure chest with bright gold coins spilling over the edge. Nothing else.'),
('great_firewall','One modern rack-mount computer server cabinet with a prominent glowing brick-wall-and-shield firewall symbol.'),
('greekfire','A single bronze brazier filled with vivid flames. No boat, no ship, no human.'),
('hollywood','The iconic white HOLLYWOOD hillside letters, clearly spelling HOLLYWOOD, tightly framed. No studio or other buildings.'),
('oil_plant','Modern oil-fired power station with two tall smokestacks emitting obvious dark pollution. No refinery towers or storage tanks.'),
('orbital_factory','Large industrial factory floating in outer space, with blocky assembly halls, cargo gantries and windows. Not a satellite.'),
('propaganda_office','Modern rectangular office building with a broadcast mast mounted directly on its flat roof. No ancient architecture.'),
('railgun_turret','Square armored modern gun turret, box-shaped housing and a long straight railgun barrel. No round turret.'),
('spaceport','Orbital space harbor already in outer space, with a ring of docking piers and small docked spacecraft. No launchpad or rocket launch.'),
('swiss_zoll','Small alpine customs post with a gate barrier and cargo crates. Absolutely no flags, crosses, emblems or national markings.'),
('tax_office','Natural-proportioned torso-up tax collector in medieval riding clothes, holding a large gold-filled moneybag; visible horse head beside shoulder.'),
('un_mission','Small modest humanitarian field outpost with canvas tents and a low prefabricated office. No skyscraper, no main headquarters.'),
('university','One large open book with a black graduation mortarboard cap resting on it. No buildings.'),
('warhead_factory','Modern industrial weapons factory: gray assembly hall with a large missile warhead on a conveyor. No ancient architecture.'),
('ad_agency','Contemporary advertising agency represented by a modern digital billboard with bold abstract colored shapes. No gallery, no classical buildings.'),
('arena','Compact square gladiatorial arena with a square sand floor enclosed by four straight stands. No oval Colosseum.'),
('arsenal','Modern military arsenal warehouse with steel doors, orderly ammunition crates and a tank barrel beside the entrance. No medieval shipyard.'),
('arthub','Futuristic art exhibition center with curved glass architecture and one luminous holographic abstract sculpture. No classical gallery.'),
('automated_defenses','Modern automated defense system: angular armored sensor tower and unmanned gun turret on a concrete base. No ancient stone fortress.'),
('barbwire_fence','Barbed wire fence stretched between THIN metal posts. Clear wire silhouette, no thick stone pillars.'),
('broadway','Close-up glowing neon theater sign reading BROADWAY in large unmistakable letters, with a few surrounding light bulbs. No building scene.'),
('burningman','One wooden human-shaped festival effigy with exactly TWO arms raised upward and exactly TWO legs. No crowd, no extra limbs.'),
('bulgarian_chitalishte','Simple close-up cultural reading-room emblem: an open book in front of a small theater curtain. No neoclassical building or tiny details.'),
('ebgatehouse','Twin-tower medieval stone gatehouse with a clearly visible broad BLUE WATER MOAT across the foreground and a raised drawbridge.'),
('cryogenic_prison','Exactly ONE upright glass cryogenic prison chamber containing one human silhouette, with icy pipes. No additional pods.'),
('highway_project','Clean plausible highway cloverleaf interchange with four smoothly connected loop ramps. No vehicles, buildings or extra disconnected roads.'),
('foundation','One simple wooden desk with a small open chest full of gold coins placed on top. No estate or other buildings.'),
]
state = json.loads((ROOT / 'review-selections.json').read_text())
lookup = {c['catalog_id']:dict(c,batch=b.name) for b in (ROOT/'batches').glob('cities-*') for c in json.loads((b/'cells.json').read_text())['cells']}
cells = []
for index,(suffix,subject) in enumerate(fixes):
    catalog_id='cities:'+suffix
    assert catalog_id in state['rejected_catalog_notes'], catalog_id
    entry=lookup[catalog_id]
    cells.append(dict(catalog_id=catalog_id,label=entry['label'],path=entry['path'],batch=entry['batch'],row=index//6+1,column=index%6+1,subject=subject,note=state['rejected_catalog_notes'][catalog_id]))
(OUT/'cells.json').write_text(json.dumps(cells,indent=2)+'\n')
prompt='''Create a NEW 6-column by 6-row square sheet of 36 separate square strategy-game icons. Use the reference ONLY for the approved ART STYLE, NOT its larger grid layout or old subjects. Exactly SIX rows and SIX columns, all identical square cells. Each cell has the same thin silver beveled square frame, charcoal-blue background, and visible soft ivory or pale cyan glow behind its subject. Match the reference mature stylized hand-painted look, simplified dimensional forms and rich warm colors. NOT photorealistic, NOT childish cartoon, no heavy black outlines. Strong silhouettes and very few details: these will be displayed at 64 pixels. All rows including the bottom must be equal height. One coherent subject per square. No merging neighboring subjects or hybrid weapons. Every person must have natural proportions and torso-up framing, not compressed full-body anatomy. Human-shaped wooden festival effigies are not people and need their stated limb counts. No logos or lettering unless the particular cell explicitly requests it. Maintain precise anatomy and plausible infrastructure. Original artwork, not copied game assets. Follow each cell instruction exactly:\n'''
for index in range(36):
    text=cells[index]['subject'] if index<len(cells) else 'Unused filler: a simple blank dark-blue tile with its silver frame, no subject.'
    prompt+=f'Row {index//6+1}, column {index%6+1}: {text}\n'
(OUT/'prompt.txt').write_text(prompt)
subprocess.run(['python3','/home/gordon/.agents/skills/openrouter-image-generator/scripts/openrouter_images.py','--model','openai/gpt-image-2.5-flare','--aspect-ratio','1:1','--quality','high','--count','1','--no-chain','--output-dir',str(OUT),'--prefix','corrections','--reference',str(ROOT/'generation/style-preview/flare-12x12-torso-v4-20261015-031843-001.png'),'--prompt',prompt],check=True)
