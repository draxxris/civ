"""Procedural button mockups for the "steel lapis" look, compared with docs/references/button.png.

Standalone preview to tune the look before porting the generator to Ui.cs.
Run: python3 tools/ui-mockups/button_pcg.py  ->  docs/references/button-pcg.png

Model (measured from the reference):
- straight sides, a small 45-degree corner cut;
- a 2 px near-black rim;
- a soft raised lip about 4 px wide, peaking ~2 px inside the rim, lit from the top and right;
- the "chipped stone" look is brightness mottling and small dark nicks along the lip,
  not bites out of the outline;
- a vertical navy gradient with an inner glow and cloudy stone grain.
"""

from dataclasses import dataclass
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

SS = 4  # supersampling factor
W, H = 247, 62  # button size in final pixels (same as the reference)
FONT = "/usr/share/fonts/google-noto-vf/NotoSerif[wght].ttf"
ROOT = Path(__file__).resolve().parents[2]
REFERENCE = ROOT / "docs" / "references" / "button.png"
OUT = ROOT / "docs" / "references" / "button-pcg.png"


def rgb(h):
    return np.array([int(h[i : i + 2], 16) / 255 for i in (1, 3, 5)])


@dataclass(frozen=True)
class Style:
    name: str
    chamfer: float = 4.0  # px corner cut
    rim: float = 2.0  # px dark outer line
    lip_peak: float = 1.4  # px inside the rim where the lip is brightest
    lip_width: float = 1.0  # px, gaussian width of the lip
    groove: float = 0.18  # darkening just inside the lip
    mottle: float = 0.25  # brightness noise on the lip
    nicks: int = 6  # dark nicks along the lip
    nick_depth: float = 0.55
    wobble: float = 0.0  # px outline wobble (0 = straight)
    grain: float = 0.22  # faint stone noise in the fill, under the glow
    seed: int = 7


TOP, BOTTOM = rgb("#2a3d51"), rgb("#233548")
GLOW, GLOW_STRENGTH, GLOW_WIDTH = rgb("#5f9ad6"), 0.5, 5.0
RIM = rgb("#05080b")
LIP = rgb("#6495bb")  # lip colour at full light
TEXT = rgb("#e8eef6")

STYLES = [
    Style("A1  smooth lip, light mottling", mottle=0.15, nicks=0, grain=0.18),
    Style("A2  mottled lip with nicks (closest)", mottle=0.28, nicks=7),
    Style("A3  worn: stronger nicks, faint wobble", mottle=0.38, nicks=12, nick_depth=0.7, wobble=0.18, grain=0.28),
]


def value_noise(shape, cell, rng):
    """Smooth noise in [-1, 1]: a random grid upsampled bicubically."""
    gh, gw = shape[0] // cell + 3, shape[1] // cell + 3
    grid = Image.fromarray((rng.random((gh, gw)) * 255).astype(np.uint8))
    big = grid.resize((gw * cell, gh * cell), Image.BICUBIC)
    return np.asarray(big, dtype=np.float32)[: shape[0], : shape[1]] / 127.5 - 1


def chamfer_box(x, y, bx, by, c):
    qx, qy = np.abs(x) - bx, np.abs(y) - by
    box = np.hypot(np.maximum(qx, 0), np.maximum(qy, 0)) + np.minimum(np.maximum(qx, qy), 0)
    cut = (np.abs(x) + np.abs(y) - (bx + by - c)) / np.sqrt(2)
    return np.maximum(box, cut)


def mix(a, b, t):
    t = np.asarray(t)[..., None]
    return np.asarray(a) * (1 - t) + np.asarray(b) * t


def render_button(style, state, label):
    rng = np.random.default_rng(style.seed)  # same texture and nicks across states
    ys, xs = np.mgrid[0 : H * SS, 0 : W * SS].astype(np.float32)
    x = (xs + 0.5) / SS - W / 2
    y = (ys + 0.5) / SS - H / 2
    shp = x.shape
    d = chamfer_box(x, y, W / 2, H / 2, style.chamfer)
    gy, gx = np.gradient(d)  # normals from the clean shape keep the sides straight
    if style.wobble:
        d = d + style.wobble * value_noise(shp, 9 * SS, rng)
    inset = -d
    pressed, hover = state == "pressed", state == "hover"

    # Fill: vertical gradient, inner glow, cloudy stone grain.
    t = np.clip((y + H / 2) / H, 0, 1)
    top, bottom = TOP.copy(), BOTTOM.copy()
    if hover:
        top, bottom = top * 1.18 + 0.02, bottom * 1.12 + 0.01
    if pressed:
        top, bottom = bottom * 0.8, top * 0.85
    fill = mix(top, bottom, t)
    grain = (0.35 * value_noise(shp, 1 * SS, rng) + 0.35 * value_noise(shp, 3 * SS, rng)
             + 0.3 * value_noise(shp, 14 * SS, rng))
    fill = fill * (1 + style.grain * grain[..., None])
    inner = style.rim + style.lip_peak
    g = np.exp(-np.maximum(inset - inner, 0) / GLOW_WIDTH)
    strength = GLOW_STRENGTH * (1.6 if hover else 1.0) * (0.35 if pressed else 1.0)
    fill = fill + GLOW * (g * strength)[..., None] * 0.55
    if pressed:  # inner shadow under the top edge
        fill = fill * (1 - 0.45 * np.exp(-np.maximum(inset - inner, 0) / 5) * (t < 0.6))[..., None]

    # Lip: light from the top and right; flipped when pressed so it reads as sunk.
    n = np.hypot(gx, gy) + 1e-6
    nx, ny = gx / n, gy / n
    sign = -1 if pressed else 1
    lit = 0.83 + sign * (0.17 * -ny + 0.07 * nx)
    lip_noise = 1 + style.mottle * (0.6 * value_noise(shp, 2 * SS, rng) + 0.4 * value_noise(shp, 6 * SS, rng))
    lip_noise = lip_noise * nick_mask(style, rng, x, y)
    lip_colour = np.clip(LIP * (lit * lip_noise)[..., None] * (1.12 if hover else 1.0), 0, 1)
    p = inset - style.rim
    lip = np.exp(-(((p - style.lip_peak) / style.lip_width) ** 2))
    lip = np.where(p < style.lip_peak, np.exp(-(((p - style.lip_peak) / (style.lip_width * 0.7)) ** 2)), lip)
    groove = style.groove * np.exp(-(((p - style.lip_peak - 2.0) / 1.0) ** 2))
    col = mix(fill * (1 - groove)[..., None], lip_colour, np.clip(lip, 0, 1) * (p > 0))
    # Shadow under the bottom lip: the rim reads thicker there.
    col = col * (1 - 0.45 * np.clip((y - (H / 2 - 4)) / 2, 0, 1) * np.exp(-np.maximum(p, 0) / 1.2) * (ny > 0.5))[..., None]

    # Dark rim with an anti-aliased inner edge.
    col = mix(col, RIM, np.clip(style.rim - inset + 0.5 / SS * SS, 0, 1) * (inset < style.rim + 0.5))
    alpha = np.clip(inset * SS + 0.5, 0, 1)
    rgba = np.dstack([np.clip(col, 0, 1), alpha])
    img = Image.fromarray((rgba * 255).astype(np.uint8), "RGBA")
    draw_label(img, label, pressed, hover)
    return img


def nick_mask(style, rng, x, y):
    """Small dark nicks spread along the lip: elongated along the edge, shallow across it."""
    mask = np.ones_like(x)
    bx, by = W / 2, H / 2
    for _ in range(style.nicks):
        side = rng.integers(4)
        along = rng.uniform(1.5, 4.0)
        if side < 2:
            cx, cy = rng.uniform(-bx + 8, bx - 8), (-by if side == 0 else by)
            dist = np.hypot((x - cx) / along, (y - cy) / 4)
        else:
            cx, cy = (-bx if side == 2 else bx), rng.uniform(-by + 6, by - 6)
            dist = np.hypot((x - cx) / 4, (y - cy) / along)
        mask = mask * (1 - style.nick_depth * np.clip(1 - dist, 0, 1) ** 0.7)
    return mask


def draw_label(img, label, pressed, hover):
    font = ImageFont.truetype(FONT, 20 * SS)
    shift = SS if pressed else 0
    tw = font.getlength(label)
    icon, gap = 30 * SS, 14 * SS
    x0 = (img.width - (icon + gap + tw)) / 2
    cy = img.height / 2 + shift
    colour = tuple(int(c * 255) for c in TEXT)
    if hover:
        colour = (255, 255, 255)
    if pressed:
        colour = tuple(int(c * 0.82) for c in colour)

    shadow = Image.new("RGBA", img.size, (0, 0, 0, 0))
    sd = ImageDraw.Draw(shadow)
    draw_icon(sd, x0 + icon / 2, cy + SS, icon, (0, 0, 0, 210))
    sd.text((x0 + icon + gap, cy + SS), label, font=font, fill=(0, 0, 0, 210), anchor="lm")
    img.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(1.2 * SS)))
    fg = ImageDraw.Draw(img)
    draw_icon(fg, x0 + icon / 2, cy, icon, colour + (255,))
    fg.text((x0 + icon + gap, cy), label, font=font, fill=colour + (255,), anchor="lm")


def draw_icon(draw, cx, cy, size, colour):
    """Crosshair, like the reference."""
    r = size * 0.32
    lw = int(1.8 * SS)
    draw.ellipse([cx - r, cy - r, cx + r, cy + r], outline=colour, width=lw)
    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        draw.line([cx + dx * r * 0.6, cy + dy * r * 0.6, cx + dx * size * 0.5, cy + dy * size * 0.5],
                  fill=colour, width=lw)
    dot = 1.4 * SS
    draw.ellipse([cx - dot, cy - dot, cx + dot, cy + dot], fill=colour)


def background(w, h):
    rng = np.random.default_rng(1)
    base = np.array([0.14, 0.15, 0.165])  # the reference sits on dark grey stone
    n = value_noise((h, w), 30 * SS, rng) * 0.012 + value_noise((h, w), 3 * SS, rng) * 0.012
    arr = np.clip(base + n[..., None], 0, 1)
    return Image.fromarray((np.dstack([arr, np.ones((h, w))]) * 255).astype(np.uint8), "RGBA")


def main():
    states = ["normal", "hover", "pressed"]
    pad, head, row_label = 24, 34, 26
    cw, rh = W + pad, H + row_label + pad
    rows = len(STYLES) + 1
    canvas = background((pad + cw * 3) * SS, (head + rh * rows + pad // 2) * SS)
    draw = ImageDraw.Draw(canvas)
    small = ImageFont.truetype(FONT, 15 * SS)
    for i, s in enumerate(states):
        draw.text(((pad + i * cw + W / 2) * SS, 16 * SS), s, font=small, fill=(150, 160, 178, 255), anchor="mm")

    # Row 0: the reference, cropped to the button, scaled to the supersampled canvas.
    ref = Image.open(REFERENCE).convert("RGBA").crop((7, 9, 7 + W, 9 + H))
    draw.text((pad * SS, (head + 6) * SS), "Reference", font=small, fill=(225, 190, 110, 255), anchor="lm")
    canvas.alpha_composite(ref.resize((W * SS, H * SS), Image.NEAREST), (pad * SS, (head + row_label) * SS))

    for r, style in enumerate(STYLES, start=1):
        oy = head + r * rh
        draw.text((pad * SS, (oy + 6) * SS), style.name, font=small, fill=(225, 190, 110, 255), anchor="lm")
        for i, state in enumerate(states):
            canvas.alpha_composite(render_button(style, state, "Track Quest"), ((pad + i * cw) * SS, (oy + row_label) * SS))
    final = canvas.resize((canvas.width // SS, canvas.height // SS), Image.LANCZOS).convert("RGB")
    final.save(OUT)
    print(OUT, final.size)


if __name__ == "__main__":
    main()
