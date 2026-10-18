"""Generate a catalog-mapped 8x8 preview. Never install game assets."""

import argparse
import json
from pathlib import Path
import subprocess
import sys

from PIL import Image, ImageDraw

from reviewer import prepare_review

ROOT = Path(__file__).resolve().parent
CLI = Path('/home/gordon/.agents/skills/openai-image-generator/scripts/openai_images.py')
MODEL = 'gpt-image-2.5-flare'


def load_cells(subjects, start, count):
    document = json.loads(subjects.read_text(encoding='utf-8'))
    if not isinstance(document, dict) or document.get('version') != 1:
        raise ValueError('Expected a version 1 object with cells')
    entries = document.get('cells')
    if not isinstance(entries, list) or not 0 <= start < len(entries):
        raise ValueError('The start offset must select an existing cell')
    if not 1 <= count <= 64:
        raise ValueError('The count must be from 1 to 64')
    cells = []
    catalog_ids, original_paths = set(), set()
    for index, entry in enumerate(entries[start:start + count]):
        if not isinstance(entry, dict):
            raise ValueError('Each subject must be an object')
        cell = dict(entry, row=index // 8 + 1, column=index % 8 + 1,
                    edit_bounds=prepare_review.edit_bounds(index),
                    review_status='pending', installed=False)
        prepare_review.validate_cell(cell, index, catalog_ids, original_paths)
        cells.append(cell)
    return cells


def validate_layout(source_path, mask_path):
    """Reject artwork references: both inputs must match the procedural layout."""
    expected_source = Image.new('RGBA', (816, 816), (255, 0, 255, 255))
    expected_mask = Image.new('RGBA', (816, 816), (255, 255, 255, 255))
    source_draw, mask_draw = ImageDraw.Draw(expected_source), ImageDraw.Draw(expected_mask)
    for index in range(64):
        left, top, right, bottom = prepare_review.edit_bounds(index)
        box = (left, top, right - 1, bottom - 1)
        source_draw.rectangle(box, fill=(18, 31, 48, 255))
        mask_draw.rectangle(box, fill=(0, 0, 0, 0))
    for path, expected in ((source_path, expected_source), (mask_path, expected_mask)):
        with Image.open(path) as image:
            if image.size != expected.size or image.convert('RGBA').tobytes() != expected.tobytes():
                raise ValueError(f'Not the canonical blank layout or mask: {path}')


def build_prompt(cells):
    prompt = '''Fill ONLY the transparent editable squares in the supplied mask with entirely NEW original BORDERLESS strategy-game artwork. The source is a procedural layout of exactly 64 blank charcoal-blue squares on pink, not old artwork. Fill each existing dark square with its assigned subject. Preserve the existing square boundaries exactly. Preserve every unmasked pixel. Do not add pink lines inside a square or continue a subject across a gutter.
The mask defines exactly EIGHT columns and EIGHT rows, 64 separate equal SQUARE regions. One complete icon per assigned square. Use these exact regions, not a replacement grid. On the 816-square canvas each editable region is 90 by 90 pixels, gutters are 10 pixels, and outer margins are 13 pixels. Top-left coordinates: x=13+100*(column-1), y=13+100*(row-1). Do not straddle, merge, split, duplicate or omit squares.
NO BORDERS: no frames, silver edges, blue rims, bevels, outlines around tiles, decorative corners, inset panels or outer strokes. Frames will be added later. Each charcoal-blue background must fill its entire editable square edge-to-edge. Center each subject with breathing room. Pink exists ONLY in protected gutters, never inside editable squares. Keep each subject within its own masked square.
ART DIRECTION: mature stylized hand-painted strategy illustrations, simplified dimensional forms, rich warm colors, softly brushed shading, strong recognizable silhouettes. Behind every subject, add a broad, clearly visible radial glow with a bright ivory or pale-cyan center fading into dark charcoal-blue corners. Make it high-contrast and readable at 64 pixels, not a faint rim or tiny aura. Few details, readable at 64 pixels. No photorealism, detailed 3D rendering, childish cartoons or thick black ink outlines. One coherent institution or concept per square. No fused unrelated objects. People use naturally proportioned torso-up framing.
BASE ARTWORK ONLY. Do not generate overlaid religious, guild or corporate logo badges, corner emblems or medallions. These will be added separately later. Religious architecture should have no added corner symbols. No letters, labels, numbers or watermarks EXCEPT an explicitly requested neon sign in a subject. Position identifiers below are instructions, not text to render.
'''
    for index in range(64):
        row, column = index // 8 + 1, index % 8 + 1
        if column == 1:
            prompt += f'\nROW {row}, eight masked squares from left to right:\n'
        if index < len(cells):
            cell = cells[index]
            prompt += f'Column {column}: {cell["label"]}: {cell["subject"]}\n'
        else:
            prompt += f'Column {column}: UNUSED SLOT. Leave this square plain charcoal-blue, without a subject.\n'
    return prompt + '\nFinal check: all assigned subjects fit in their own squares. No frames or logo badges. All protected pink pixels unchanged.\n'


def generated_sheet(batch):
    records = (batch / 'manifest.jsonl').read_text(encoding='utf-8').splitlines()
    if len(records) != 1:
        raise ValueError('Expected exactly one generation result')
    # The direct image CLI records the actual output basename in its manifest.
    return prepare_review.resolve_sheet(batch, {'sheet_file': json.loads(records[0])['output']})


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('subjects', type=Path, help='Version 1 JSON object with cells')
    parser.add_argument('batch_name', help='New directory name inside icon-workbench/batches')
    parser.add_argument('--start', type=int, default=0, help='Zero-based subject offset')
    parser.add_argument('--count', type=int, default=64, help='1 to 64 subjects; unused slots stay blank')
    parser.add_argument('--dry-run', action='store_true', help='Write prompt and cells without calling the API')
    args = parser.parse_args(argv)
    cells = load_cells(args.subjects, args.start, args.count)
    batch = prepare_review.batch_directory(args.batch_name)
    source, mask = ROOT / 'layout/blank-layout.png', ROOT / 'layout/mask.png'
    validate_layout(source, mask)
    batch.mkdir(parents=True, exist_ok=False)
    metadata = {
        'version': 1, 'model': MODEL, 'rows': 8, 'columns': 8,
        'generation_mode': 'direct OpenAI masked edit; procedural layout only',
        'layout_reference': 'layout/blank-layout.png', 'mask_file': 'layout/mask.png',
        'references': [], 'geometry': prepare_review.GEOMETRY,
        'subject_source': str(args.subjects.resolve()), 'start_offset': args.start,
        'filler_slots': 64 - len(cells), 'status': 'prepared_for_generation', 'cells': cells,
    }
    metadata_path = batch / 'cells.json'
    metadata_path.write_text(json.dumps(metadata, indent=2) + '\n', encoding='utf-8')
    prompt = build_prompt(cells)
    (batch / 'prompt.txt').write_text(prompt, encoding='utf-8')
    if args.dry_run:
        print(f'Prepared {len(cells)} subjects and {64 - len(cells)} blank slots in {batch}. No API call made.')
        return
    subprocess.run([
        sys.executable, str(CLI), '--mode', 'edit', '--model', MODEL,
        '--image', str(source), '--mask', str(mask), '--size', '816x816',
        '--quality', 'low', '--count', '1', '--no-chain',
        '--output-dir', str(batch), '--prompt', prompt,
    ], check=True)
    metadata['sheet_file'] = generated_sheet(batch).name
    metadata['status'] = 'awaiting_review'
    metadata_path.write_text(json.dumps(metadata, indent=2) + '\n', encoding='utf-8')
    print(f'Generated {batch / metadata["sheet_file"]}. Run reviewer/prepare_review.py before review.')


if __name__ == '__main__':
    main()
