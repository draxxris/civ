#!/usr/bin/env python3
"""Prepare generated terrain albedo and height PNGs. Requires Pillow and NumPy.

Usage: python3 tools/art/terrain_textures.py --selection /tmp/p5/selection.json
The selection JSON maps each layer name to its selected candidate image path.
Candidates and contact sheets stay outside the repository.
"""

import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

SIZE = 1024
# Aliases preserve the existing content-id asset contract without duplicate generations.
ALIASES = {
    "peak": "rock",
    "coast": "seabed",
    "slimy_coast": "seabed",
    "ocean": "deep_seabed",
    "slimy_ocean": "deep_seabed",
}


def _feather(length):
    distance = np.abs(np.arange(length) - length / 2)
    t = np.clip(1 - distance / (length * 0.18), 0, 1)
    return t * t * (3 - 2 * t)


def _pin_edges(pixels):
    """Feather opposite edge differences into 24 pixels, then match endpoints exactly."""
    pixels = pixels.copy()
    for axis in (1, 0):
        view = np.swapaxes(pixels, 0, axis)
        delta = (view[-1] - view[0]) / 2
        for i in range(24):
            t = 1 - i / 24
            weight = t * t * (3 - 2 * t)
            view[i] += delta * weight
            view[-1 - i] -= delta * weight
        edge = (view[0] + view[-1]) / 2
        view[0] = edge
        view[-1] = edge
    return pixels


def make_seamless(img):
    """Half-offset and smooth cross-fade, with periodic support at cross-arm endpoints.

    A single cross mask exposes the original's non-periodic edges at its endpoints.
    The separable cross uses one-axis offsets on those arms and the original at
    the crossing. This keeps both outer boundaries periodic, including corners.
    """
    original = np.asarray(img.convert("RGB").resize((SIZE, SIZE), Image.Resampling.LANCZOS), dtype=np.float32)
    offset_x = np.roll(original, SIZE // 2, axis=1)
    offset_y = np.roll(original, SIZE // 2, axis=0)
    offset_xy = np.roll(offset_x, SIZE // 2, axis=0)
    mx = _feather(SIZE)[None, :, None]
    my = _feather(SIZE)[:, None, None]
    blended = (offset_xy * (1 - mx) * (1 - my)
               + offset_y * mx * (1 - my)
               + offset_x * (1 - mx) * my
               + original * mx * my)
    return Image.fromarray(np.clip(np.rint(_pin_edges(blended)), 0, 255).astype(np.uint8))


def height_from_albedo(img):
    """Put normalised high-pass luminance in alpha, using a periodic Gaussian blur."""
    rgb = np.asarray(img.convert("RGB"), dtype=np.float32)
    luminance = rgb @ np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)
    h, w = luminance.shape
    tiled = np.tile(luminance, (3, 3))
    blur = Image.fromarray(np.rint(tiled).astype(np.uint8)).filter(ImageFilter.GaussianBlur(12))
    low = np.asarray(blur, dtype=np.float32)[h:2 * h, w:2 * w]
    high = luminance - low
    span = float(high.max() - high.min())
    height = (high - high.min()) / span if span > 1e-6 else np.full_like(high, 0.5)
    rgba = np.dstack((rgb, height * 255))
    return Image.fromarray(np.clip(np.rint(_pin_edges(rgba)), 0, 255).astype(np.uint8))


def contact_sheet(img, path):
    rgb = img.convert("RGB")
    sheet = Image.new("RGB", (rgb.width * 3, rgb.height * 3))
    for y in range(3):
        for x in range(3):
            sheet.paste(rgb, (x * rgb.width, y * rgb.height))
    sheet.save(path)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--selection", type=Path, required=True)
    parser.add_argument("--output", type=Path, default=Path("src/Mods/World/art/terrain"))
    parser.add_argument("--work", type=Path, default=Path("/tmp/p5/contact-sheets"))
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    args.work.mkdir(parents=True, exist_ok=True)
    selection = json.loads(args.selection.read_text())
    images = {}
    for layer, source in selection.items():
        with Image.open(source) as candidate:
            images[layer] = height_from_albedo(make_seamless(candidate))
    for alias, layer in ALIASES.items():
        if layer in images:
            images[alias] = images[layer]
    for layer, image in images.items():
        image.save(args.output / f"{layer}.png")
        contact_sheet(image, args.work / f"{layer}.png")
        pixels = np.asarray(image)
        if not (np.array_equal(pixels[0], pixels[-1]) and np.array_equal(pixels[:, 0], pixels[:, -1])):
            raise ValueError(f"Non-periodic edges: {layer}")
        print(f"{layer}: {image.size}, {image.mode}, periodic edges")


if __name__ == "__main__":
    main()
