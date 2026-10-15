"""Remove detached sheet debris without dropping meaningful symbol components."""
from collections import deque
from math import hypot
from PIL import Image

# Optical corrections at final 64-pixel icon scale, relative to box centering.
BADGE_OFFSETS = {'confectionery': (0, 1), 'asatru': (0, -1), 'hindu': (0, -1)}

def component_pixels(mask):
    remaining = {(x, y) for y in range(mask.height) for x in range(mask.width) if mask.getpixel((x, y))}
    while remaining:
        seed = remaining.pop()
        group = [seed]
        queue = deque([seed])
        while queue:
            x, y = queue.popleft()
            neighbors = {(x + dx, y + dy) for dx in (-1, 0, 1) for dy in (-1, 0, 1)} & remaining
            remaining.difference_update(neighbors)
            group.extend(neighbors)
            queue.extend(neighbors)
        yield group


def clean_badge(image):
    groups = list(component_pixels(image.getchannel('A')))
    main = max(groups, key=len)
    threshold = len(main) * 0.02
    main_bottom = max(y for x, y in main)
    main_height = main_bottom - min(y for x, y in main) + 1
    # Neighboring rows leave detached arcs beneath otherwise complete badges.
    # Keep separate upper accents (Om), musical notes, and paw-print toes.
    bottom_limit = main_bottom + max(3, main_height * 0.08)
    retained = [group for group in groups if len(group) >= threshold
                and min(y for x, y in group) <= bottom_limit]
    mask = Image.new('L', image.size)
    for group in retained:
        for point in group:
            mask.putpixel(point, 255)
    clean = image.copy()
    clean.putalpha(mask)
    return clean.crop(clean.getbbox())


def fit_badge(image, key=None):
    max_size, max_radius = {'asatru': (24, 12.5)}.get(key, (22, 11.5))
    badge = image.copy()
    badge.thumbnail((max_size, max_size), Image.Resampling.LANCZOS)
    radius = max(hypot(x + 0.5 - badge.width / 2, y + 0.5 - badge.height / 2)
                 for y in range(badge.height) for x in range(badge.width)
                 if badge.getpixel((x, y))[3] > 64)
    scale = min(1, max_radius / radius)
    size = (max(1, int(badge.width * scale)), max(1, int(badge.height * scale)))
    return badge.resize(size, Image.Resampling.LANCZOS)
