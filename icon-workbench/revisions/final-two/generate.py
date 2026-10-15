import json
from pathlib import Path
import subprocess

root = Path(__file__).resolve().parents[2]
out = Path(__file__).resolve().parent
manifest = json.loads((root/'revisions/logo-overlays/manifest.json').read_text())
fixes = {
'burningman': 'The wooden human effigy MUST BE BURNING: vivid orange and yellow FLAMES engulf its torso, arms and legs, with a few rising embers. Clearly recognizable as one wooden human figure with exactly TWO arms raised and TWO legs, visibly ON FIRE. Not merely a straw figure, not an unlit sculpture. No people or crowds.',
'railgun_turret': 'The turret housing AND THE ENTIRE GUN BARREL must be square or rectangular in cross section. A box-shaped armored turret with a LONG RECTANGULAR BOX BEAM as its barrel. Show flat top and side planes, straight edges and a clearly RECTANGULAR hollow muzzle aperture. NOT a cylindrical tube, NOT a round muzzle, NOT a round cannon or gun. This is a futuristic electromagnetic railgun with a rectangular barrel, not an ordinary tank cannon.'
}
for suffix,delta in fixes.items():
    catalog_id='cities:'+suffix
    prompt='Make one corrected SINGLE SQUARE icon, no grid. Preserve the reference art style, subject identity, camera angle, warm painted colors, dimensional but simplified forms, charcoal-blue background, soft visible halo and thin silver beveled frame. The correction is: '+delta+' Keep the composition bold and simple enough for final64-pixel display. Avoid tiny texture details or photorealism. The subject must stay safely within the frame. No captions, letters or logos. No extra objects. Keep the square frame fully inside the canvas, with no overflow into adjacent art.'
    (out/f'{suffix}-prompt.txt').write_text(prompt)
    reference=root/manifest['candidates'][catalog_id]['file']
    subprocess.run(['python3','/home/gordon/.agents/skills/openrouter-image-generator/scripts/openrouter_images.py','--model','openai/gpt-image-2.5-flare','--quality','high','--aspect-ratio','1:1','--count','1','--no-chain','--reference',str(reference),'--output-dir',str(out),'--prefix',suffix,'--prompt',prompt],check=True)
