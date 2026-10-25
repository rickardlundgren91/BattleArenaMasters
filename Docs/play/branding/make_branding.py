#!/usr/bin/env python3
"""Draws every BAM icon and store image from code (Pillow + Lilita One, OFL). No other artwork is used.

    python3 Docs/play/branding/make_branding.py [--font path/to/LilitaOne.ttf]

Writes (paths relative to the bam-unity working copy):
  Assets/BAM/Branding/icon_foreground.png  432x432, transparent, art inside the centre 288x288 safe zone (adaptive layer)
  Assets/BAM/Branding/icon_background.png  432x432, opaque (adaptive layer)
  Assets/BAM/Branding/icon_legacy.png      512x512, opaque square (legacy/round/default icon)
  Docs/play/store-icon-512.png             512x512, opaque, 32-bit PNG (Play store icon)
  Docs/play/feature-graphic-1024x500.png   1024x500, opaque, 24-bit PNG (Play feature graphic)

The look: the two team colours (blue below-left, red above-right) split by a bright diagonal clash, and "BAM" in gold
with a thick dark outline. Everything is drawn at 4x and scaled down for smooth edges.
"""
import argparse
import math
import os

from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", ".."))  # bam-unity
DEFAULT_FONTS = [
    os.path.join(ROOT, "Assets", "BAM", "Fonts", "LilitaOne.ttf"),
    "/mnt/project-files/bam-ref/xyz/ABC2/Resources/Fonts/LilitaOne.ttf",
]

SS = 4  # supersampling

BLUE_DARK, BLUE = (18, 46, 140), (46, 112, 255)
RED_DARK, RED = (120, 14, 40), (236, 56, 80)
GOLD_TOP, GOLD_BOTTOM = (255, 232, 110), (255, 160, 30)
INK = (16, 18, 40)
FLASH = (255, 246, 214)


def lerp(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(len(a)))


def team_background(w, h, seam=0.5, angle_deg=-28.0):
    """Blue below-left, red above-right, with a glowing diagonal seam through the centre."""
    img = Image.new("RGB", (w, h))
    px = img.load()
    a = math.radians(angle_deg)
    nx, ny = -math.sin(a), math.cos(a)  # normal of the seam line
    cx, cy = w * seam, h * 0.5
    diag = math.hypot(w, h)
    for y in range(h):
        for x in range(w):
            d = ((x - cx) * nx + (y - cy) * ny) / diag  # signed distance to the seam, about -0.5..0.5
            r = math.hypot(x - cx, y - cy) / diag        # distance from the centre, for a soft vignette
            if d > 0:
                c = lerp(BLUE, BLUE_DARK, min(1.0, d * 2.2 + r * 0.6))
            else:
                c = lerp(RED, RED_DARK, min(1.0, -d * 2.2 + r * 0.6))
            glow = max(0.0, 1.0 - abs(d) * 22.0) ** 2
            px[x, y] = lerp(c, FLASH, glow * 0.9)
    return img


def burst(draw, cx, cy, r_in, r_out, points, fill, rot=0.0):
    """A comic-style impact star behind the word."""
    pts = []
    for i in range(points * 2):
        ang = rot + math.pi * i / points
        r = r_out if i % 2 == 0 else r_in
        pts.append((cx + math.cos(ang) * r, cy + math.sin(ang) * r))
    draw.polygon(pts, fill=fill)


def word(text, font_path, height_px, tilt_deg=-6.0):
    """Gold text with a thick dark outline and a drop shadow, as an RGBA image cropped to its content."""
    size = int(height_px * 1.25)
    font = ImageFont.truetype(font_path, size)
    l, t, r, b = font.getbbox(text)
    tw, th = r - l, b - t
    outline = max(2, int(size * 0.085))
    pad = outline * 3
    W, H = tw + pad * 2, th + pad * 2
    ox, oy = pad - l, pad - t

    # gold fill with a vertical gradient
    mask = Image.new("L", (W, H), 0)
    ImageDraw.Draw(mask).text((ox, oy), text, font=font, fill=255)
    grad = Image.new("RGBA", (W, H))
    gp = grad.load()
    for y in range(H):
        c = lerp(GOLD_TOP, GOLD_BOTTOM, min(1.0, max(0.0, (y - pad) / max(1, th))))
        for x in range(W):
            gp[x, y] = c + (255,)
    fill = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    fill.paste(grad, (0, 0), mask)

    # outline: the text drawn with a stroke, in ink
    stroke = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(stroke).text((ox, oy), text, font=font, fill=INK + (255,), stroke_width=outline, stroke_fill=INK + (255,))

    # shine: a light band on the upper part of the letters
    shine_mask = Image.new("L", (W, H), 0)
    ImageDraw.Draw(shine_mask).rectangle((0, 0, W, oy + int(th * 0.28) + t), fill=110)
    shine_mask = Image.composite(shine_mask, Image.new("L", (W, H), 0), mask)
    shine = Image.new("RGBA", (W, H), (255, 255, 255, 0))
    shine.putalpha(shine_mask)

    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    shadow = stroke.copy()
    shadow.putalpha(stroke.getchannel("A").point(lambda v: int(v * 0.55)))
    shift = outline
    out.alpha_composite(shadow, (shift // 2, shift))
    out.alpha_composite(stroke)
    out.alpha_composite(fill)
    out.alpha_composite(shine)
    out = out.rotate(tilt_deg, resample=Image.BICUBIC, expand=True)
    return out.crop(out.getbbox())


def place_center(canvas, layer, cx, cy, max_w, max_h):
    s = min(max_w / layer.width, max_h / layer.height)
    layer = layer.resize((max(1, int(layer.width * s)), max(1, int(layer.height * s))), Image.LANCZOS)
    canvas.alpha_composite(layer, (int(cx - layer.width / 2), int(cy - layer.height / 2)))


def emblem(font_path, size, safe):
    """Burst + BAM, transparent, fitted inside a centred square of side `safe`."""
    S = size * SS
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    c = S / 2
    rs = safe * SS / 2
    burst(d, c, c, rs * 0.70, rs * 0.98, 12, INK + (255,), rot=0.13)
    burst(d, c, c, rs * 0.62, rs * 0.88, 12, FLASH + (255,), rot=0.13)
    w = word("BAM", font_path, int(rs * 0.9))
    place_center(img, w, c, c * 1.0, rs * 1.62, rs * 1.1)
    return img.resize((size, size), Image.LANCZOS)


def save(img, path, mode):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.convert(mode).save(path, optimize=True)
    print("wrote", os.path.relpath(path, ROOT), img.size, mode)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--font", default=None)
    args = ap.parse_args()
    font = args.font or next((p for p in DEFAULT_FONTS if os.path.exists(p)), None)
    if not font:
        raise SystemExit("Lilita One not found; pass --font")

    branding = os.path.join(ROOT, "Assets", "BAM", "Branding")
    play = os.path.join(ROOT, "Docs", "play")

    # Adaptive icon: 432 canvas (108 dp at xxxhdpi), launchers crop to a circle/squircle of about the centre 288 (72 dp).
    bg = team_background(432, 432)
    save(bg, os.path.join(branding, "icon_background.png"), "RGB")
    fg = emblem(font, 432, 250)
    save(fg, os.path.join(branding, "icon_foreground.png"), "RGBA")

    # Legacy/round icon and the store icon: the same picture, full bleed (Play applies its own rounded mask).
    full = team_background(512, 512).convert("RGBA")
    full.alpha_composite(emblem(font, 512, 420))
    save(full, os.path.join(branding, "icon_legacy.png"), "RGB")
    save(full, os.path.join(play, "store-icon-512.png"), "RGBA")

    # Feature graphic: keep the important part away from the edges (Play may crop or overlay a play button in the middle).
    W, H = 1024, 500
    fgimg = team_background(W, H, angle_deg=-18.0).convert("RGBA")
    big = Image.new("RGBA", (W * SS, H * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(big)
    burst(d, W * SS * 0.5, H * SS * 0.40, H * SS * 0.28, H * SS * 0.40, 14, INK + (255,), rot=0.1)
    burst(d, W * SS * 0.5, H * SS * 0.40, H * SS * 0.25, H * SS * 0.36, 14, FLASH + (255,), rot=0.1)
    place_center(big, word("BAM", font, H * SS * 0.30), W * SS * 0.5, H * SS * 0.40, W * SS * 0.40, H * SS * 0.40)
    place_center(big, word("BATTLE ARENA MASTERS", font, H * SS * 0.11, tilt_deg=0.0), W * SS * 0.5, H * SS * 0.82,
                 W * SS * 0.80, H * SS * 0.16)
    fgimg.alpha_composite(big.resize((W, H), Image.LANCZOS))
    save(fgimg, os.path.join(play, "feature-graphic-1024x500.png"), "RGB")


if __name__ == "__main__":
    main()
