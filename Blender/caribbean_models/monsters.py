"""Sea monsters.

SeaSerpent.fbx : Head, Jaw, Segment, Tail      (SeaSerpent.cs chains them)
Kraken.fbx     : Head, TentacleSeg, TentacleTip       (Kraken.cs builds the tentacles)
All parts point forward along +z (game space); body pieces extend backwards from
their pivot (the joint with the previous piece).  Tentacle pieces grow along +y.
"""

import math
import random

from .core import MB, empty


def _ring(cx, cy, z, w, h, n=8, rot=0.0):
    pts = []
    for i in range(n):
        a = 2 * math.pi * i / n + rot
        pts.append((cx + math.cos(a) * w / 2, cy + math.sin(a) * h / 2, z))
    return pts


def _tube(mb, rings_spec, mat_top, mat_bottom, n=8, belly_y=None):
    """rings_spec: list of (z, w, h, cy). Faces below belly_y get mat_bottom."""
    rings = [_ring(0.0, cy, z, w, h, n, rot=math.pi / n) for (z, w, h, cy) in rings_spec]

    def mf(i, j):
        a = 2 * math.pi * (j + 0.5) / n + math.pi / n
        return mat_bottom if math.sin(a) < -0.35 else mat_top

    mb.loft(rings, mf, closed=True)
    return rings


def build_serpent():
    root = empty("SeaSerpent")
    # ---------------- head ----------------
    h = MB()
    spec = [(0.0, 1.5, 1.35, 0.15), (0.8, 1.6, 1.35, 0.2), (1.8, 1.2, 0.95, 0.15), (2.6, 0.8, 0.55, 0.05), (2.95, 0.42, 0.3, 0.0)]
    rings = _tube(h, spec, "SerpentScale", "SerpentBelly")
    h.poly(list(reversed(rings[-1])), "SerpentScale")  # snout cap
    h.poly(rings[0], "SerpentScale")                   # neck cap
    for sx in (-1, 1):
        h.sphere((sx * 0.62, 0.62, 1.15), 0.21, "EyeYellow", segs=8, rings=6)
        h.box((sx * 0.8, 0.62, 1.2), (0.04, 0.28, 0.07), "EyeBlack")
        h.box_between((sx * 0.35, 0.92, 1.4), (sx * 0.8, 0.82, 0.85), 0.12, 0.1, "SerpentFin")  # brow
        h.cyl_between((sx * 0.45, 0.8, 0.35), (sx * 0.8, 1.55, -0.65), 0.17, 0.02, "Bone", segs=6)
        h.poly([(sx * 0.72, 0.35, 0.35), (sx * 1.55, 1.0, -0.55), (sx * 1.3, -0.25, -0.65)][::sx], "SerpentFin",
               double_sided=True, thickness=0.04)
        h.box((sx * 0.13, 0.28, 2.88), (0.06, 0.05, 0.05), "EyeBlack")
        for k in range(5):
            z = 1.0 + k * 0.38
            w = 0.5 - k * 0.07
            h.cyl_between((sx * w, -0.28, z), (sx * w, -0.55, z + 0.05), 0.06, 0.0, "Bone", segs=4)
    head = h.to_object("Head", origin=(0, 0, 0), parent=root, recalc=True)
    j = MB()
    jspec = [(0.15, 1.2, 0.55, -0.45), (1.5, 0.95, 0.45, -0.45), (2.6, 0.55, 0.28, -0.4)]
    jr = _tube(j, jspec, "SerpentScale", "SerpentBelly")
    j.poly(list(reversed(jr[-1])), "SerpentScale")
    j.poly(jr[0], "SerpentScale")
    for sx in (-1, 1):
        for k in range(5):
            z = 0.9 + k * 0.36
            w = 0.42 - k * 0.06
            j.cyl_between((sx * w, -0.25, z), (sx * w, 0.0, z + 0.04), 0.05, 0.0, "Bone", segs=4)
    j.to_object("Jaw", origin=(0, -0.2, 0.25), parent=root, recalc=True)

    # ---------------- body segment ----------------
    s = MB()
    sspec = [(0.0, 1.95, 1.85, 0.0), (-1.2, 2.05, 1.95, 0.0), (-2.45, 1.9, 1.8, 0.0)]
    sr = _tube(s, sspec, "SerpentScale", "SerpentBelly")
    s.poly(sr[0], "SerpentScale")
    s.poly(list(reversed(sr[-1])), "SerpentScale")
    s.poly([(0, 0.92, -0.25), (0, 1.95, -1.05), (0, 0.92, -2.1)], "SerpentFin", double_sided=True, thickness=0.05)
    for sx in (-1, 1):
        s.sphere((sx * 0.75, 0.55, -1.2), 0.18, "SerpentFin", scale=(0.4, 1, 1), segs=6, rings=4)
    s.to_object("Segment", origin=(0, 0, 0), parent=root, recalc=True)

    # ---------------- tail ----------------
    t = MB()
    tspec = [(0.0, 1.9, 1.8, 0.0), (-1.5, 1.3, 1.25, 0.05), (-3.0, 0.6, 0.55, 0.1), (-3.7, 0.15, 0.15, 0.1)]
    tr = _tube(t, tspec, "SerpentScale", "SerpentBelly")
    t.poly(tr[0], "SerpentScale")
    t.poly(list(reversed(tr[-1])), "SerpentScale")
    t.poly([(0, 0.2, -2.4), (0, 1.5, -4.6), (0, -1.25, -4.3)], "SerpentFin", double_sided=True, thickness=0.05)
    t.poly([(0, 0.85, -0.3), (0, 1.5, -1.0), (0, 0.6, -1.8)], "SerpentFin", double_sided=True, thickness=0.05)
    t.to_object("Tail", origin=(0, 0, 0), parent=root, recalc=True)
    return root


def build_kraken():
    root = empty("Kraken")
    rng = random.Random(4)
    h = MB()
    h.sphere((0, 4.4, -0.8), 1.0, "KrakenSkin", scale=(4.3, 6.2, 4.3), segs=14, rings=10, jitter=0.12, seed=2)
    h.sphere((0, 0.6, 0.6), 1.0, "KrakenSkin", scale=(4.0, 2.2, 3.4), segs=12, rings=7, jitter=0.08, seed=3)
    for i in range(26):
        a = rng.uniform(0, 2 * math.pi)
        y = rng.uniform(2.5, 9.5)
        r = 4.1 * math.sqrt(max(0.05, 1 - ((y - 4.4) / 6.2) ** 2))
        x, z = math.sin(a) * r, math.cos(a) * r - 0.8
        h.sphere((x, y, z), rng.uniform(0.25, 0.55), "KrakenDark", scale=(1, 0.6, 1), segs=6, rings=4)
    for sx in (-1, 1):
        ex, ey, ez = sx * 1.9, 1.9, 3.15
        h.sphere((ex, ey, ez), 0.95, "EyeYellow", segs=12, rings=8)
        h.box((ex, ey, ez + 0.88), (0.95, 0.22, 0.12), "EyeBlack")
        h.box_between((ex - sx * 1.2, ey + 1.35, ez + 0.4), (ex + sx * 1.1, ey + 0.75, ez + 0.6), 0.4, 0.45, "KrakenDark")
    # beak + mouth tentacles
    h.cyl_between((0, 0.2, 3.4), (0, -0.9, 4.3), 0.45, 0.02, "EyeBlack", segs=5)
    for i in range(6):
        x = -2.4 + i * 0.96
        h.cyl_between((x, 0.0, 2.6), (x * 1.25, -2.2, 3.4 + rng.uniform(-0.3, 0.5)), 0.42, 0.08, "KrakenSkin", segs=6)
    h.to_object("Head", origin=(0, 0, 0), parent=root, recalc=True)

    seg = MB()
    rings = []
    for (y, r) in ((0.0, 0.62), (0.9, 0.66), (1.85, 0.56)):
        rings.append([(math.cos(2 * math.pi * i / 8) * r, y, math.sin(2 * math.pi * i / 8) * r) for i in range(8)])
    seg.loft(rings, lambda i, j: "KrakenSkin", closed=True)
    seg.poly(rings[0], "KrakenSkin")
    seg.poly(list(reversed(rings[-1])), "KrakenSkin")
    for y in (0.45, 1.35):
        seg.cyl_between((0, y, 0.52), (0, y, 0.72), 0.2, 0.17, "KrakenSucker", segs=8)
    seg.to_object("TentacleSeg", origin=(0, 0, 0), parent=root, recalc=True)

    tip = MB()
    rings = []
    pts = [(0.0, 0.56, 0.0), (0.9, 0.42, 0.1), (1.7, 0.26, 0.3), (2.3, 0.1, 0.6), (2.55, 0.02, 0.8)]
    for (y, r, zoff) in pts:
        rings.append([(math.cos(2 * math.pi * i / 8) * r, y, zoff + math.sin(2 * math.pi * i / 8) * r) for i in range(8)])
    tip.loft(rings, lambda i, j: "KrakenSkin", closed=True)
    tip.poly(rings[0], "KrakenSkin")
    tip.poly(list(reversed(rings[-1])), "KrakenSkin")
    for (y, r, zoff) in pts[:3]:
        tip.cyl_between((0, y + 0.3, zoff + r * 0.85), (0, y + 0.3, zoff + r + 0.1), 0.13 * r / 0.5, 0.11 * r / 0.5, "KrakenSucker", segs=6)
    tip.to_object("TentacleTip", origin=(0, 0, 0), parent=root, recalc=True)
    return root


MODELS = {
    "SeaSerpent": (build_serpent, dict(water=False, yaw=40, pitch=20)),
    "Kraken": (build_kraken, dict(water=False, yaw=30, pitch=15)),
}
