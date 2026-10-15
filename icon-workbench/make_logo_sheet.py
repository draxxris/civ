import json
import subprocess
from pathlib import Path

root = Path(__file__).resolve().parent
out = root / 'logos'
out.mkdir(exist_ok=True)
symbols = [
('c','original circular C monogram'),('sushi','original circular sushi roll emblem'),('ethanol','original lowercase e monogram'),('construction','orange traffic cone'),('mining','original circular M monogram'),('aluminum','original capital A monogram'),
('diamond','faceted diamond'),('palm','palm tree'),('beaker','laboratory beaker'),('music','musical notes'),('bread','bread loaf'),('anvil','blacksmith anvil'),
('burger','hamburger'),('bull','bull head'),('saw','handsaw'),('quill','quill pen'),('cloth','rolled cloth bolt'),('ec','original EC monogram'),
('horseshoe','horseshoe'),('ewc','original EWC monogram'),('dress','dress'),('fish','fish'),('jar','glass jar'),('crown','crown'),
('apple','apple'),('star','original five-pointed star emblem'),('map','folded map'),('pillar','classical pillar'),('textile','woven fabric'),('airplane','airplane'),
('cow','original playful cow head, not a commercial mascot'),('coffee','coffee cup'),('curtain','red theater curtains'),('paw','paw print'),('soldier','helmeted soldier bust'),('confectionery','original W monogram with a small candy, not an existing brand logo'),
('briefcase','briefcase'),('hindu','traditional Hindu Om symbol, accurately formed'),('asatru','traditional Valknut symbol of three interlocking triangles'),('buddhist','traditional eight-spoked Dharma wheel'),('christian','traditional Christian Latin cross'),('islam','traditional crescent moon and five-pointed star'),
('jewish','traditional Star of David with two interlocking triangles'),('shinto','traditional Shinto torii gate'),('taoist','traditional black and white taijitu with two opposite dots'),('blank1','simple small neutral circle'),('blank2','simple small neutral circle'),('blank3','simple small neutral circle')]
(out / 'symbols.json').write_text(json.dumps(symbols,indent=2))
prompt = '''Create EXACTLY 8 ROWS and 6 COLUMNS of 48 isolated emblem badges in a square sheet. Equal cells, no dividing lines, NO square frames. Every badge centered with 18 percent empty margins. Pure solid BLACK background throughout, no gradients, no shadows outside badges. Rich painted gold and ivory symbols with colored accents and subtle dark edge outlines for visibility on blue game icons. Compact legible thick silhouettes, simple recognizable forms, no tiny details. These badges will be extracted onto transparent backgrounds and displayed at 22 pixels. Match the mature painted illustration style of the reference. Company and guild marks MUST be new original designs, not copied logos. Religious symbols must preserve their established geometry, not invented variations. Only the requested monogram letters may appear, no other text. Keep all symbols completely separate. Strict left-to-right order:\n'''
for row in range(8):
    prompt += f'Row {row+1}: ' + '; '.join(s[1] for s in symbols[row*6:(row+1)*6]) + '.\n'
(out / 'prompt.txt').write_text(prompt)
subprocess.run(['python3','/home/gordon/.agents/skills/openrouter-image-generator/scripts/openrouter_images.py','--model','openai/gpt-image-2.5-flare','--quality','high','--aspect-ratio','1:1','--count','1','--no-chain','--reference',str(root/'generation/style-preview/flare-12x12-torso-v4-20261015-031843-001.png'),'--output-dir',str(out),'--prefix','badge-sheet','--prompt',prompt],check=True)
