"""Generate a catalog-mapped preview. This command never installs game assets."""

import argparse
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parent
CLI = Path('/home/gordon/.agents/skills/openrouter-image-generator/scripts/openrouter_images.py')
REFERENCE = ROOT / 'generation/style-preview/flare-12x12-torso-v4-20261015-031843-001.png'
TEMPLATE = ROOT / 'generation/style-preview/template-12x12.png'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('subjects', type=Path)
    parser.add_argument('batch', help='New batch directory name inside icon-workbench/batches')
    args = parser.parse_args()
    entries = json.loads(args.subjects.read_text())
    if len(entries) != 144:
        raise ValueError('A batch requires exactly 144 subjects')
    if len({entry['path'] for entry in entries}) != 144:
        raise ValueError('Target paths must be unique')
    repo = ROOT.parent
    for entry in entries:
        target = (repo / entry['path']).resolve()
        if not target.is_relative_to(repo / 'src/Mods') or not target.is_file():
            raise ValueError(f'Invalid target path: {entry["path"]}')
    batch = ROOT / 'batches' / args.batch
    if Path(args.batch).name != args.batch:
        raise ValueError('The batch name must be a single directory name')
    batch.mkdir(parents=True, exist_ok=False)
    cells = [dict(entry, row=index // 12 + 1, column=index % 12 + 1,
                  review_status='pending', installed=False)
             for index, entry in enumerate(entries)]
    metadata = {
        'model': 'openai/gpt-image-2.5-flare',
        'rows': 12, 'columns': 12,
        'style_reference': str(REFERENCE.relative_to(ROOT)),
        'layout_reference': str(TEMPLATE.relative_to(ROOT)),
        'status': 'awaiting_generation_and_review',
        'cells': cells,
    }
    (batch / 'cells.json').write_text(json.dumps(metadata, indent=2) + '\n')
    prompt = '''Create a NEW sheet of original replacement icons. Image 1 is the mandatory layout template. Image 2 is the APPROVED ART STYLE reference only, not the new subject list. Replace its sample subjects with the exact new subjects below.
Layout: exactly TWELVE columns and TWELVE rows, 144 identical SQUARE cells. Preserve all boundaries of image 1. Keep ALL row heights equal, including the bottom four rows. No perspective, no squashed bottom rows, no missing cells, no merged cells. Fill all 144 cells, in the given order. Keep breathing room inside every frame.
Match image 2 exactly in art direction: mature stylized hand-painted strategy illustrations, simplified dimensional forms, warm rich palette, softly brushed shading, not realistic photography or detailed 3D renders, not childish cartoons, no thick ink outlines. Match its beveled silver frames, charcoal-blue backgrounds and visible warm ivory or pale cyan soft glows BEHIND every subject. Do not weaken the glow. Keep the four corners dark.
Every cell represents ONE coherent building, institution or concept. Secondary props can identify that institution, but keep the composition simple and readable at 64 pixels. Prefer a recognizable building silhouette over tiny interior details. Depict human subjects as natural-proportioned TORSO-UP portraits, never compressed full-body figures. Do not fuse tools, weapons or unrelated objects. No text, labels, letters, numbers, logos or watermarks. Use original designs, not copied game art.
The following numbered rows and columns are instructions, NOT text to render. Make every cell represent its assigned subject. Do not borrow sample subjects from the style reference.\n'''
    for row in range(12):
        prompt += f'ROW {row + 1}:\n'
        for column in range(12):
            entry = entries[row * 12 + column]
            prompt += f'Column {column + 1}, {entry["label"]}: {entry["subject"]}.\n'
    prompt += 'Final check: 12 columns, 12 equal-height rows, 144 distinct square framed icons. Bottom-row cells have exactly the same dimensions as top-row cells.'
    (batch / 'prompt.txt').write_text(prompt + '\n')
    subprocess.run([
        'python3', str(CLI), '--model', metadata['model'],
        '--aspect-ratio', '1:1', '--quality', 'high', '--count', '1',
        '--no-chain', '--output-dir', str(batch), '--prefix', 'sheet',
        '--reference', str(TEMPLATE), '--reference', str(REFERENCE),
        '--prompt', prompt,
    ], check=True)


if __name__ == '__main__':
    main()
