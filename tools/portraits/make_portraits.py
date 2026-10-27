"""Draws the five Isekai Stream staff personas as layered, Live2D-ready bust portraits.

For each persona it writes:
  Game/Assets/_Game/Art/Portraits/<Id>/<layer>.png   cropped layers for the in-game 2.5D puppet
  Game/Assets/_Game/Art/Portraits/<Id>/rig.json      layer rects, groups, pivots and depths
  ArtSource/Live2D/<Id>.psd                          the same layers as a PSD, ready to import into Cubism Editor
  previews/portraits_sheet.png                       all five composited, for a quick look

Usage (from the repo root):  python -I tools/portraits/make_portraits.py
Every persona is original art made from code. Edit the colours and shapes below and re-run.
"""
import json
import math
import os
import struct
import sys

from PIL import Image, ImageChops, ImageDraw, ImageFilter

W = H = 1024          # canvas size of every layer
SS = 2                # supersampling factor while drawing
REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT_ART = os.path.join(REPO, "Game", "Assets", "_Game", "Art", "Portraits")
OUT_PSD = os.path.join(REPO, "ArtSource", "Live2D")
OUT_PREVIEW = os.path.join(REPO, "previews")

EYE_Y = 462
EYE_DX = 74           # eye centres sit at 512 ± EYE_DX
HEAD_PIVOT = (512, 640)


# ---------- geometry helpers ----------

def smooth(pts, closed=True, n=10):
    """Catmull-Rom spline through pts."""
    out = []
    m = len(pts)
    segs = range(m) if closed else range(m - 1)
    for i in segs:
        p0 = pts[(i - 1) % m] if closed else pts[max(i - 1, 0)]
        p1 = pts[i]
        p2 = pts[(i + 1) % m]
        p3 = pts[(i + 2) % m] if closed else pts[min(i + 2, m - 1)]
        for k in range(n):
            t = k / n
            t2, t3 = t * t, t * t * t
            out.append(tuple(0.5 * ((2 * p1[j]) + (-p0[j] + p2[j]) * t + (2 * p0[j] - 5 * p1[j] + 4 * p2[j] - p3[j]) * t2
                                    + (-p0[j] + 3 * p1[j] - 3 * p2[j] + p3[j]) * t3) for j in (0, 1)))
    if not closed:
        out.append(pts[-1])
    return out


def stroke(center, w0, w1=None, taper=True):
    """Polygon around a centre line. Width goes w0 → w1, pinched at both ends when taper is on."""
    w1 = w0 if w1 is None else w1
    n = len(center)
    left, right = [], []
    for i, p in enumerate(center):
        a = center[max(i - 1, 0)]
        b = center[min(i + 1, n - 1)]
        dx, dy = b[0] - a[0], b[1] - a[1]
        ln = math.hypot(dx, dy) or 1
        nx, ny = -dy / ln, dx / ln
        t = i / (n - 1)
        w = w0 + (w1 - w0) * t
        if taper:
            w *= max(math.sin(math.pi * t), 0.15)
        left.append((p[0] + nx * w / 2, p[1] + ny * w / 2))
        right.append((p[0] - nx * w / 2, p[1] - ny * w / 2))
    return left + right[::-1]


def strand(base, tip, width, bend=0.0):
    """A hair strand: wide at the base, sharp at the tip, bowed sideways by bend."""
    bx, by = base
    tx, ty = tip
    mx, my = (bx + tx) / 2 + bend, (by + ty) / 2
    left = smooth([(bx - width / 2, by), (mx - width * 0.32, my), (tx, ty)], closed=False, n=8)
    right = smooth([(tx, ty), (mx + width * 0.32, my), (bx + width / 2, by)], closed=False, n=8)
    return left + right[1:]


def mirror(pts):
    return [(W - x, y) for x, y in pts]


def offset(pts, dx, dy):
    return [(x + dx, y + dy) for x, y in pts]


def ellipse_pts(cx, cy, rx, ry, n=48):
    return [(cx + rx * math.cos(2 * math.pi * i / n), cy + ry * math.sin(2 * math.pi * i / n)) for i in range(n)]


def darker(c, f=0.7):
    return tuple(int(v * f) for v in c[:3]) + tuple(c[3:])


def lighter(c, f=0.4):
    return tuple(int(v + (255 - v) * f) for v in c[:3]) + tuple(c[3:])


def rgba(c, a=255):
    return tuple(c[:3]) + (a,)


# ---------- layers ----------

class Layer:
    def __init__(self, name, group, depth=0.0, outline=True):
        self.name, self.group, self.depth, self.outline = name, group, depth, outline
        self.img = Image.new("RGBA", (W * SS, H * SS), (0, 0, 0, 0))

    def _draw(self, img=None):
        return ImageDraw.Draw(img or self.img)

    def poly(self, pts, col, img=None):
        self._draw(img).polygon([(x * SS, y * SS) for x, y in pts], fill=rgba(col, col[3] if len(col) > 3 else 255))

    def ellipse(self, cx, cy, rx, ry, col, img=None):
        self._draw(img).ellipse([(cx - rx) * SS, (cy - ry) * SS, (cx + rx) * SS, (cy + ry) * SS],
                                fill=rgba(col, col[3] if len(col) > 3 else 255))

    def line(self, pts, col, w, img=None):
        self._draw(img).line([(x * SS, y * SS) for x, y in pts], fill=rgba(col, col[3] if len(col) > 3 else 255),
                             width=max(1, int(w * SS)), joint="curve")

    def clipped(self, paint, blur=0):
        """Paint onto a scratch image, keep it only where this layer already has pixels."""
        tmp = Image.new("RGBA", self.img.size, (0, 0, 0, 0))
        paint(tmp)
        if blur:
            tmp = tmp.filter(ImageFilter.GaussianBlur(blur * SS))
        tmp.putalpha(ImageChops.multiply(tmp.getchannel("A"), self.img.getchannel("A")))
        self.img = Image.alpha_composite(self.img, tmp)

    def blurred(self, paint, radius):
        tmp = Image.new("RGBA", self.img.size, (0, 0, 0, 0))
        paint(tmp)
        tmp = tmp.filter(ImageFilter.GaussianBlur(radius * SS))
        self.img = Image.alpha_composite(self.img, tmp)

    def finish(self, line_col):
        img = self.img
        if self.outline:
            a = img.getchannel("A").point(lambda v: 255 if v > 40 else 0)
            grown = a.filter(ImageFilter.MaxFilter(7))
            ink = Image.new("RGBA", img.size, rgba(line_col))
            ink.putalpha(grown)
            img = Image.alpha_composite(ink, img)
        return img.resize((W, H), Image.LANCZOS)


# ---------- the parts every persona shares ----------

def eye_shapes(cx, cy, side):
    """side = -1 for the eye on the viewer's left, +1 for the right. The outer corner is towards side."""
    def p(x, y):
        return (cx + x * side, cy + y)
    white = smooth([p(-40, 4), p(-30, -26), p(0, -36), p(30, -32), p(46, -10), p(34, 22), p(2, 32), p(-28, 24)])
    lash = stroke(smooth([p(-44, 6), p(-30, -30), p(0, -42), p(32, -37), p(52, -18), p(64, -28)], closed=False, n=6), 13, 9)
    lower = stroke(smooth([p(8, 33), p(28, 28), p(42, 18)], closed=False, n=6), 5)
    closed = stroke(smooth([p(-44, 2), p(-14, 16), p(20, 16), p(48, 2), p(58, -6)], closed=False, n=6), 9)
    return white, lash, lower, closed


def draw_eye(layers, side, cfg):
    cx = 512 + EYE_DX * side
    cy = EYE_Y
    s = "L" if side < 0 else "R"
    grp = "Eye" + s
    white, lash, lower, closed = eye_shapes(cx, cy, side)

    lw = Layer("eye_white_" + s, grp, 0.15, outline=False)
    lw.poly(white, (255, 255, 255))
    lw.clipped(lambda im: lw.poly(offset(white, 0, -14), (222, 222, 238), im))   # lid shadow
    lw.clipped(lambda im: lw.poly(offset(white, 0, 8), (255, 255, 255), im))

    li = Layer("iris_" + s, "Iris" + s, 0.18, outline=False)
    ix, iy = cx + 2 * side, cy + 2
    li.ellipse(ix, iy, 27, 34, cfg["iris"])
    li.clipped(lambda im: li.ellipse(ix, iy - 20, 34, 24, darker(cfg["iris"], 0.55), im))
    li.clipped(lambda im: li.ellipse(ix, iy + 24, 22, 12, lighter(cfg["iris"], 0.45), im))
    pr = cfg.get("pupil", (11, 17))
    li.ellipse(ix, iy - 2, pr[0], pr[1], darker(cfg["iris"], 0.3))
    ring = ellipse_pts(ix, iy, 26, 33)
    li.line(ring + ring[:1], darker(cfg["iris"], 0.45), 3)

    lh = Layer("eye_highlight_" + s, "Iris" + s, 0.22, outline=False)
    lh.ellipse(ix - 9, iy - 13, 10, 11, (255, 255, 255))
    lh.ellipse(ix + 11, iy + 15, 5, 5, (255, 255, 255, 220))

    ll = Layer("eye_lash_" + s, grp, 0.2, outline=False)
    ll.poly(lash, cfg["lash"])
    ll.poly(lower, cfg["lash"])

    lc = Layer("eye_closed_" + s, grp, 0.2, outline=False)
    lc.poly(closed, cfg["lash"])

    layers += [lw, li, lh, ll, lc]


def draw_brows(layers, cfg):
    tilt = cfg.get("brow_tilt", 0)
    for side in (-1, 1):
        s = "L" if side < 0 else "R"
        cx = 512 + EYE_DX * side
        line = smooth([(cx - 36 * side, 404 + tilt), (cx + 2 * side, 394), (cx + 40 * side, 400 - tilt * 0.5)], closed=False, n=8)
        lb = Layer("brow_" + s, "Brow" + s, 0.25, outline=False)
        lb.poly(stroke(line, 9), cfg["brow"])
        layers.append(lb)


def draw_face(layers, cfg):
    skin, shade = cfg["skin"], cfg["skin_shade"]
    face = smooth([(350, 300), (348, 420), (366, 502), (408, 560), (468, 602), (512, 614), (556, 602),
                   (616, 560), (658, 502), (676, 420), (674, 300), (600, 196), (512, 176), (424, 196)])
    lf = Layer("face", "Head", 0.0)
    lf.poly(face, skin)
    lf.clipped(lambda im: lf.poly([(330, 280), (700, 280), (700, 360), (512, 382), (330, 360)], shade, im))  # under the fringe
    lf.clipped(lambda im: lf.poly(smooth([(336, 380), (372, 520), (440, 600), (380, 640), (320, 520)]), shade, im))
    lf.clipped(lambda im: lf.poly(mirror(smooth([(336, 380), (372, 520), (440, 600), (380, 640), (320, 520)])), shade, im))
    lf.line([(514, 512), (508, 524)], darker(shade, 0.8), 3)  # nose
    layers.append(lf)

    lb = Layer("blush", "Head", 0.06, outline=False)
    lb.blurred(lambda im: (lb.ellipse(428, 532, 34, 15, rgba(cfg.get("blush", (255, 130, 150)), 120), im),
                           lb.ellipse(596, 532, 34, 15, rgba(cfg.get("blush", (255, 130, 150)), 120), im)), 6)
    for x0 in (414, 582):   # little anime blush hatching
        for k in range(3):
            lb.line([(x0 + k * 11, 538), (x0 + k * 11 + 7, 526)], rgba(darker(cfg.get("blush", (255, 130, 150)), 0.85), 170), 2)
    layers.append(lb)


def draw_mouth(layers, cfg):
    line = cfg["lash"]
    style = cfg.get("mouth", "smile")
    lc = Layer("mouth_closed", "Mouth", 0.12, outline=False)
    lo = Layer("mouth_open", "Mouth", 0.12)
    if style == "cat":
        lc.poly(stroke(smooth([(488, 556), (500, 566), (512, 558), (524, 566), (536, 556)], closed=False, n=5), 5, taper=False), line)
    elif style == "grin":
        lc.poly(stroke(smooth([(470, 552), (512, 572), (556, 548)], closed=False, n=8), 6), line)
        lc.poly([(528, 560), (538, 559), (533, 574)], (255, 255, 255))  # fang
    elif style == "flat":
        lc.poly(stroke([(496, 562), (528, 562)], 4, taper=False), line)
    else:
        lc.poly(stroke(smooth([(490, 558), (512, 566), (534, 556)], closed=False, n=6), 5), line)

    shape = smooth([(484, 552), (512, 548), (540, 552), (534, 574), (512, 586), (490, 574)])
    lo.poly(shape, (132, 42, 58))
    lo.clipped(lambda im: lo.ellipse(512, 588, 22, 14, (238, 120, 132), im))   # tongue
    lo.clipped(lambda im: lo.poly([(480, 540), (544, 540), (544, 556), (480, 556)], (255, 255, 255), im))  # teeth
    if style == "grin":
        lo.poly([(526, 553), (536, 553), (531, 567)], (255, 255, 255))
        lo.poly([(488, 553), (498, 553), (493, 567)], (255, 255, 255))
    layers += [lc, lo]


def draw_body(layers, cfg):
    skin, shade, cloth = cfg["skin"], cfg["skin_shade"], cfg["cloth"]
    lb = Layer("body", "Body", -0.2)
    neck = [(474, 560), (550, 560), (556, 712), (468, 712)]
    lb.poly(neck, skin)
    lb.clipped(lambda im: lb.ellipse(512, 610, 70, 44, shade, im))
    torso = smooth([(296, 1060), (300, 860), (340, 760), (430, 712), (512, 706), (594, 712), (684, 760), (724, 860), (728, 1060)])
    lb.poly(torso, cloth)
    lb.clipped(lambda im: lb.poly([(296, 900), (420, 760), (430, 1060), (296, 1060)], darker(cloth, 0.82), im))
    layers.append(lb)


# ---------- hair ----------

HAIR_BACK = {
    "long": [(312, 320), (340, 168), (512, 116), (684, 168), (712, 320), (734, 560), (776, 860), (748, 1010),
             (276, 1010), (248, 860), (290, 560)],
    "bob": [(306, 330), (338, 166), (512, 120), (686, 166), (718, 330), (724, 560), (690, 640), (334, 640), (300, 560)],
    "spiky": [(300, 330), (320, 176), (512, 104), (704, 176), (724, 330), (736, 470), (700, 450), (712, 560),
              (660, 520), (640, 600), (384, 600), (364, 520), (312, 560), (324, 450), (288, 470)],
}


def draw_hair_back(layers, cfg):
    lh = Layer("hair_back", "HairBack", -0.5)
    pts = HAIR_BACK[cfg["hair_back"]]
    # A step darker than the front hair, so the side locks read against it.
    lh.poly(smooth(pts) if cfg["hair_back"] != "spiky" else pts, darker(cfg["hair"], 0.86))
    lh.clipped(lambda im: lh.poly(smooth([(400, 400), (512, 360), (624, 400), (640, 1010), (384, 1010)]), cfg["hair_shade"], im))
    layers.append(lh)


def draw_hair_front(layers, cfg):
    hair, shade, hl = cfg["hair"], cfg["hair_shade"], cfg["hair_light"]
    lf = Layer("hair_front", "Head", 0.4)
    cap = smooth([(336, 340), (348, 228), (420, 160), (512, 140), (604, 160), (676, 228), (688, 340),
                  (604, 296), (512, 286), (420, 296)])
    lf.poly(cap, hair)
    if cfg.get("blunt"):   # a straight-cut fringe: fill the gaps between the strands down to the cut line
        tips = cfg["bangs"]
        cut = min(t[1] for t in tips) - 6
        lf.poly([(tips[0][0] - 20, 236), (tips[-1][0] + 20, 236), (tips[-1][0], cut), (tips[0][0], cut)], hair)
    for (tx, ty, w, bend) in cfg["bangs"]:
        base = (tx * 0.65 + 512 * 0.35, 236)
        lf.poly(strand(base, (tx, ty), w, bend), hair)
    # soft shading towards the strand tips, then the shiny "angel ring"
    lf.clipped(lambda im: lf.poly(smooth([(330, 340), (512, 330), (700, 340), (700, 440), (512, 430), (330, 440)]),
                                  rgba(shade, 200), im), blur=18)
    ring = smooth([(392, 232), (450, 206), (512, 198), (574, 206), (632, 232)], closed=False, n=8)
    lf.clipped(lambda im: lf.poly(stroke(ring, 16), rgba(hl, 230), im))
    if cfg.get("clip"):
        lf.poly(smooth([(640, 268), (668, 252), (676, 282), (652, 296)]), cfg["clip"])
        lf.ellipse(658, 274, 6, 6, lighter(cfg["clip"], 0.6))
    layers.append(lf)

    for side in (-1, 1):
        s = "L" if side < 0 else "R"
        length = cfg["side_len"]
        x0 = 512 + 150 * side
        lock = strand((x0, 300), (x0 + 6 * side, length), cfg.get("side_w", 66), bend=10 * side)
        ls = Layer("hair_side_" + s, "HairSide" + s, 0.3)
        ls.poly(lock, hair)
        ls.clipped(lambda im: ls.poly(stroke([(x0 + 14 * side, 320), (x0 + 10 * side, length - 30)], 16), shade, im))
        layers.append(ls)


# ---------- persona accessories ----------

def elf_ears(layers, cfg, big=False):
    for side in (-1, 1):
        s = "L" if side < 0 else "R"
        if big:   # gremlin
            pts = [(362, 410), (290, 372), (206, 344), (244, 420), (300, 478), (364, 500)]
        else:     # elf
            pts = [(362, 430), (316, 410), (226, 352), (276, 432), (318, 480), (362, 500)]
        if side > 0:
            pts = mirror(pts)
        le = Layer("ear_" + s, "Ear" + s, -0.05)
        le.poly(pts, cfg["skin"])
        inner = [(p[0] * 0.6 + (362 if side < 0 else 662) * 0.4, p[1] * 0.75 + 455 * 0.25) for p in pts]
        le.clipped(lambda im: le.poly(inner, cfg["skin_shade"], im))
        layers.append(le)


def cat_ears(layers, cfg):
    for side in (-1, 1):
        s = "L" if side < 0 else "R"
        outer = [(374, 268), (366, 112), (478, 196)]
        inner = [(390, 246), (382, 146), (452, 202)]
        if side > 0:
            outer, inner = mirror(outer), mirror(inner)
        lc = Layer("cat_ear_" + s, "CatEar" + s, -0.3)
        lc.poly(outer, cfg["hair"])
        lc.poly(inner, (255, 196, 210))
        layers.append(lc)


def horns(layers, cfg):
    for side in (-1, 1):
        s = "L" if side < 0 else "R"
        center = smooth([(428, 214), (392, 160), (344, 118), (312, 62)], closed=False, n=8)
        if side > 0:
            center = mirror(center)
        lh = Layer("horn_" + s, "Head", -0.3)
        lh.poly(stroke(center, 46, 6, taper=False), (74, 52, 70))
        for k in (0.25, 0.45, 0.65):
            i = int(k * (len(center) - 1))
            px, py = center[i]
            lh.clipped(lambda im, px=px, py=py: lh.poly(stroke([(px - 30, py + 10), (px + 30, py - 10)], 5, taper=False),
                                                         (120, 92, 116), im))
        layers.append(lh)


def tail(layers, cfg, kind):
    lt = Layer("tail", "Tail", -0.6)
    if kind == "dragon":
        c = smooth([(690, 1060), (790, 900), (850, 760), (860, 640), (830, 560)], closed=False, n=10)
        lt.poly(stroke(c, 80, 26, taper=False), cfg["tail"])
        lt.poly([(800, 590), (828, 492), (884, 560), (850, 600)], darker(cfg["tail"], 0.8))
        lt.clipped(lambda im: lt.poly(stroke(offset(c, -14, 4), 20, 8, taper=False), lighter(cfg["tail"], 0.4), im))
    else:
        c = smooth([(690, 1060), (800, 930), (860, 770), (840, 650), (790, 600)], closed=False, n=10)
        lt.poly(stroke(c, 40, 34, taper=False), cfg["hair"])
        lt.ellipse(790, 600, 22, 22, cfg["hair"])
        lt.clipped(lambda im: lt.ellipse(792, 606, 40, 46, lighter(cfg["hair"], 0.6), im))
    layers.append(lt)


def outfit(layers, cfg):
    lo = Layer("outfit", "Body", -0.15)
    kind = cfg["outfit"]
    trim = cfg.get("trim", (230, 190, 90))
    if kind == "vest":          # Lumi: receptionist vest over a white shirt
        lo.poly([(450, 700), (512, 812), (574, 700), (560, 690), (512, 760), (464, 690)], (250, 250, 255))
        lo.poly([(462, 694), (500, 716), (512, 760), (470, 730)], (255, 255, 255))
        lo.poly(mirror([(462, 694), (500, 716), (512, 760), (470, 730)]), (255, 255, 255))
        lo.poly(stroke([(452, 704), (512, 816)], 8, taper=False), trim)
        lo.poly(stroke([(572, 704), (512, 816)], 8, taper=False), trim)
        lo.ellipse(512, 830, 16, 16, (60, 190, 150))
        lo.ellipse(507, 825, 5, 5, (220, 255, 240))
    elif kind == "porter":      # Ember: porter jacket with a luggage strap
        lo.poly([(436, 708), (512, 800), (470, 870), (400, 760)], darker(cfg["cloth"], 0.75))
        lo.poly(mirror([(436, 708), (512, 800), (470, 870), (400, 760)]), darker(cfg["cloth"], 0.75))
        for y in (850, 920, 990):
            lo.ellipse(488, y, 9, 9, trim)
            lo.ellipse(536, y, 9, 9, trim)
        lo.poly(stroke([(372, 750), (520, 900), (700, 1060)], 30, taper=False), (120, 80, 50))
        lo.ellipse(520, 900, 16, 14, trim)
    elif kind == "maid_black":  # Mochi: dress with a frilled apron bib and bow
        bib = [(430, 790), (594, 790), (614, 1060), (410, 1060)]
        lo.poly(bib, (255, 255, 255))
        for i in range(9):
            x = 430 + i * 20.5
            lo.ellipse(x, 790, 11, 9, (255, 255, 255))
        lo.poly([(460, 700), (512, 740), (564, 700), (580, 720), (512, 770), (444, 720)], (255, 255, 255))
        lo.poly([(512, 740), (460, 716), (466, 770)], cfg["bow"])
        lo.poly([(512, 740), (564, 716), (558, 770)], cfg["bow"])
        lo.ellipse(512, 742, 12, 11, darker(cfg["bow"], 0.8))
    elif kind == "maid_tech":   # Unit-07: blue-grey maid dress with a tech collar
        lo.poly([(440, 800), (584, 800), (604, 1060), (420, 1060)], (246, 248, 252))
        lo.poly([(450, 700), (512, 744), (574, 700), (592, 728), (512, 776), (432, 728)], (246, 248, 252))
        lo.poly(stroke([(448, 724), (512, 762), (576, 724)], 5, taper=False), (60, 230, 240))
        lo.poly([(512, 790), (526, 806), (512, 822), (498, 806)], (60, 230, 240))
    elif kind == "chef":        # Gigi: double-breasted chef coat and neckerchief
        lo.poly([(452, 704), (572, 704), (512, 790)], cfg["bow"])
        lo.poly([(500, 780), (524, 780), (540, 830), (484, 830)], darker(cfg["bow"], 0.85))
        for y in (850, 920, 990):
            lo.ellipse(470, y, 10, 10, (200, 200, 196))
            lo.ellipse(554, y, 10, 10, (200, 200, 196))
        lo.poly(stroke([(512, 800), (512, 1060)], 4, taper=False), (210, 210, 205))
    layers.append(lo)


def headwear(layers, cfg):
    kind = cfg.get("headwear")
    if kind == "maid_band":
        lh = Layer("headband", "Head", 0.45)
        arc = smooth([(352, 236), (430, 172), (512, 156), (594, 172), (672, 236)], closed=False, n=8)
        for x, y in arc[::3]:
            lh.ellipse(x, y - 10, 20, 18, (255, 255, 255))
        lh.poly(stroke(arc, 20, taper=False), (255, 255, 255))
        lh.clipped(lambda im: lh.poly(stroke(offset(arc, 0, 6), 6, taper=False), (60, 230, 240), im))
        layers.append(lh)
    elif kind == "chef_hat":
        lh = Layer("chef_hat", "Head", 0.45)
        for cx, cy, r in ((428, 150, 70), (512, 118, 82), (596, 150, 70)):
            lh.ellipse(cx, cy, r, r * 0.9, (255, 255, 255))
        lh.poly(smooth([(400, 170), (624, 170), (642, 262), (512, 248), (382, 262)]), (255, 255, 255))
        lh.clipped(lambda im: lh.poly([(370, 226), (654, 226), (654, 270), (370, 270)], (226, 226, 232), im))
        lh.clipped(lambda im: lh.ellipse(560, 110, 40, 30, (232, 232, 238), im))
        layers.append(lh)
    if cfg.get("visor"):
        lv = Layer("visor", "Head", 0.35)
        band = smooth([(332, 420), (512, 400), (692, 420), (700, 508), (512, 500), (324, 508)])
        lv.poly(band, (80, 230, 255, 120))
        lv.poly(stroke([(334, 418), (512, 398), (690, 418)], 8, taper=False), (220, 236, 245))
        lv.poly(stroke([(326, 508), (512, 500), (698, 508)], 6, taper=False), (220, 236, 245))
        lv.ellipse(676, 464, 7, 7, (255, 90, 140))
        lv.poly(stroke([(380, 430), (440, 424)], 4, taper=False), (255, 255, 255, 160))
        layers.append(lv)


# ---------- personas ----------

def bangs(tips):
    return [(x, y, w, b) for x, y, w, b in tips]


PERSONAS = [
    dict(id="Lumi", title="Lumi, Elf Receptionist",
         skin=(253, 238, 230), skin_shade=(238, 204, 200), hair=(226, 232, 248), hair_shade=(178, 186, 226),
         hair_light=(255, 255, 255), lash=(64, 56, 92), brow=(150, 150, 196), iris=(52, 178, 136),
         cloth=(48, 56, 104), trim=(232, 196, 96), line=(70, 62, 100), clip=(232, 196, 96),
         hair_back="long", side_len=760, outfit="vest", ears="elf", mouth="smile",
         bangs=bangs([(366, 372, 84, -6), (420, 404, 80, 0), (472, 388, 74, 6), (514, 360, 60, 0),
                      (560, 392, 76, -6), (612, 406, 80, 0), (660, 374, 84, 6)])),
    dict(id="Ember", title="Ember, Dragon Porter",
         skin=(250, 228, 214), skin_shade=(232, 188, 176), hair=(214, 76, 58), hair_shade=(150, 44, 46),
         hair_light=(255, 176, 120), lash=(70, 34, 40), brow=(150, 50, 46), iris=(236, 170, 40), pupil=(5, 22),
         cloth=(132, 36, 44), trim=(236, 196, 90), line=(66, 30, 36), tail=(196, 64, 56),
         hair_back="spiky", side_len=560, side_w=72, outfit="porter", horns=True, tail_kind="dragon", mouth="flat",
         eye_open=0.62, brow_tilt=-4,
         bangs=bangs([(360, 392, 90, -14), (412, 418, 84, -8), (462, 380, 80, 4), (512, 410, 76, 0),
                      (566, 378, 80, -4), (616, 420, 86, 8), (664, 390, 90, 14)])),
    dict(id="Mochi", title="Mochi, Catgirl Server",
         skin=(255, 238, 230), skin_shade=(244, 200, 196), hair=(252, 206, 188), hair_shade=(226, 150, 150),
         hair_light=(255, 246, 236), lash=(80, 46, 70), brow=(204, 134, 132), iris=(76, 150, 232),
         cloth=(44, 40, 56), bow=(250, 110, 150), line=(86, 52, 72),
         hair_back="bob", side_len=620, outfit="maid_black", cat_ears=True, tail_kind="cat", mouth="cat",
         bangs=bangs([(370, 388, 82, -6), (424, 412, 80, 0), (476, 396, 72, 4), (520, 410, 60, 0),
                      (566, 396, 72, -4), (614, 412, 80, 0), (656, 388, 82, 6)])),
    dict(id="Unit07", title="Unit-07, Android Maid",
         skin=(242, 238, 244), skin_shade=(214, 208, 226), hair=(104, 206, 214), hair_shade=(56, 140, 160),
         hair_light=(220, 255, 255), lash=(40, 64, 90), brow=(60, 130, 150), iris=(60, 230, 240), pupil=(8, 14),
         cloth=(70, 88, 122), line=(40, 54, 80),
         hair_back="bob", side_len=640, side_w=60, outfit="maid_tech", headwear="maid_band", visor=True, blunt=True,
         mouth="flat", brow_tilt=0,
         bangs=bangs([(368, 384, 96, -4), (428, 396, 104, 2), (490, 392, 104, 0), (546, 394, 104, -2),
                      (604, 396, 104, 2), (658, 384, 96, 4)])),
    dict(id="Gigi", title="Gigi, Gremlin Chef",
         skin=(222, 240, 212), skin_shade=(186, 214, 176), hair=(92, 176, 84), hair_shade=(54, 120, 60),
         hair_light=(186, 236, 150), lash=(44, 64, 40), brow=(54, 110, 50), iris=(250, 136, 40), pupil=(9, 15),
         cloth=(246, 246, 240), bow=(214, 52, 52), line=(42, 60, 40),
         hair_back="spiky", side_len=500, side_w=70, outfit="chef", ears="gremlin", headwear="chef_hat",
         mouth="grin", brow_tilt=8, blush=(255, 150, 120),
         bangs=bangs([(364, 380, 90, -16), (414, 404, 84, -8), (466, 372, 80, 6), (514, 398, 76, 0),
                      (562, 372, 80, -6), (612, 404, 84, 8), (662, 380, 90, 16)])),
]


def build(cfg):
    layers = []
    if cfg.get("tail_kind"):
        tail(layers, cfg, cfg["tail_kind"])
    draw_hair_back(layers, cfg)
    draw_body(layers, cfg)
    outfit(layers, cfg)
    if cfg.get("ears") == "elf":
        elf_ears(layers, cfg)
    elif cfg.get("ears") == "gremlin":
        elf_ears(layers, cfg, big=True)
    if cfg.get("cat_ears"):
        cat_ears(layers, cfg)
    if cfg.get("horns"):
        horns(layers, cfg)
    draw_face(layers, cfg)
    draw_mouth(layers, cfg)
    draw_eye(layers, -1, cfg)
    draw_eye(layers, 1, cfg)
    draw_brows(layers, cfg)
    draw_hair_front(layers, cfg)
    headwear(layers, cfg)
    return layers


# Groups form the rig's hierarchy. Each one rotates/scales around its pivot (canvas pixels).
def groups(cfg):
    g = {
        "Body": ("", (512, 1024)),
        "Head": ("Body", HEAD_PIVOT),
        "HairBack": ("Head", (512, 250)),
        "HairSideL": ("Head", (362, 300)),
        "HairSideR": ("Head", (662, 300)),
        "EyeL": ("Head", (512 - EYE_DX, EYE_Y)),
        "EyeR": ("Head", (512 + EYE_DX, EYE_Y)),
        "IrisL": ("EyeL", (512 - EYE_DX, EYE_Y)),
        "IrisR": ("EyeR", (512 + EYE_DX, EYE_Y)),
        "BrowL": ("Head", (512 - EYE_DX, 398)),
        "BrowR": ("Head", (512 + EYE_DX, 398)),
        "Mouth": ("Head", (512, 562)),
    }
    if cfg.get("ears"):
        g["EarL"] = ("Head", (360, 455))
        g["EarR"] = ("Head", (664, 455))
    if cfg.get("cat_ears"):
        g["CatEarL"] = ("Head", (420, 240))
        g["CatEarR"] = ("Head", (604, 240))
    if cfg.get("tail_kind"):
        g["Tail"] = ("Body", (690, 1024))
    return g


# How much each group swings with the physics springs (0 = rigid).
SWING = {"HairSideL": 1.0, "HairSideR": 1.0, "HairBack": 0.35, "Tail": 1.3, "EarL": 0.45, "EarR": 0.45,
         "CatEarL": 0.6, "CatEarR": 0.6}


# Alternate drawings that are hidden in the rest pose (and in the PSD).
HIDDEN_LAYERS = ("eye_closed", "mouth_open")


# ---------- PSD writer (layered, RLE) ----------

def packbits(row):
    out = bytearray()
    i, n = 0, len(row)
    while i < n:
        j = i
        while j + 1 < n and row[j + 1] == row[i] and j - i < 127:
            j += 1
        if j > i:
            out += bytes([257 - (j - i + 1)]) + bytes([row[i]])
            i = j + 1
        else:
            j = i
            while j + 1 < n and row[j + 1] != row[j] and j - i < 127:
                j += 1
            if j + 1 >= n:
                j = n - 1
            out += bytes([j - i]) + row[i:j + 1]
            i = j + 1
    return bytes(out)


def rle_channel(ch):
    w, h = ch.size
    data = ch.tobytes()
    rows = [packbits(data[y * w:(y + 1) * w]) for y in range(h)]
    return struct.pack(">H", 1) + b"".join(struct.pack(">H", len(r)) for r in rows) + b"".join(rows)


def pascal(name):
    b = name.encode("ascii", "replace")[:255]
    s = bytes([len(b)]) + b
    return s + b"\0" * ((4 - len(s) % 4) % 4)


def write_psd(path, layers, composite):
    """layers: list of (name, rgba image cropped, left, top), bottom first."""
    records, chdata = b"", b""
    for name, img, left, top in layers:
        w, h = img.size
        chans = [(-1, img.getchannel("A")), (0, img.getchannel("R")), (1, img.getchannel("G")), (2, img.getchannel("B"))]
        encoded = [(cid, rle_channel(c)) for cid, c in chans]
        rec = struct.pack(">iiiiH", top, left, top + h, left + w, len(encoded))
        for cid, data in encoded:
            rec += struct.pack(">hI", cid, len(data))
        uni = name.encode("utf-16-be")
        luni = b"8BIMluni" + struct.pack(">II", 4 + len(uni) + (len(uni) % 4 and 4 - len(uni) % 4 or 0), len(name)) \
            + uni + b"\0" * (len(uni) % 4 and 4 - len(uni) % 4 or 0)
        extra = struct.pack(">I", 0) + struct.pack(">I", 0) + pascal(name) + luni
        flags = 0x02 if name.startswith(HIDDEN_LAYERS) else 0   # bit 1 = hidden
        rec += b"8BIMnorm" + bytes([255, 0, flags, 0]) + struct.pack(">I", len(extra)) + extra
        records += rec
        chdata += b"".join(d for _, d in encoded)
    info = struct.pack(">h", -len(layers)) + records + chdata
    if len(info) % 2:
        info += b"\0"
    layer_info = struct.pack(">I", len(info)) + info
    lmi = layer_info + struct.pack(">I", 0)
    w, h = composite.size
    with open(path, "wb") as f:
        f.write(b"8BPS" + struct.pack(">H", 1) + b"\0" * 6 + struct.pack(">HIIHH", 4, h, w, 8, 3))
        f.write(struct.pack(">I", 0))                      # colour mode data
        f.write(struct.pack(">I", 0))                      # image resources
        f.write(struct.pack(">I", len(lmi)) + lmi)
        f.write(struct.pack(">H", 0))                      # composite, raw
        for c in "RGBA":
            f.write(composite.getchannel(c).tobytes())


# ---------- output ----------

def export(cfg):
    layers = build(cfg)
    art_dir = os.path.join(OUT_ART, cfg["id"])
    os.makedirs(art_dir, exist_ok=True)
    for old in os.listdir(art_dir):
        if old.endswith(".png"):
            os.remove(os.path.join(art_dir, old))

    composite = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    rig_layers, psd_layers = [], []
    for order, layer in enumerate(layers):
        img = layer.finish(cfg["line"])
        bbox = img.getchannel("A").point(lambda v: 255 if v > 2 else 0).getbbox()
        if not bbox:
            continue
        l, t, r, b = bbox
        l, t = max(l - 2, 0), max(t - 2, 0)
        r, b = min(r + 2, W), min(b + 2, H)
        crop = img.crop((l, t, r, b))
        crop.save(os.path.join(art_dir, layer.name + ".png"))
        psd_layers.append((layer.name, crop, l, t))
        hidden_by_default = layer.name.startswith(HIDDEN_LAYERS)
        if not hidden_by_default:
            composite.alpha_composite(img)
        rig_layers.append(dict(name=layer.name, file=layer.name + ".png", group=layer.group, order=order,
                               depth=layer.depth, rect=[l, t, r - l, b - t]))

    rig = dict(id=cfg["id"], title=cfg["title"], canvas=[W, H],
               groups=[dict(name=n, parent=p, pivot=list(pv), swing=SWING.get(n, 0.0)) for n, (p, pv) in groups(cfg).items()],
               layers=rig_layers,
               defaults=dict(ParamEyeLOpen=cfg.get("eye_open", 1.0), ParamEyeROpen=cfg.get("eye_open", 1.0)))
    with open(os.path.join(art_dir, "rig.json"), "w", encoding="utf-8") as f:
        json.dump(rig, f, indent=1)

    os.makedirs(OUT_PSD, exist_ok=True)
    write_psd(os.path.join(OUT_PSD, cfg["id"] + ".psd"), psd_layers, composite)
    return composite


def main():
    only = set(sys.argv[1:])
    sheet = Image.new("RGBA", (W * len(PERSONAS) // 2, H // 2), (236, 228, 246, 255))
    for i, cfg in enumerate(PERSONAS):
        if only and cfg["id"] not in only:
            continue
        comp = export(cfg)
        sheet.alpha_composite(comp.resize((W // 2, H // 2), Image.LANCZOS), (i * W // 2, 0))
        print("wrote", cfg["id"])
    os.makedirs(OUT_PREVIEW, exist_ok=True)
    sheet.save(os.path.join(OUT_PREVIEW, "portraits_sheet.png"))


if __name__ == "__main__":
    main()
