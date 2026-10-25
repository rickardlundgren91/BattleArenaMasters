#!/usr/bin/env python3
"""Builds every BAM icon and store image from Rickard's logo (Docs/play/branding/bam-logo.png, 2026-10-25).
Replaces the code-drawn look of make_branding.py; same output files and sizes:

    python3 Docs/play/branding/make_branding_logo.py

The logo is a rounded gold badge on black. The black corners are cut away (flood fill from the corners), and the badge
is placed on a dark navy background so no launcher or Play mask shows black or clips the frame.
"""
import math
import os

from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
LOGO = os.path.join(HERE, "bam-logo.png")

NAVY_DARK, NAVY, GLOW = (6, 12, 26), (16, 34, 66), (40, 170, 230)


def badge():
    """The logo with its black outer corners made transparent."""
    img = Image.open(LOGO).convert("RGB")
    w, h = img.size
    dark = img.convert("L").point(lambda v: 255 if v < 40 else 0)
    # Flood from the four corners through near-black pixels only: that is the outside of the badge.
    outside = Image.new("L", (w, h), 0)
    seed = dark.copy()
    for xy in ((0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1)):
        ImageDraw.floodfill(seed, xy, 128)
    outside = seed.point(lambda v: 255 if v == 128 else 0)
    alpha = outside.point(lambda v: 255 - v).filter(ImageFilter.GaussianBlur(1.2))
    out = img.convert("RGBA")
    out.putalpha(alpha)
    return out.crop(alpha.point(lambda v: 255 if v > 8 else 0).getbbox())


def background(w, h, glow=0.55):
    """Dark navy with a soft cyan-blue glow in the middle, matching the badge's inner colours."""
    img = Image.new("RGB", (w, h))
    px = img.load()
    cx, cy, r = w / 2, h / 2, math.hypot(w, h) / 2
    for y in range(h):
        for x in range(w):
            t = min(1.0, math.hypot(x - cx, y - cy) / r)
            base = tuple(int(NAVY[i] + (NAVY_DARK[i] - NAVY[i]) * t) for i in range(3))
            g = max(0.0, 1.0 - t * 1.6) ** 2 * glow
            px[x, y] = tuple(min(255, int(base[i] + (GLOW[i] - base[i]) * g * 0.35)) for i in range(3))
    return img


def fit(layer, side):
    s = side / max(layer.width, layer.height)
    return layer.resize((max(1, round(layer.width * s)), max(1, round(layer.height * s))), Image.LANCZOS)


def paste_center(canvas, layer, cx=None, cy=None):
    cx = canvas.width / 2 if cx is None else cx
    cy = canvas.height / 2 if cy is None else cy
    canvas.alpha_composite(layer, (round(cx - layer.width / 2), round(cy - layer.height / 2)))


def save(img, path, mode):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.convert(mode).save(path, optimize=True)
    print("wrote", os.path.relpath(path, ROOT), img.size, mode)


def main():
    b = badge()
    branding = os.path.join(ROOT, "Assets", "BAM", "Branding")
    play = os.path.join(ROOT, "Docs", "play")

    # Adaptive icon (432 canvas). Launchers show about the centre 288 as a circle or squircle; a rounded square of side
    # 230 fits inside a 288 circle with its corners, so nothing of the frame is cut.
    save(background(432, 432), os.path.join(branding, "icon_background.png"), "RGB")
    fg = Image.new("RGBA", (432, 432), (0, 0, 0, 0))
    paste_center(fg, fit(b, 230))
    save(fg, os.path.join(branding, "icon_foreground.png"), "RGBA")

    # Legacy/round icon and store icon: badge almost full size on navy. Play rounds the store icon's corners itself
    # (about 20% radius), so a thin navy margin keeps the gold frame whole.
    full = background(512, 512).convert("RGBA")
    paste_center(full, fit(b, 478))
    save(full, os.path.join(branding, "icon_legacy.png"), "RGB")
    store = background(512, 512).convert("RGBA")
    paste_center(store, fit(b, 470))
    save(store, os.path.join(play, "store-icon-512.png"), "RGBA")

    # Feature graphic 1024x500: badge centred, with room around it (Play may crop the edges or put a play button on it).
    fgimg = background(1024, 500, glow=0.9).convert("RGBA")
    paste_center(fgimg, fit(b, 440))
    save(fgimg, os.path.join(play, "feature-graphic-1024x500.png"), "RGB")


if __name__ == "__main__":
    main()
