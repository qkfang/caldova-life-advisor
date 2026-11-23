#!/usr/bin/env python3
"""
Generator for the Financial Crime "Try it out" specimen documents
(Advice Team in a Day — Caldova Life Advice).

Produces visibly fictional PNG documents watermarked "SPECIMEN — NOT A REAL
DOCUMENT" and the manifest.json the backend serves from
``src/backend/wwwroot/fincrime/samples/``:

* Priya Natarajan (REQ-20261103-LIF001, new family protection advice):
  Victoria-style driver licence and a payslip — both genuine.
* Grace Williams (REQ-20261106-LIF002, identity takeover and a fake trauma
  claim): an oncologist's report from "Dr A. Moretti, Eastern Oncology Group"
  (fake — pasted letterhead, edited dates, font mismatch, provider number in
  the wrong format, no histopathology), her driver licence with a tampered
  photo region, and a bank statement for a new account in the name
  "G Williams Consulting Pty Ltd" (suspicious — not the policy owner).
* George Papadopoulos and Harpreet Kaur (REQ-20261109-LIF003, business
  protection): company extract for Northcote Custom Joinery Pty Ltd, George's
  driver licence and Harpreet's passport — all genuine.
* Tom Bradley (REQ-20261111-LIF004, income protection claim): BAS and profit
  and loss summary for Bradley Electrical and the orthopaedic surgeon's
  report — both genuine.
* Robert Chen (REQ-20261113-LIF005, death claim): certified copy of Mei Chen's
  death certificate and Robert's certified driver licence — both genuine.

Every person, business, number, bank, clinic and layout is fictional. Layouts
are inspired by Australian documents but deliberately don't reproduce any real
government design, crest or security feature. ACNs and ABNs are chosen so their
check digits are invalid, so they can't belong to a real entity.

Run (requires Pillow — ``pip install pillow``):

    python tools/generate_fincrime_samples.py

The script also removes any PNG in the samples folder that isn't part of the
set. The output is committed to the repo — the web app never runs this script.
"""
from __future__ import annotations

import json
import math
from datetime import date
from functools import lru_cache
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

OUT_DIR = Path(__file__).resolve().parent.parent / "src" / "backend" / "wwwroot" / "fincrime" / "samples"
SRC_PREFIX = "/fincrime/samples/"
SPECIMEN = "SPECIMEN — NOT A REAL DOCUMENT"

# ───────────────────────── Fonts (Windows, Linux, macOS) ─────────────────────────
_FONT_CANDIDATES: dict[tuple[str, bool], list[str]] = {
    ("sans", False): [
        "C:/Windows/Fonts/arial.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
        "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
        "/System/Library/Fonts/Supplemental/Arial.ttf",
    ],
    ("sans", True): [
        "C:/Windows/Fonts/arialbd.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
        "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf",
        "/System/Library/Fonts/Supplemental/Arial Bold.ttf",
    ],
    ("mono", False): [
        "C:/Windows/Fonts/consola.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf",
        "/usr/share/fonts/truetype/liberation/LiberationMono-Regular.ttf",
        "/System/Library/Fonts/Supplemental/Courier New.ttf",
    ],
    ("mono", True): [
        "C:/Windows/Fonts/consolab.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSansMono-Bold.ttf",
        "/usr/share/fonts/truetype/liberation/LiberationMono-Bold.ttf",
        "/System/Library/Fonts/Supplemental/Courier New Bold.ttf",
    ],
    # Deliberately "wrong" typefaces used for edits on the fake documents.
    ("courier", False): [
        "C:/Windows/Fonts/cour.ttf",
        "/usr/share/fonts/truetype/liberation/LiberationMono-Regular.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf",
        "/System/Library/Fonts/Supplemental/Courier New.ttf",
    ],
    ("courier", True): [
        "C:/Windows/Fonts/courbd.ttf",
        "/usr/share/fonts/truetype/liberation/LiberationMono-Bold.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSansMono-Bold.ttf",
        "/System/Library/Fonts/Supplemental/Courier New Bold.ttf",
    ],
    ("times", False): [
        "C:/Windows/Fonts/times.ttf",
        "/usr/share/fonts/truetype/liberation/LiberationSerif-Regular.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSerif.ttf",
        "/System/Library/Fonts/Supplemental/Times New Roman.ttf",
    ],
    ("serif", False): [
        "C:/Windows/Fonts/georgia.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSerif.ttf",
        "/usr/share/fonts/truetype/liberation/LiberationSerif-Regular.ttf",
        "/System/Library/Fonts/Supplemental/Georgia.ttf",
    ],
    ("serif", True): [
        "C:/Windows/Fonts/georgiab.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSerif-Bold.ttf",
        "/usr/share/fonts/truetype/liberation/LiberationSerif-Bold.ttf",
        "/System/Library/Fonts/Supplemental/Georgia Bold.ttf",
    ],
    ("script", False): [
        "C:/Windows/Fonts/segoesc.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSerif-Italic.ttf",
        "/usr/share/fonts/truetype/liberation/LiberationSerif-Italic.ttf",
        "/System/Library/Fonts/Supplemental/Georgia Italic.ttf",
    ],
}


@lru_cache(maxsize=None)
def font(size: int, kind: str = "sans", bold: bool = False) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    for path in _FONT_CANDIDATES.get((kind, bold), []) + _FONT_CANDIDATES[("sans", bold)]:
        if Path(path).exists():
            return ImageFont.truetype(path, size)
    return ImageFont.load_default()


# ───────────────────────── Drawing helpers ─────────────────────────
INK = (17, 24, 39)
MUTED = (100, 116, 139)
RED = (190, 24, 24)
WHITE = (255, 255, 255, 255)


def text_width(d: ImageDraw.ImageDraw, text: str, f) -> int:
    box = d.textbbox((0, 0), text, font=f)
    return box[2] - box[0]


def scan_bed(w: int, h: int) -> Image.Image:
    """Neutral 'scanner bed' background a card sits on."""
    return Image.new("RGBA", (w, h), (218, 223, 229, 255))


def paper(w: int, h: int, colour=(255, 255, 255)) -> Image.Image:
    return Image.new("RGBA", (w, h), colour + (255,))


def guilloche(d: ImageDraw.ImageDraw, box: tuple[int, int, int, int], colour, *, rows: int = 10,
              amp: float = 7.0, period: float = 70.0, width: int = 1, phase: float = 0.0) -> None:
    x0, y0, x1, y1 = box
    for i in range(rows):
        base = y0 + (y1 - y0) * (i + 0.5) / rows
        pts = [(x, base + amp * math.sin((x - x0) / period * 2 * math.pi + i * 0.9 + phase))
               for x in range(x0, x1 + 1, 3)]
        d.line(pts, fill=colour, width=width)


def hologram(img: Image.Image, box: tuple[int, int, int, int], alpha: int = 120) -> None:
    """Rainbow 'hologram' band composited over part of the card."""
    x0, y0, x1, y1 = box
    w, h = x1 - x0, y1 - y0
    band = Image.new("RGBA", (w, h))
    px = band.load()
    for x in range(w):
        for y in range(h):
            t = (x + y * 0.7) / max(w * 0.45, 1)
            r = int(150 + 100 * math.sin(t * 2 * math.pi))
            g = int(150 + 100 * math.sin(t * 2 * math.pi + 2.1))
            b = int(150 + 100 * math.sin(t * 2 * math.pi + 4.2))
            px[x, y] = (r, g, b, alpha)
    bd = ImageDraw.Draw(band)
    for k in range(-h, w, 14):
        bd.line((k, h, k + h, 0), fill=(255, 255, 255, 90), width=2)
    bd.rounded_rectangle((0, 0, w - 1, h - 1), radius=10, outline=(255, 255, 255, 160), width=2)
    img.alpha_composite(band, (x0, y0))


def portrait(size: tuple[int, int], bg, fg, *, hair: str = "short", hair_colour=None) -> Image.Image:
    """Simple stylised head-and-shoulders silhouette (no real likeness)."""
    w, h = size
    im = Image.new("RGBA", (w, h), bg)
    d = ImageDraw.Draw(im)
    cx = w / 2
    hc = hair_colour or fg
    if hair == "long":
        d.ellipse((cx - w * 0.27, h * 0.12, cx + w * 0.27, h * 0.78), fill=hc)
    elif hair == "short":
        d.ellipse((cx - w * 0.21, h * 0.14, cx + w * 0.21, h * 0.50), fill=hc)
    elif hair == "bun":
        d.ellipse((cx - w * 0.09, h * 0.07, cx + w * 0.09, h * 0.22), fill=hc)
        d.ellipse((cx - w * 0.21, h * 0.15, cx + w * 0.21, h * 0.48), fill=hc)
    elif hair == "turban":
        d.ellipse((cx - w * 0.24, h * 0.08, cx + w * 0.24, h * 0.40), fill=hc)
    d.ellipse((cx - w * 0.45, h * 0.64, cx + w * 0.45, h * 1.35), fill=fg)
    d.rectangle((cx - w * 0.09, h * 0.48, cx + w * 0.09, h * 0.70), fill=fg)
    d.ellipse((cx - w * 0.19, h * 0.19, cx + w * 0.19, h * 0.58), fill=fg)
    if hair == "turban":
        d.ellipse((cx - w * 0.23, h * 0.09, cx + w * 0.23, h * 0.33), fill=hc)
    return im


def ghost_portrait(size: tuple[int, int], hair: str) -> Image.Image:
    """Faint secondary portrait (a 'ghost image' security feature) of the holder."""
    return portrait(size, (0, 0, 0, 0), (80, 96, 118, 64), hair=hair, hair_colour=(52, 62, 84, 118))


def rotated_stamp(lines: list[str], colour, *, angle: float = -8, size: int = 18, pad: int = 14) -> Image.Image:
    """Rubber-stamp style box (e.g. a JP certification) returned as an RGBA image."""
    f = font(size, "sans", True)
    tmp = ImageDraw.Draw(Image.new("RGBA", (10, 10)))
    widths = [text_width(tmp, ln, f) for ln in lines]
    line_h = size + 8
    w = max(widths) + pad * 2
    h = line_h * len(lines) + pad * 2 - 6
    st = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(st)
    d.rounded_rectangle((2, 2, w - 3, h - 3), radius=8, outline=colour + (200,), width=3)
    for i, ln in enumerate(lines):
        d.text((pad + (max(widths) - widths[i]) / 2, pad + i * line_h - 2), ln, font=f, fill=colour + (210,))
    return st.rotate(angle, expand=True, resample=Image.Resampling.BICUBIC)


def specimen(img: Image.Image, *, opacity: int = 70) -> Image.Image:
    """Diagonal 'SPECIMEN — NOT A REAL DOCUMENT' watermark plus a corner tag."""
    w, h = img.size
    angle = math.degrees(math.atan2(h, w)) * 0.75
    tmp = ImageDraw.Draw(Image.new("RGBA", (10, 10)))
    size = int(min(w, h) * 0.085)
    while True:
        f = font(size, "sans", True)
        bbox = tmp.textbbox((0, 0), SPECIMEN, font=f)
        tw, th = bbox[2] - bbox[0], bbox[3] - bbox[1]
        rad = math.radians(angle)
        rot_w = tw * math.cos(rad) + th * math.sin(rad)
        rot_h = tw * math.sin(rad) + th * math.cos(rad)
        if (rot_w <= w * 0.9 and rot_h <= h * 0.9) or size <= 18:
            break
        size -= 2
    timg = Image.new("RGBA", (tw + 40, th + 40), (0, 0, 0, 0))
    ImageDraw.Draw(timg).text((20 - bbox[0], 20 - bbox[1]), SPECIMEN, font=f, fill=RED + (opacity,))
    rot = timg.rotate(angle, expand=True, resample=Image.Resampling.BICUBIC)
    overlay = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    overlay.paste(rot, ((w - rot.width) // 2, (h - rot.height) // 2), rot)
    out = Image.alpha_composite(img, overlay)

    d = ImageDraw.Draw(out)
    tag_font = font(15, "sans", True)
    tag_w = text_width(d, SPECIMEN, tag_font) + 20
    d.rounded_rectangle((w - tag_w - 14, h - 40, w - 14, h - 12), radius=6, fill=(254, 226, 226, 255),
                        outline=RED + (255,), width=2)
    d.text((w - tag_w - 4, h - 35), SPECIMEN, font=tag_font, fill=RED + (255,))
    return out


def save(img: Image.Image, name: str) -> None:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    img.convert("RGB").save(OUT_DIR / name, "PNG", optimize=True)


def dmy(d: date) -> str:
    return d.strftime("%d/%m/%Y")


def dmy_long(d: date) -> str:
    return f"{d.day} {d.strftime('%B %Y')}"


def dmy_mrz(d: date) -> str:
    return d.strftime("%d %b %Y").upper()


def money(v: float) -> str:
    return f"${v:,.2f}"


def whole(v: float) -> str:
    return f"${v:,.0f}"


# ───────────────────────── MRZ (ICAO 9303, TD3) ─────────────────────────
def mrz_check_digit(field: str) -> str:
    weights = (7, 3, 1)
    total = 0
    for i, ch in enumerate(field):
        if ch.isdigit():
            v = int(ch)
        elif ch.isalpha():
            v = ord(ch.upper()) - 55
        else:
            v = 0
        total += v * weights[i % 3]
    return str(total % 10)


def td3_mrz(*, surname: str, given: str, number: str, nationality: str, dob: date, sex: str,
            expiry: date, issuing_state: str = "AUS") -> tuple[str, str]:
    name = f"{surname.upper()}<<{given.upper().replace(' ', '<')}"
    line1 = (f"P<{issuing_state}{name}" + "<" * 44)[:44]
    num = (number.upper() + "<" * 9)[:9]
    dob_s = dob.strftime("%y%m%d")
    exp_s = expiry.strftime("%y%m%d")
    personal = "<" * 14
    line2 = (num + mrz_check_digit(num) + nationality + dob_s + mrz_check_digit(dob_s) + sex
             + exp_s + mrz_check_digit(exp_s) + personal + "<")
    composite = line2[0:10] + line2[13:20] + line2[21:43]
    line2 += mrz_check_digit(composite)
    assert len(line1) == 44 and len(line2) == 44
    return line1, line2


# ───────────────────────── Fictional identifiers ─────────────────────────
def acn_check_ok(acn: str) -> bool:
    digits = [int(c) for c in acn if c.isdigit()]
    total = sum(a * b for a, b in zip(digits[:8], (8, 7, 6, 5, 4, 3, 2, 1)))
    return (10 - total % 10) % 10 == digits[8]


def abn_check_ok(abn: str) -> bool:
    digits = [int(c) for c in abn if c.isdigit()]
    digits[0] -= 1
    weights = (10, 1, 3, 5, 7, 9, 11, 13, 15, 17, 19)
    return sum(a * b for a, b in zip(digits, weights)) % 89 == 0


def certified_copy_canvas(w: int, h: int, certified: bool) -> Image.Image:
    """A certified copy is a photocopy on paper with the JP stamp beside the
    document, so certified documents get a white page with room for the stamp."""
    if not certified:
        return scan_bed(w, h)
    return Image.new("RGBA", (w, h + 150), (250, 250, 248, 255))


def jp_stamp(img: Image.Image, sighted: date, top: int, left: int = 40) -> None:
    st = rotated_stamp(["CERTIFIED TRUE COPY OF THE ORIGINAL", "A. Nguyen JP (Vic) · Reg. 14422",
                        f"Sighted {dmy(sighted)}"], (29, 78, 216), size=16, angle=3)
    img.alpha_composite(st, (left, top + 8))


# ───────────────────────── Driver licence (Victoria-style) ─────────────────────────
def driver_licence(name: str, p: dict, *, tampered_photo: bool = False) -> None:
    W, H = 980, 620
    img = certified_copy_canvas(W, H, bool(p.get("certified")))
    card = (22, 22, W - 22, H - 22)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle(card, radius=26, fill=(243, 248, 246, 255), outline=(150, 170, 170, 255), width=2)
    guilloche(d, (40, 120, W - 40, H - 60), (186, 220, 214, 255), rows=14, amp=6, period=90)

    # Header band
    d.rounded_rectangle((22, 22, W - 22, 116), radius=26, fill=(13, 79, 95, 255))
    d.rectangle((22, 80, W - 22, 116), fill=(13, 79, 95, 255))
    d.text((50, 36), "VICTORIA · AUSTRALIA", font=font(17, "sans", True), fill=(186, 230, 253, 255))
    d.text((50, 60), "DRIVER LICENCE", font=font(38, "sans", True), fill=WHITE)
    d.rounded_rectangle((W - 200, 48, W - 50, 88), radius=8, fill=(253, 230, 138, 255))
    d.text((W - 185, 56), "SPECIMEN", font=font(22, "sans", True), fill=(120, 53, 15, 255))

    # Photo, hologram band across its lower corner, and the ghost image.
    photo_box = (52, 140, 292, 430)
    pw, ph = photo_box[2] - photo_box[0], photo_box[3] - photo_box[1]
    genuine = portrait((pw, ph), (205, 222, 230, 255), (71, 85, 105, 255), hair=p.get("hair", "short"),
                       hair_colour=p.get("hair_colour"))
    img.alpha_composite(genuine, (photo_box[0], photo_box[1]))
    d.rectangle(photo_box, outline=(71, 85, 105, 255), width=2)
    # The hologram band runs across the lower part of the photo and past both edges.
    hologram(img, (30, 392, 324, 436))
    d.text((36, 440), "HOLOGRAM", font=font(11, "sans", True), fill=MUTED + (255,))
    img.alpha_composite(ghost_portrait((84, 104), p.get("hair", "short")), (W - 140, 404))
    d.text((W - 140, 512), "GHOST IMAGE", font=font(10, "sans", True), fill=MUTED + (255,))

    if tampered_photo:
        # Replacement photo pasted over the original: a patched, guilloche-free
        # margin, a white cut-out halo, a different backdrop and lighting, a
        # slight rotation — and it cuts straight through the hologram band,
        # leaving only stubs of the band either side of the photo.
        d.rectangle((photo_box[0] - 8, photo_box[1] - 8, photo_box[2] + 10, photo_box[3] + 8),
                    fill=(249, 250, 247, 255))
        swap = portrait((pw - 4, ph - 2), (226, 211, 186, 255), (92, 66, 52, 255), hair="short",
                        hair_colour=(58, 38, 28, 255))
        halo = Image.new("RGBA", (swap.width + 12, swap.height + 12), WHITE)
        halo.alpha_composite(swap, (6, 6))
        ImageDraw.Draw(halo).rectangle((0, 0, halo.width - 1, halo.height - 1), outline=(205, 205, 205, 255))
        halo = halo.rotate(1.8, expand=True, resample=Image.Resampling.BICUBIC, fillcolor=(0, 0, 0, 0))
        img.alpha_composite(halo, (photo_box[0] - 4, photo_box[1] - 6))

    # Fields
    lf = font(13, "sans", True)
    x, y = 330, 140

    def field(label: str, value: str, fx: int, fy: int, *, size: int = 25) -> None:
        d.text((fx, fy), label, font=lf, fill=MUTED + (255,))
        d.text((fx, fy + 18), value, font=font(size, "sans", True), fill=INK + (255,))

    field("NAME", p["name"].upper(), x, y)
    field("ADDRESS", p["address"].upper(), x, y + 64, size=18)
    field("LICENCE NO.", p["licence_no"], x, y + 128)
    field("DATE OF BIRTH", dmy(p["dob"]), x + 300, y + 128)
    field("ISSUE DATE", dmy(p["issue"]), x, y + 192)
    field("LICENCE EXPIRY", dmy(p["expiry"]), x + 300, y + 192)
    field("LICENCE TYPE", "CAR", x, y + 256)
    field("CONDITIONS", "NIL", x + 300, y + 256)

    # Signature (shrunk to fit the signature panel)
    d.text((60, 470), "Signature", font=font(12, "sans", True), fill=MUTED + (255,))
    sig_size = 30
    while sig_size > 18 and text_width(d, p["signature"], font(sig_size, "script")) > 240:
        sig_size -= 2
    d.text((60, 488 + (30 - sig_size) // 2), p["signature"], font=font(sig_size, "script"), fill=(30, 41, 59, 255))
    d.line((60, 532, 300, 532), fill=(148, 163, 184, 255), width=1)

    # Microprint border + issuer footer
    micro = ("ROADLICENSINGVICTORIA" * 12)[:150]
    d.text((50, H - 46), micro, font=font(8, "sans"), fill=(120, 150, 150, 255))
    d.text((330, 500), "Issued by Road Licensing Victoria", font=font(17, "sans", True), fill=(30, 41, 59, 255))
    d.text((330, 526), "Security features: hologram band · ghost image · microprint border",
           font=font(13, "sans"), fill=MUTED + (255,))
    d.text((330, 546), "Specimen layout — not a real government design", font=font(13, "sans"),
           fill=MUTED + (255,))

    if p.get("certified"):
        jp_stamp(img, p["certified"], H)

    save(specimen(img), name)


# ───────────────────────── Passport bio page (Australia-style) ─────────────────────────
def passport(name: str, p: dict) -> tuple[str, str]:
    W, H = 1000, 720
    img = certified_copy_canvas(W, H, bool(p.get("certified")))
    d = ImageDraw.Draw(img)
    page = (22, 22, W - 22, H - 22)
    d.rounded_rectangle(page, radius=18, fill=(248, 243, 228, 255), outline=(170, 160, 140, 255), width=2)
    guilloche(d, (40, 110, W - 40, 520), (214, 200, 230, 255), rows=16, amp=8, period=110)
    guilloche(d, (40, 110, W - 40, 520), (200, 226, 214, 255), rows=9, amp=12, period=160, phase=1.7)

    d.text((48, 38), "PASSPORT", font=font(36, "serif", True), fill=(30, 27, 75, 255))
    d.text((270, 50), "AUSTRALIA  ·  specimen layout", font=font(19, "sans", True), fill=(76, 29, 149, 255))
    d.rounded_rectangle((W - 210, 40, W - 48, 80), radius=8, fill=(253, 230, 138, 255))
    d.text((W - 196, 48), "SPECIMEN", font=font(22, "sans", True), fill=(120, 53, 15, 255))

    photo_box = (52, 120, 292, 440)
    pw, ph = photo_box[2] - photo_box[0], photo_box[3] - photo_box[1]
    main = portrait((pw, ph), (215, 226, 236, 255), (55, 65, 81, 255), hair=p.get("hair", "short"),
                    hair_colour=p.get("hair_colour"))
    img.alpha_composite(main, (photo_box[0], photo_box[1]))
    d.rectangle(photo_box, outline=(55, 65, 81, 255), width=2)
    img.alpha_composite(ghost_portrait((110, 140), p.get("hair", "short")), (W - 160, 110))

    lf = font(13, "sans", True)
    vf = font(23, "sans", True)

    def field(label: str, value: str, fx: int, fy: int) -> None:
        d.text((fx, fy), label.upper(), font=lf, fill=(107, 114, 128, 255))
        d.text((fx, fy + 16), value, font=vf, fill=(30, 27, 75, 255))

    x = 330
    field("Type", "P", x, 120)
    field("Code", "AUS", x + 90, 120)
    field("Passport No.", p["number"], x + 210, 120)
    field("Surname", p["surname"].upper(), x, 178)
    field("Given names", p["given"].upper(), x, 236)
    field("Nationality", "AUSTRALIAN", x, 294)
    field("Date of birth", dmy_mrz(p["dob"]), x, 352)
    field("Sex", p["sex"], x + 300, 352)
    field("Date of issue", dmy_mrz(p["issue"]), x, 410)
    field("Date of expiry", dmy_mrz(p["expiry"]), x + 300, 410)
    d.text((x, 470), "Authority: AUSTRALIA (specimen)   ·   Specimen layout — not a real government design",
           font=font(13, "sans"), fill=(107, 114, 128, 255))

    line1, line2 = td3_mrz(surname=p["surname"], given=p["given"], number=p["number"], nationality="AUS",
                           dob=p["dob"], sex=p["sex"], expiry=p["expiry"])
    d.rectangle((40, H - 170, W - 40, H - 60), fill=(255, 251, 235, 255))
    mf = font(29, "mono", True)
    d.text((56, H - 160), line1, font=mf, fill=(30, 27, 75, 255))
    d.text((56, H - 116), line2, font=mf, fill=(30, 27, 75, 255))

    if p.get("certified"):
        jp_stamp(img, p["certified"], H)

    save(specimen(img), name)
    return line1, line2


# ───────────────────────── Payslip ─────────────────────────
def payslip(name: str, s: dict) -> None:
    W, H = 900, 1140
    img = paper(W, H)
    d = ImageDraw.Draw(img)
    brand = s["brand_colour"]
    d.rectangle((0, 0, W, 118), fill=brand + (255,))
    d.ellipse((44, 30, 100, 86), outline=WHITE, width=5)
    d.line((58, 58, 86, 58), fill=WHITE, width=5)
    d.line((72, 44, 72, 72), fill=WHITE, width=5)
    d.text((120, 30), s["employer"].upper(), font=font(22, "sans", True), fill=WHITE)
    d.text((120, 68), s["employer_line"], font=font(15, "sans"), fill=(220, 236, 240, 255))
    d.text((W - 190, 62), "PAYSLIP", font=font(32, "sans", True), fill=WHITE)

    d.rounded_rectangle((48, 146, W - 48, 386), radius=10, fill=(248, 250, 252, 255), outline=(203, 213, 225, 255))
    left = [
        ("Employee", s["employee"]),
        ("Employee ID", s["employee_id"]),
        ("Position", s["position"]),
        ("Employment", s["employment"]),
    ]
    right = [
        ("Annual salary", s["annual_salary"]),
        ("Pay frequency", "Fortnightly"),
        ("Pay period", s["period"]),
        ("Pay date", s["pay_date"]),
    ]
    for col, rows in ((72, left), (480, right)):
        y = 166
        for k, v in rows:
            d.text((col, y), k, font=font(14, "sans", True), fill=MUTED + (255,))
            d.text((col, y + 18), v, font=font(19, "sans", True), fill=INK + (255,))
            y += 54

    def table(top: int, title: str, rows: list[tuple[str, str, str, str, str]], *, bold_last: bool = False) -> int:
        cols = (48, 330, 450, 590, 730)
        d.text((48, top), title, font=font(18, "sans", True), fill=brand + (255,))
        top += 30
        d.rectangle((48, top, W - 48, top + 34), fill=(241, 245, 249, 255))
        for cx, head in zip(cols, ("Description", "Hours", "Rate", "This pay", "Year to date")):
            d.text((cx + 10, top + 8), head, font=font(15, "sans", True), fill=INK + (255,))
        top += 44
        for i, row in enumerate(rows):
            bold = bold_last and i == len(rows) - 1
            f = font(16, "sans", bold)
            for cx, cell in zip(cols, row):
                if cell:
                    d.text((cx + 10, top), cell, font=f, fill=INK + (255,))
            top += 32
            d.line((48, top - 7, W - 48, top - 7), fill=(226, 232, 240, 255), width=1)
        return top + 18

    y = table(414, "Earnings", s["earnings"], bold_last=True)
    y = table(y, "Deductions", s["deductions"])
    d.rounded_rectangle((48, y, W - 48, y + 64), radius=10, fill=(236, 253, 245, 255), outline=(110, 231, 183, 255))
    d.text((72, y + 10), "Net pay", font=font(15, "sans", True), fill=MUTED + (255,))
    d.text((72, y + 30), s["net_pay"], font=font(22, "sans", True), fill=INK + (255,))
    d.text((300, y + 10), "Paid to", font=font(15, "sans", True), fill=MUTED + (255,))
    d.text((300, y + 31), s["paid_to"], font=font(18, "sans"), fill=INK + (255,))
    y += 92
    y = table(y, "Employer superannuation", s["super"])
    d.text((48, y), s["leave"], font=font(15, "sans"), fill=MUTED + (255,))
    d.text((48, H - 96), f"{s['employer']} is a fictional business. Specimen layout — not a real payslip.",
           font=font(14, "sans"), fill=MUTED + (255,))
    save(specimen(img), name)


# ───────────────────────── Specialist medical report ─────────────────────────
def letterhead(width: int, lines: tuple[str, str, str], colour) -> Image.Image:
    lh = Image.new("RGBA", (width, 132), (255, 255, 255, 255))
    d = ImageDraw.Draw(lh)
    d.rectangle((0, 0, 14, 132), fill=colour + (255,))
    d.ellipse((36, 26, 104, 94), outline=colour + (255,), width=6)
    d.arc((52, 42, 88, 78), 200, 520, fill=colour + (255,), width=5)
    d.text((126, 20), lines[0], font=font(28, "sans", True), fill=colour + (255,))
    d.text((126, 60), lines[1], font=font(17, "sans", True), fill=INK + (255,))
    d.text((126, 88), lines[2], font=font(15, "sans"), fill=MUTED + (255,))
    d.line((0, 128, width, 128), fill=colour + (255,), width=3)
    return lh


def medical_report(name: str, r: dict, *, fake: bool = False) -> None:
    W, H = 900, 1100
    img = paper(W, H)
    d = ImageDraw.Draw(img)
    colour = r["colour"]

    lh = letterhead(W - 96, r["letterhead"], colour)
    if fake:
        # Pasted letterhead: a low-resolution copy, on a slightly different
        # paper tone, a little rotated and off the page margin.
        small = lh.resize((lh.width // 3, lh.height // 3), Image.Resampling.BILINEAR)
        lh = small.resize((lh.width, lh.height), Image.Resampling.NEAREST)
        tint = Image.new("RGBA", lh.size, (246, 242, 230, 255))
        lh = Image.blend(lh, tint, 0.18)
        lh = lh.rotate(0.9, expand=True, resample=Image.Resampling.BICUBIC, fillcolor=(0, 0, 0, 0))
        img.alpha_composite(lh, (62, 30))
    else:
        img.alpha_composite(lh, (48, 36))

    body = font(18, "sans")
    bold = font(18, "sans", True)
    y = 196

    def date_line(label: str, value: str, yy: int, *, edited: bool) -> None:
        d.text((48, yy), label, font=bold, fill=INK + (255,))
        x = 48 + text_width(d, label + " ", bold)
        if edited:
            # Edited date: a white patch over the original and the digits
            # re-typed in a different typeface, weight and baseline.
            ef = font(19, "courier", True)
            tw = text_width(d, value, ef)
            d.rectangle((x - 4, yy - 3, x + tw + 6, yy + 24), fill=(252, 252, 249, 255),
                        outline=(236, 236, 230, 255))
            d.text((x + 1, yy + 3), value, font=ef, fill=(48, 44, 40, 255))
        else:
            d.text((x, yy), value, font=body, fill=INK + (255,))

    date_line("Date:", r["report_date"], y, edited=fake)
    y += 44
    d.text((48, y), r["addressee"], font=bold, fill=INK + (255,))
    y += 40
    d.text((48, y), "Re:", font=bold, fill=INK + (255,))
    d.text((92, y), r["patient"], font=bold, fill=INK + (255,))
    d.text((92, y + 26), r["patient_line"], font=body, fill=INK + (255,))
    y += 76

    for kind, label, text in r["sections"]:
        if kind == "date":
            date_line(label, text, y, edited=fake)
            y += 40
            continue
        d.text((48, y), label, font=font(15, "sans", True), fill=colour + (255,))
        y += 24
        if kind == "mismatch" and fake:
            # Font mismatch: this block is set in a different typeface and size.
            f = font(20, "times")
            for ln in text:
                d.text((52, y + 2), ln, font=f, fill=(30, 30, 30, 255))
                y += 27
        else:
            for ln in text:
                d.text((48, y), ln, font=body, fill=INK + (255,))
                y += 27
        y += 14

    y += 10
    d.text((48, y), r["sign_off"], font=body, fill=INK + (255,))
    d.text((48, y + 26), r["signature"], font=font(32, "script"), fill=(30, 41, 59, 255))
    d.line((48, y + 72, 330, y + 72), fill=(148, 163, 184, 255), width=1)
    d.text((48, y + 80), r["signatory"], font=bold, fill=INK + (255,))
    d.text((48, y + 106), r["signatory_line"], font=body, fill=MUTED + (255,))
    d.text((48, H - 96), r["footer"], font=font(14, "sans"), fill=MUTED + (255,))
    save(specimen(img), name)


# ───────────────────────── Bank statement ─────────────────────────
def bank_statement(name: str, s: dict) -> None:
    W, H = 900, 660 + 34 * len(s["transactions"]) + 170
    img = paper(W, H)
    d = ImageDraw.Draw(img)
    brand = s["brand_colour"]
    d.rectangle((0, 0, W, 120), fill=brand + (255,))
    d.polygon([(48, 92), (92, 28), (112, 72)], fill=(255, 255, 255, 230))
    d.text((128, 30), s["bank"].upper(), font=font(34, "sans", True), fill=WHITE)
    d.text((128, 76), s["bank_tagline"], font=font(16, "sans"), fill=(226, 232, 240, 255))

    d.text((48, 150), s["title"], font=font(28, "sans", True), fill=INK + (255,))
    d.text((48, 190), f"Statement period: {s['period']}", font=font(17, "sans"), fill=MUTED + (255,))
    d.text((W - 210, 190), "Page 1 of 1", font=font(17, "sans"), fill=MUTED + (255,))

    d.rounded_rectangle((48, 230, W - 48, 420), radius=10, fill=(248, 250, 252, 255), outline=(203, 213, 225, 255))
    rows = [
        ("Account name", s["account_name"]),
        ("Account type", s["account_type"]),
        ("BSB / account", s["bsb_account"]),
        (s["opened_label"], s["opened"]),
        ("Statement date", s["statement_date"]),
    ]
    y = 248
    for k, v in rows:
        d.text((72, y), k, font=font(16, "sans", True), fill=MUTED + (255,))
        d.text((300, y), v, font=font(19, "sans", True), fill=INK + (255,))
        y += 33

    y = 450
    cols = (48, 180, 560, 666, 766)
    d.rectangle((48, y, W - 48, y + 36), fill=(241, 245, 249, 255))
    for cx, head in zip(cols, ("Date", "Description", "Debit", "Credit", "Balance")):
        d.text((cx + 10, y + 8), head, font=font(16, "sans", True), fill=INK + (255,))
    y += 46
    tf = font(16, "sans")
    for dt, desc, debit, credit, bal in s["transactions"]:
        d.text((cols[0] + 10, y), dt, font=tf, fill=INK + (255,))
        d.text((cols[1] + 10, y), desc, font=tf, fill=INK + (255,))
        if debit:
            d.text((cols[2] + 10, y), debit, font=tf, fill=INK + (255,))
        if credit:
            d.text((cols[3] + 10, y), credit, font=tf, fill=INK + (255,))
        d.text((cols[4] + 10, y), bal, font=tf, fill=INK + (255,))
        y += 34
        d.line((48, y - 8, W - 48, y - 8), fill=(226, 232, 240, 255), width=1)

    y += 14
    d.text((48, y), f"Closing balance: {s['closing']}", font=font(20, "sans", True), fill=INK + (255,))
    d.text((48, H - 120), s["footer"], font=font(14, "sans"), fill=MUTED + (255,))
    d.text((48, H - 96), f"{s['bank']} is a fictional bank. Specimen layout — not a real statement.",
           font=font(14, "sans"), fill=MUTED + (255,))
    save(specimen(img), name)


# ───────────────────────── Company extract ─────────────────────────
def company_extract(name: str, c: dict) -> None:
    W, H = 900, 1240
    img = paper(W, H)
    d = ImageDraw.Draw(img)
    d.rectangle((0, 0, W, 108), fill=(30, 58, 95, 255))
    d.text((48, 24), "CURRENT COMPANY EXTRACT", font=font(30, "sans", True), fill=WHITE)
    d.text((48, 68), "Company register · specimen layout", font=font(17, "sans"), fill=(203, 213, 225, 255))
    d.text((W - 300, 30), f"Extracted {c['extracted']}", font=font(16, "sans", True), fill=WHITE)
    d.text((W - 300, 56), f"Extract no. {c['extract_no']}", font=font(15, "sans"), fill=(203, 213, 225, 255))

    d.text((48, 134), c["company"], font=font(30, "sans", True), fill=INK + (255,))
    d.text((48, 176), f"ACN {c['acn']}   ·   ABN {c['abn']}", font=font(19, "mono", True), fill=INK + (255,))

    def section(top: int, title: str) -> int:
        d.rectangle((48, top, W - 48, top + 34), fill=(226, 232, 240, 255))
        d.text((60, top + 7), title, font=font(17, "sans", True), fill=(30, 58, 95, 255))
        return top + 46

    y = section(222, "Organisation details")
    for k, v in c["details"]:
        d.text((64, y), k, font=font(15, "sans", True), fill=MUTED + (255,))
        d.text((330, y - 1), v, font=font(17, "sans"), fill=INK + (255,))
        y += 32

    y = section(y + 14, "Office holders")
    for role, who, born, appointed in c["officers"]:
        d.text((64, y), role, font=font(15, "sans", True), fill=MUTED + (255,))
        d.text((330, y - 1), who, font=font(17, "sans", True), fill=INK + (255,))
        d.text((330, y + 23), f"Born {born}   ·   Appointed {appointed}", font=font(15, "sans"),
               fill=INK + (255,))
        y += 58

    y = section(y + 8, "Share structure")
    d.text((64, y), c["shares"], font=font(17, "sans"), fill=INK + (255,))
    y += 40

    y = section(y, "Members (shareholders)")
    for who, holding in c["members"]:
        d.text((64, y), who, font=font(17, "sans", True), fill=INK + (255,))
        d.text((420, y), holding, font=font(16, "sans"), fill=INK + (255,))
        y += 32

    y = section(y + 10, "Ultimate holding company")
    d.text((64, y), "None recorded", font=font(17, "sans"), fill=INK + (255,))
    y += 50

    d.rounded_rectangle((48, y, W - 48, y + 92), radius=10, fill=(239, 246, 255, 255), outline=(147, 197, 253, 255))
    note = [
        "This extract lists the company's details, office holders and members as recorded",
        "on the register at the time of the search. It is not a valuation of the company.",
    ]
    for i, ln in enumerate(note):
        d.text((68, y + 18 + i * 26), ln, font=font(16, "sans"), fill=(30, 58, 138, 255))

    d.text((48, H - 96), "Specimen layout — not an official company register extract.",
           font=font(14, "sans"), fill=MUTED + (255,))
    save(specimen(img), name)


# ───────────────────────── BAS and profit and loss summary ─────────────────────────
def earnings_summary(name: str, e: dict) -> None:
    W, H = 900, 1280
    img = paper(W, H)
    d = ImageDraw.Draw(img)
    brand = e["brand_colour"]
    d.rectangle((0, 0, W, 112), fill=brand + (255,))
    d.text((48, 24), e["preparer"].upper(), font=font(28, "sans", True), fill=WHITE)
    d.text((48, 66), e["preparer_line"], font=font(16, "sans"), fill=(220, 240, 236, 255))

    d.text((48, 134), e["title"], font=font(25, "sans", True), fill=INK + (255,))
    d.text((48, 170), e["subtitle"], font=font(17, "sans"), fill=MUTED + (255,))

    d.rounded_rectangle((48, 206, W - 48, 330), radius=10, fill=(248, 250, 252, 255), outline=(203, 213, 225, 255))
    y = 220
    for k, v in e["client"]:
        d.text((72, y), k, font=font(15, "sans", True), fill=MUTED + (255,))
        d.text((300, y - 1), v, font=font(17, "sans", True), fill=INK + (255,))
        y += 27

    # BAS lodgement summary
    y = 360
    d.text((48, y), "BAS lodgement summary (quarterly)", font=font(19, "sans", True), fill=brand + (255,))
    y += 32
    cols = (48, 300, 520, 700)
    d.rectangle((48, y, W - 48, y + 34), fill=(241, 245, 249, 255))
    for cx, head in zip(cols, ("Quarter", "G1 total sales (incl. GST)", "1A GST on sales", "Lodged")):
        d.text((cx + 10, y + 8), head, font=font(15, "sans", True), fill=INK + (255,))
    y += 44
    for quarter, sales, gst, lodged in e["bas"]:
        for cx, cell in zip(cols, (quarter, sales, gst, lodged)):
            d.text((cx + 10, y), cell, font=font(16, "sans"), fill=INK + (255,))
        y += 32
        d.line((48, y - 7, W - 48, y - 7), fill=(226, 232, 240, 255), width=1)
    for cx, cell in zip(cols, ("Total", e["bas_total_sales"], e["bas_total_gst"], "")):
        d.text((cx + 10, y), cell, font=font(16, "sans", True), fill=INK + (255,))
    y += 50

    # Profit and loss
    d.text((48, y), "Profit and loss (GST exclusive)", font=font(19, "sans", True), fill=brand + (255,))
    y += 34
    for label, value, strong in e["pl"]:
        f = font(17, "sans", strong)
        d.text((64, y), label, font=f, fill=INK + (255,))
        d.text((W - 48 - text_width(d, value, f) - 16, y), value, font=f, fill=INK + (255,))
        y += 30
        if strong:
            d.line((48, y - 6, W - 48, y - 6), fill=(203, 213, 225, 255), width=1)
    y += 18

    for ln in e["notes"]:
        d.text((48, y), ln, font=font(15, "sans"), fill=MUTED + (255,))
        y += 24
    y += 16
    d.text((48, y), e["signature"], font=font(28, "script"), fill=(30, 41, 59, 255))
    d.line((48, y + 46, 330, y + 46), fill=(148, 163, 184, 255), width=1)
    d.text((48, y + 54), e["signatory"], font=font(16, "sans", True), fill=INK + (255,))
    d.text((48, H - 96), f"{e['preparer']} and {e['business']} are fictional. Specimen layout — not a real document.",
           font=font(14, "sans"), fill=MUTED + (255,))
    save(specimen(img), name)


# ───────────────────────── Death certificate (certified copy) ─────────────────────────
def death_certificate(name: str, c: dict) -> None:
    W, H = 900, 1300
    img = Image.new("RGBA", (W, H), (253, 251, 245, 255))
    d = ImageDraw.Draw(img)
    d.rectangle((20, 20, W - 20, H - 20), outline=(120, 113, 92, 255), width=3)
    d.rectangle((32, 32, W - 32, H - 32), outline=(180, 170, 140, 255), width=1)
    guilloche(d, (40, 40, W - 40, 150), (226, 218, 196, 255), rows=6, amp=5, period=80)

    def centred(text: str, y: int, f, fill) -> None:
        d.text(((W - text_width(d, text, f)) / 2, y), text, font=f, fill=fill)

    centred("BIRTHS, DEATHS AND MARRIAGES — VICTORIA", 60, font(24, "serif", True), (68, 64, 60, 255))
    centred("DEATH CERTIFICATE", 100, font(40, "serif", True), INK + (255,))
    centred("Specimen layout — not a real certificate", 152, font(15, "sans"), MUTED + (255,))
    d.text((60, 196), f"Registration number  {c['registration']}", font=font(20, "mono", True), fill=INK + (255,))

    y = 246
    for k, v in c["rows"]:
        d.text((60, y), k, font=font(16, "sans", True), fill=MUTED + (255,))
        d.text((330, y - 2), v, font=font(19, "serif"), fill=INK + (255,))
        d.line((60, y + 34, W - 60, y + 34), fill=(226, 220, 205, 255), width=1)
        y += 48

    y += 30
    d.text((60, y), "Registrar", font=font(14, "sans", True), fill=MUTED + (255,))
    d.text((60, y + 18), "M. Okafor", font=font(30, "script"), fill=(30, 41, 59, 255))
    d.line((60, y + 66, 330, y + 66), fill=(148, 163, 184, 255), width=1)
    seal = (W - 250, y - 20, W - 90, y + 140)
    d.ellipse(seal, outline=(161, 98, 7, 255), width=4)
    d.ellipse((seal[0] + 14, seal[1] + 14, seal[2] - 14, seal[3] - 14), outline=(161, 98, 7, 255), width=2)
    sf = font(15, "sans", True)
    for i, ln in enumerate(("REGISTRY", "SEAL", "SPECIMEN")):
        d.text(((seal[0] + seal[2]) / 2 - text_width(d, ln, sf) / 2, seal[1] + 52 + i * 20), ln, font=sf,
               fill=(161, 98, 7, 255))

    st = rotated_stamp(["CERTIFIED TRUE COPY OF THE ORIGINAL", "A. Nguyen JP (Vic) · Reg. 14422",
                        f"Sighted {c['certified']}"], (29, 78, 216), size=17, angle=6)
    img.alpha_composite(st, (60, H - st.height - 70))
    d.text((60, H - 60), "Security features: certified-copy stamp · registry seal · guilloche border",
           font=font(13, "sans"), fill=MUTED + (255,))
    save(specimen(img), name)


# ───────────────────────── Case data (spec §4) ─────────────────────────
PRIYA_DL = {
    "name": "Priya Natarajan",
    "address": "9 Calloway Walk, Coburg VIC 3058",
    "licence_no": "071 330 958",
    "dob": date(1992, 2, 14),
    "issue": date(2024, 2, 14),
    "expiry": date(2034, 2, 14),
    "signature": "P. Natarajan",
    "hair": "long",
    "hair_colour": (30, 27, 30, 255),
}
PRIYA_PAYSLIP = {
    "employer": "Brunswick Movement Physiotherapy Pty Ltd",
    "employer_line": "ABN 51 824 753 558  ·  Brunswick VIC 3056  ·  (03) 5550 3120",
    "brand_colour": (14, 116, 144),
    "employee": "Priya Natarajan",
    "employee_id": "BMP-0142",
    "position": "Physiotherapist",
    "employment": "Full time (since 2019)",
    "annual_salary": "$105,000.00 + super",
    "period": "12/10/2026 – 25/10/2026",
    "pay_date": "29/10/2026",
    "earnings": [
        ("Ordinary hours", "76.00", "$53.1377", "$4,038.46", "$36,346.14"),
        ("Gross pay", "", "", "$4,038.46", "$36,346.14"),
    ],
    "deductions": [
        ("PAYG tax withheld", "", "", "$928.00", "$8,352.00"),
    ],
    "net_pay": "$3,110.46",
    "paid_to": "P Natarajan · account ending 3317",
    "super": [
        ("Employer contribution — Riverbend Super", "", "", "$484.62", "$4,361.58"),
    ],
    "leave": "Leave balances at 25/10/2026: annual leave 98.6 hours · personal leave 61.2 hours",
}

GRACE_DL = {
    "name": "Grace Williams",
    "address": "21 Wexcombe Avenue, Hawthorn VIC 3122",
    "licence_no": "038 641 207",
    "dob": date(1965, 7, 22),
    "issue": date(2021, 8, 16),
    "expiry": date(2031, 8, 16),
    "signature": "G. Williams",
    "hair": "long",
    "hair_colour": (148, 148, 160, 255),
}
GRACE_REPORT = {
    "colour": (126, 34, 206),
    "letterhead": ("EASTERN ONCOLOGY GROUP",
                   "Dr A. Moretti  ·  Consultant Medical Oncologist",
                   "Box Hill VIC 3128  ·  (03) 5550 4471  ·  Provider No. EOG-4471-M"),
    "report_date": "02/11/2026",
    "addressee": "TO WHOM IT MAY CONCERN",
    "patient": "Mrs Grace Williams  ·  DOB 22/07/1965",
    "patient_line": "21 Wexcombe Avenue, Hawthorn VIC 3122",
    "sections": [
        ("date", "Consultation:", "29/10/2026"),
        ("text", "HISTORY", [
            "Mrs Williams presented with a lump in the left breast. She was examined in",
            "my rooms and sent for imaging the same day.",
        ]),
        ("text", "INVESTIGATIONS", [
            "Mammogram and ultrasound (left breast).",
            "Pathology: results to follow.",
        ]),
        ("mismatch", "DIAGNOSIS", [
            "Invasive breast carcinoma, left breast, stage II.",
            "Chemotherapy to commence urgently.",
        ]),
        ("text", "REQUEST", [
            "Mrs Williams is unable to work and needs funds for treatment. I ask that her",
            "insurance benefit be paid as a matter of urgency.",
        ]),
    ],
    "sign_off": "Yours faithfully,",
    "signature": "A. Moretti",
    "signatory": "Dr A. Moretti",
    "signatory_line": "Consultant Medical Oncologist  ·  Provider No. EOG-4471-M",
    "footer": "Eastern Oncology Group is fictional. Specimen layout — not a real medical report.",
}
GRACE_STATEMENT = {
    "bank": "Laneway Bank",
    "bank_tagline": "Business banking",
    "brand_colour": (55, 48, 163),
    "title": "Interim statement — Business Transaction Account",
    "period": "03 Nov 2026 to 05 Nov 2026",
    "account_name": "G WILLIAMS CONSULTING PTY LTD",
    "account_type": "Business Transaction Account",
    "bsb_account": "063-***   ****4471",
    "opened_label": "Account opened",
    "opened": "03 Nov 2026",
    "statement_date": "05 Nov 2026 (printed on request)",
    "transactions": [
        ("03 Nov 2026", "Account opened — opening deposit (cash)", "", "10.00", "10.00"),
        ("05 Nov 2026", "Closing balance", "", "", "10.00"),
    ],
    "closing": "$10.00",
    "footer": "Interim statement printed on request. No other transactions in the period.",
}

GEORGE_DL = {
    "name": "George Papadopoulos",
    "address": "33 Penhallow Street, Thornbury VIC 3071",
    "licence_no": "029 554 871",
    "dob": date(1974, 4, 19),
    "issue": date(2022, 4, 19),
    "expiry": date(2032, 4, 19),
    "signature": "G. Papadopoulos",
    "hair": "short",
    "hair_colour": (58, 52, 50, 255),
}
HARPREET_PP = {
    "surname": "Kaur",
    "given": "Harpreet",
    "number": "PK5513408",
    "dob": date(1979, 9, 8),
    "sex": "F",
    "issue": date(2023, 5, 22),
    "expiry": date(2033, 5, 22),
    "hair": "long",
    "hair_colour": (28, 24, 28, 255),
}
NORTHCOTE_EXTRACT = {
    "company": "NORTHCOTE CUSTOM JOINERY PTY LTD",
    "acn": "151 446 228",
    "abn": "33 151 446 228",
    "extracted": "09/11/2026 14:32",
    "extract_no": "CX-26-110917",
    "details": [
        ("Type", "Australian proprietary company, limited by shares"),
        ("Status", "Registered"),
        ("Registered on", "14/03/2011"),
        ("Registered office", "Unit 3, 41 Tannery Lane, Northcote VIC 3070"),
        ("Principal place of business", "Unit 3, 41 Tannery Lane, Northcote VIC 3070"),
    ],
    "officers": [
        ("Director", "PAPADOPOULOS, GEORGE — Thornbury VIC 3071", "19/04/1974", "14/03/2011"),
        ("Director", "KAUR, HARPREET — Preston VIC 3072", "08/09/1979", "14/03/2011"),
        ("Secretary", "KAUR, HARPREET", "08/09/1979", "14/03/2011"),
    ],
    "shares": "Ordinary (ORD): 100 shares issued, fully paid ($100.00)",
    "members": [
        ("PAPADOPOULOS, GEORGE", "50 ORD · fully paid · beneficially held"),
        ("KAUR, HARPREET", "50 ORD · fully paid · beneficially held"),
    ],
}

TOM_EARNINGS = {
    "preparer": "Tidewater Accounting",
    "preparer_line": "Tax agents and bookkeepers  ·  Frankston VIC 3199  ·  (03) 5550 2186",
    "brand_colour": (15, 118, 110),
    "business": "Bradley Electrical",
    "title": "Bradley Electrical — earnings summary",
    "subtitle": "BAS and profit and loss for the 12 months to 30 September 2026",
    "client": [
        ("Business", "Bradley Electrical (sole trader)"),
        ("Owner", "Thomas (Tom) Bradley  ·  ABN 62 418 905 337"),
        ("Prepared", "10/11/2026, at the client's request, for an income protection claim"),
        ("Period", "01/10/2025 – 30/09/2026"),
    ],
    "bas": [
        ("Oct–Dec 2025", "$49,280", "$4,480", "20/02/2026"),
        ("Jan–Mar 2026", "$47,960", "$4,360", "22/04/2026"),
        ("Apr–Jun 2026", "$51,040", "$4,640", "21/07/2026"),
        ("Jul–Sep 2026", "$52,360", "$4,760", "27/10/2026"),
    ],
    "bas_total_sales": "$200,640",
    "bas_total_gst": "$18,240",
    "pl": [
        ("Sales income", "$182,400", True),
        ("Materials and supplies", "($46,800)", False),
        ("Vehicle (fuel, registration, servicing)", "($14,200)", False),
        ("Tools and equipment", "($7,400)", False),
        ("Business insurance", "($4,900)", False),
        ("Phone, software and administration", "($3,600)", False),
        ("Accounting and bookkeeping", "($3,100)", False),
        ("Licences, training and memberships", "($2,400)", False),
        ("Depreciation", "($4,000)", False),
        ("Total expenses", "($86,400)", True),
        ("Net business income", "$96,000", True),
    ],
    "notes": [
        "Sales of $182,400 (GST exclusive) reconcile to total BAS sales of $200,640 including GST.",
        "Prepared from the business's accounting records and the four BAS lodgements above.",
    ],
    "signature": "J. Okoro",
    "signatory": "J. Okoro, registered tax agent, Tidewater Accounting",
}
TOM_REPORT = {
    "colour": (3, 105, 161),
    "letterhead": ("SEAFORD SHOULDER & UPPER LIMB CLINIC",
                   "Dr Hamish Calder  ·  Orthopaedic Surgeon",
                   "Seaford VIC 3198  ·  (03) 5550 7314  ·  Provider No. 2716453L"),
    "report_date": "06/11/2026",
    "addressee": "Claims Assessor, Harbourline Life — policy HL-23051877",
    "patient": "Mr Thomas (Tom) Bradley  ·  DOB 03/03/1985",
    "patient_line": "18 Saltbush Close, Frankston VIC 3199  ·  Occupation: electrician (self-employed)",
    "sections": [
        ("text", "HISTORY", [
            "On 14/10/2026 Mr Bradley fell from a ladder while working and injured his",
            "right shoulder. He was unable to lift his arm above shoulder height.",
        ]),
        ("text", "INVESTIGATIONS", [
            "MRI right shoulder, 16/10/2026: full-thickness tear of the supraspinatus",
            "tendon (rotator cuff).",
        ]),
        ("text", "DIAGNOSIS AND TREATMENT", [
            "Torn rotator cuff, right shoulder. Arthroscopic rotator cuff repair performed",
            "on 21/10/2026. Sling for six weeks; physiotherapy has started.",
        ]),
        ("text", "CAPACITY FOR WORK", [
            "Mr Bradley can't do his usual work as an electrician (overhead work, ladders,",
            "lifting). I expect him to be off work for four to six months. Next review",
            "02/12/2026.",
        ]),
    ],
    "sign_off": "Yours sincerely,",
    "signature": "Hamish Calder",
    "signatory": "Dr Hamish Calder",
    "signatory_line": "Orthopaedic Surgeon  ·  Provider No. 2716453L",
    "footer": "Seaford Shoulder & Upper Limb Clinic is fictional. Specimen layout — not a real medical report.",
}

MEI_CERT = {
    "registration": "2026/041857",
    "rows": [
        ("Surname", "CHEN"),
        ("Given names", "Mei"),
        ("Sex", "Female"),
        ("Date of death", "12 October 2026"),
        ("Place of death", "Box Hill, Victoria"),
        ("Cause of death", "Subarachnoid haemorrhage; ruptured cerebral aneurysm"),
        ("Date of birth", "5 June 1984"),
        ("Age", "42 years"),
        ("Occupation", "Pharmacist"),
        ("Usual residence", "46 Fernhurst Rise, Box Hill VIC 3128"),
        ("Marital status", "Married — spouse Robert Chen"),
        ("Children (name, age)", "Lily (12), Max (9)"),
        ("Informant", "Robert Chen, husband"),
        ("Date of registration", "19 October 2026"),
    ],
    "certified": "11/11/2026",
}
ROBERT_DL = {
    "name": "Robert Chen",
    "address": "46 Fernhurst Rise, Box Hill VIC 3128",
    "licence_no": "046 218 395",
    "dob": date(1982, 1, 30),
    "issue": date(2022, 1, 30),
    "expiry": date(2032, 1, 30),
    "signature": "Robert Chen",
    "hair": "short",
    "hair_colour": (25, 25, 30, 255),
    "certified": date(2026, 11, 11),
}


# ───────────────────────── Manifest ─────────────────────────
def manifest(harpreet_mrz: tuple[str, str]) -> list[dict]:
    return [
        # Scenario 1 — Priya Natarajan (new family protection advice)
        {
            "id": "dl-priya",
            "label": "Driver licence — Priya Natarajan",
            "kind": "driver-licence",
            "src": SRC_PREFIX + "driver-licence-priya-natarajan.png",
            "expected": "legit",
            "owner": "priya",
            "description": "Victoria-style specimen licence supplied for Priya's identity check when she became a "
                           "client. Verified through the Document Verification Service (DVS) on 03/11/2026.",
            "holderName": "Priya Natarajan",
            "documentNumber": "071 330 958",
            "issuer": "Road Licensing Victoria",
            "issueDate": "2024-02-14",
            "expiryDate": "2034-02-14",
            "dateOfBirth": "1992-02-14",
            "securityFeatures": "yes",
            "tamperIndicators": "none",
            "electronicVerification": "Match — DVS, 03/11/2026",
        },
        {
            "id": "payslip-priya",
            "label": "Payslip — Priya Natarajan (pay date 29/10/2026)",
            "kind": "payslip",
            "src": SRC_PREFIX + "payslip-priya-natarajan.png",
            "expected": "legit",
            "owner": "priya",
            "description": "Fortnightly payslip from her employer, a private physiotherapy clinic in Brunswick: "
                           "annual salary $105,000 plus super, matching the income in her fact find.",
            "holderName": "Priya Natarajan",
            "documentNumber": "BMP-0142",
            "issuer": "Brunswick Movement Physiotherapy Pty Ltd",
            "issueDate": "2026-10-29",
            "periodStart": "2026-10-12",
            "periodEnd": "2026-10-25",
            "keyAmount": 105000,
            "keyAmountLabel": "Annual salary",
            "securityFeatures": "n/a",
            "tamperIndicators": "none",
        },
        # Scenario 2 — Grace Williams (identity takeover and a fake trauma claim)
        {
            "id": "oncology-report-grace",
            "label": "Oncologist's report — Dr A. Moretti, Eastern Oncology Group (sent with the claim)",
            "kind": "medical-report",
            "src": SRC_PREFIX + "oncology-report-grace-williams.png",
            "expected": "fake",
            "owner": "grace",
            "description": "Report attached to the trauma claim emailed on 06/11/2026. The letterhead is a pasted, "
                           "low-resolution image; both dates were edited; the diagnosis is in a different font; "
                           "the provider number isn't in the format of a Medicare provider number; and there is "
                           "no histopathology.",
            "holderName": "Grace Williams",
            "documentNumber": "EOG-4471-M",
            "issuer": "Eastern Oncology Group — Dr A. Moretti",
            "issueDate": "2026-11-02",
            "consultationDate": "2026-10-29",
            "dateOfBirth": "1965-07-22",
            "providerNumber": "EOG-4471-M",
            "diagnosisEvidence": "Mammogram and ultrasound only; pathology 'results to follow' — no histopathology "
                                 "report",
            "securityFeatures": "n/a",
            "tamperIndicators": "Letterhead pasted in as a low-resolution image on a different paper tone, rotated "
                                "and off the page margin; report and consultation dates edited (white patches, "
                                "re-typed in a different typeface and baseline); diagnosis set in a different font "
                                "and size from the rest of the letter.",
        },
        {
            "id": "dl-grace-tampered",
            "label": "Driver licence — Grace Williams (sent with the claim)",
            "kind": "driver-licence",
            "src": SRC_PREFIX + "driver-licence-grace-williams-tampered.png",
            "expected": "tampered",
            "owner": "grace",
            "description": "Licence image attached to the claim email. The details are Grace's, but the photo "
                           "region was edited: a different photo pasted over the original, cutting through the "
                           "hologram band, and the ghost image no longer matches the main photo.",
            "holderName": "Grace Williams",
            "documentNumber": "038 641 207",
            "issuer": "Road Licensing Victoria",
            "issueDate": "2021-08-16",
            "expiryDate": "2031-08-16",
            "dateOfBirth": "1965-07-22",
            "securityFeatures": "partial",
            "tamperIndicators": "Photo region edited: replacement photo pasted over the original with a white "
                                "cut-out halo, a different backdrop and a slight rotation; the patch breaks the "
                                "guilloche and cuts straight through the hologram band; the ghost image shows a "
                                "different person from the main photo, and the main photo doesn't match the licence "
                                "copy on Grace's client file.",
            "electronicVerification": "Match (data only) — DVS confirms the licence details, not the photo",
        },
        {
            "id": "bank-statement-gw-consulting",
            "label": "Bank statement — G Williams Consulting Pty Ltd, account ending 4471 (sent with the claim)",
            "kind": "bank-statement",
            "src": SRC_PREFIX + "bank-statement-g-williams-consulting.png",
            "expected": "suspicious",
            "owner": "grace",
            "description": "Interim statement for the account the claim asks Caldova to pay. The account was opened "
                           "on 03/11/2026 with a $10 cash deposit, and it's in a company's name — not the policy "
                           "owner, Grace Williams.",
            "holderName": "G Williams Consulting Pty Ltd",
            "documentNumber": "063-*** ****4471",
            "issuer": "Laneway Bank",
            "issueDate": "2026-11-05",
            "accountOpenedDate": "2026-11-03",
            "periodStart": "2026-11-03",
            "periodEnd": "2026-11-05",
            "keyAmount": 10.00,
            "keyAmountLabel": "Closing balance",
            "securityFeatures": "n/a",
            "tamperIndicators": "none",
        },
        # Scenario 3 — George Papadopoulos and Harpreet Kaur (business protection)
        {
            "id": "company-extract-northcote",
            "label": "Company extract — Northcote Custom Joinery Pty Ltd",
            "kind": "company-extract",
            "src": SRC_PREFIX + "company-extract-northcote-custom-joinery.png",
            "expected": "legit",
            "owner": "george",
            "description": "Current company extract searched on 09/11/2026: registered since 14/03/2011; directors "
                           "George Papadopoulos and Harpreet Kaur; 100 ordinary shares held 50/50, beneficially. "
                           "The company will own the key person policy.",
            "holderName": "Northcote Custom Joinery Pty Ltd",
            "documentNumber": "ACN 151 446 228",
            "issuer": "Company register (specimen layout)",
            "issueDate": "2026-11-09",
            "registrationDate": "2011-03-14",
            "companyStatus": "Registered",
            "officers": "George Papadopoulos — director, 50 ordinary shares (beneficially held); "
                        "Harpreet Kaur — director and secretary, 50 ordinary shares (beneficially held)",
            "securityFeatures": "n/a",
            "tamperIndicators": "none",
        },
        {
            "id": "dl-george",
            "label": "Driver licence — George Papadopoulos",
            "kind": "driver-licence",
            "src": SRC_PREFIX + "driver-licence-george-papadopoulos.png",
            "expected": "legit",
            "owner": "george",
            "description": "Victoria-style specimen licence for George — director, 50% shareholder and the life "
                           "insured on the buy-sell and key person cover.",
            "holderName": "George Papadopoulos",
            "documentNumber": "029 554 871",
            "issuer": "Road Licensing Victoria",
            "issueDate": "2022-04-19",
            "expiryDate": "2032-04-19",
            "dateOfBirth": "1974-04-19",
            "securityFeatures": "yes",
            "tamperIndicators": "none",
            "electronicVerification": "Match — DVS, 10/11/2026",
        },
        {
            "id": "passport-harpreet",
            "label": "Passport — Harpreet Kaur",
            "kind": "passport",
            "src": SRC_PREFIX + "passport-harpreet-kaur.png",
            "expected": "legit",
            "owner": "george",
            "description": "Australia-style specimen passport bio page for Harpreet — director, secretary, 50% "
                           "shareholder and the life insured on her buy-sell cover. Printed data and MRZ check "
                           "digits are consistent.",
            "holderName": "Harpreet Kaur",
            "documentNumber": "PK5513408",
            "issuer": "Australia (specimen layout)",
            "issueDate": "2023-05-22",
            "expiryDate": "2033-05-22",
            "dateOfBirth": "1979-09-08",
            "mrz": "\n".join(harpreet_mrz),
            "securityFeatures": "yes",
            "tamperIndicators": "none",
            "electronicVerification": "Match — DVS, 10/11/2026",
        },
        # Scenario 4 — Tom Bradley (income protection claim)
        {
            "id": "earnings-tom",
            "label": "BAS and profit and loss summary — Bradley Electrical (12 months to 30/09/2026)",
            "kind": "bas-profit-loss",
            "src": SRC_PREFIX + "bas-profit-loss-bradley-electrical.png",
            "expected": "legit",
            "owner": "tom",
            "description": "Earnings evidence for Tom's income protection claim, prepared by his tax agent: four "
                           "quarterly BAS lodgements and a profit and loss showing net business income of $96,000 "
                           "for the 12 months before his injury.",
            "holderName": "Tom Bradley — Bradley Electrical",
            "documentNumber": "ABN 62 418 905 337",
            "issuer": "Tidewater Accounting (tax agent)",
            "issueDate": "2026-11-10",
            "periodStart": "2025-10-01",
            "periodEnd": "2026-09-30",
            "keyAmount": 96000,
            "keyAmountLabel": "Net business income (12 months)",
            "securityFeatures": "n/a",
            "tamperIndicators": "none",
        },
        {
            "id": "surgeon-report-tom",
            "label": "Surgeon's report — Dr Hamish Calder, orthopaedic surgeon (06/11/2026)",
            "kind": "medical-report",
            "src": SRC_PREFIX + "surgeon-report-tom-bradley.png",
            "expected": "legit",
            "owner": "tom",
            "description": "Treating surgeon's report: fell from a ladder on 14/10/2026, MRI-confirmed tear of the "
                           "right rotator cuff, arthroscopic repair on 21/10/2026, four to six months off work.",
            "holderName": "Tom Bradley",
            "documentNumber": "2716453L",
            "issuer": "Seaford Shoulder & Upper Limb Clinic — Dr Hamish Calder",
            "issueDate": "2026-11-06",
            "consultationDate": "2026-10-21",
            "dateOfBirth": "1985-03-03",
            "providerNumber": "2716453L",
            "diagnosisEvidence": "MRI right shoulder 16/10/2026 (full-thickness supraspinatus tear) and the "
                                 "operation on 21/10/2026",
            "securityFeatures": "n/a",
            "tamperIndicators": "none",
        },
        # Scenario 5 — Robert Chen (death claim)
        {
            "id": "death-cert-mei-chen",
            "label": "Death certificate (certified copy) — Mei Chen",
            "kind": "death-certificate",
            "src": SRC_PREFIX + "death-certificate-mei-chen.png",
            "expected": "legit",
            "owner": "robert",
            "description": "Certified copy of a Victoria-style death certificate, registration 2026/041857: Mei Chen, "
                           "born 05/06/1984, died 12/10/2026, registered 19/10/2026. Certified by a Justice of the "
                           "Peace on 11/11/2026.",
            "holderName": "Mei Chen",
            "documentNumber": "2026/041857",
            "issuer": "Births, Deaths and Marriages Victoria (specimen layout)",
            "issueDate": "2026-10-19",
            "dateOfBirth": "1984-06-05",
            "dateOfDeath": "2026-10-12",
            "registrationDate": "2026-10-19",
            "certification": "A. Nguyen JP (Vic), sighted 11/11/2026",
            "securityFeatures": "yes",
            "tamperIndicators": "none",
        },
        {
            "id": "dl-robert",
            "label": "Driver licence — Robert Chen (certified copy)",
            "kind": "driver-licence",
            "src": SRC_PREFIX + "driver-licence-robert-chen.png",
            "expected": "legit",
            "owner": "robert",
            "description": "Certified copy of Robert's Victoria-style specimen licence — he is the nominated "
                           "beneficiary (100%) on Mei's policy.",
            "holderName": "Robert Chen",
            "documentNumber": "046 218 395",
            "issuer": "Road Licensing Victoria",
            "issueDate": "2022-01-30",
            "expiryDate": "2032-01-30",
            "dateOfBirth": "1982-01-30",
            "certification": "A. Nguyen JP (Vic), sighted 11/11/2026",
            "securityFeatures": "yes",
            "tamperIndicators": "none",
            "electronicVerification": "Match — DVS, 13/11/2026",
        },
    ]


def main() -> None:
    for acn in (NORTHCOTE_EXTRACT["acn"],):
        assert not acn_check_ok(acn), f"ACN {acn} has a valid check digit — pick a fictional one"
    for abn in (NORTHCOTE_EXTRACT["abn"], "62 418 905 337", "51 824 753 558"):
        assert not abn_check_ok(abn), f"ABN {abn} has a valid check digit — pick a fictional one"

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    driver_licence("driver-licence-priya-natarajan.png", PRIYA_DL)
    payslip("payslip-priya-natarajan.png", PRIYA_PAYSLIP)
    medical_report("oncology-report-grace-williams.png", GRACE_REPORT, fake=True)
    driver_licence("driver-licence-grace-williams-tampered.png", GRACE_DL, tampered_photo=True)
    bank_statement("bank-statement-g-williams-consulting.png", GRACE_STATEMENT)
    company_extract("company-extract-northcote-custom-joinery.png", NORTHCOTE_EXTRACT)
    driver_licence("driver-licence-george-papadopoulos.png", GEORGE_DL)
    harpreet_mrz = passport("passport-harpreet-kaur.png", HARPREET_PP)
    earnings_summary("bas-profit-loss-bradley-electrical.png", TOM_EARNINGS)
    medical_report("surgeon-report-tom-bradley.png", TOM_REPORT)
    death_certificate("death-certificate-mei-chen.png", MEI_CERT)
    driver_licence("driver-licence-robert-chen.png", ROBERT_DL)

    samples = manifest(harpreet_mrz)
    keep = {Path(s["src"]).name for s in samples}
    for stale in OUT_DIR.glob("*.png"):
        if stale.name not in keep:
            stale.unlink()
            print(f"Removed {stale.name}")
    (OUT_DIR / "manifest.json").write_text(
        json.dumps({"samples": samples}, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"Wrote {len(samples)} samples and manifest.json to {OUT_DIR}")


if __name__ == "__main__":
    main()
