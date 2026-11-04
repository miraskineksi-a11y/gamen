"""Props: vegetation, buildings, camp items, treasure items and other small models.

Most functions add geometry into a given MB (mesh builder) relative to a base
point in game space, so islands can create one object per prop.  The MODELS dict
at the bottom lists the props exported as standalone FBX files.
"""

import math
import random

from .core import MB, V, empty, lerp


class Xform:
    """Game-space transform: rotate (yaw clockwise from above, then optional roll
    around the forward axis) and translate."""

    def __init__(self, base=(0, 0, 0), yaw=0.0, roll=0.0, scale=1.0):
        self.base = base
        self.c, self.s = math.cos(math.radians(yaw)), math.sin(math.radians(yaw))
        self.cr, self.sr = math.cos(math.radians(roll)), math.sin(math.radians(roll))
        self.k = scale
        self.yaw = yaw

    def p(self, x, y, z):
        x, y, z = x * self.k, y * self.k, z * self.k
        # roll around forward (z)
        x, y = x * self.cr - y * self.sr, x * self.sr + y * self.cr
        # yaw (Unity convention: +90 turns forward towards right)
        x, z = x * self.c + z * self.s, -x * self.s + z * self.c
        bx, by, bz = self.base
        return (bx + x, by + y, bz + z)


# ---------------------------------------------------------------------------
# Vegetation / nature
# ---------------------------------------------------------------------------

def palm(mb, base, h=7.0, lean=(0.25, 0.1), seed=0, leaves=7):
    rng = random.Random(seed)
    bx, by, bz = base
    n = 6
    pts = []
    for i in range(n + 1):
        t = i / n
        pts.append((bx + lean[0] * h * t ** 1.6, by + h * t, bz + lean[1] * h * t ** 1.6))
    for i in range(n):
        r0 = lerp(0.3, 0.17, i / n)
        r1 = lerp(0.3, 0.17, (i + 1) / n)
        mb.cyl_between(pts[i], pts[i + 1], r0, r1 * 0.92, "PalmTrunk", segs=6)
        mb.cyl_between(pts[i + 1], (pts[i + 1][0], pts[i + 1][1] + 0.08, pts[i + 1][2]), r1 * 1.08, r1 * 0.95, "Wood", segs=6)
    top = pts[-1]
    for k in range(3):
        a = 2 * math.pi * k / 3 + rng.uniform(0, 1)
        mb.sphere((top[0] + 0.24 * math.cos(a), top[1] - 0.25, top[2] + 0.24 * math.sin(a)), 0.16, "Coconut", segs=6, rings=4)
    for k in range(leaves):
        ang = 2 * math.pi * k / leaves + rng.uniform(-0.25, 0.25)
        dx, dz = math.cos(ang), math.sin(ang)
        px, pz = -dz, dx
        L = rng.uniform(3.0, 4.2) * h / 7.0
        droop = rng.uniform(0.75, 1.15)
        rows = []
        for j in range(6):
            s = j / 5
            w = 0.55 * math.sin(math.pi * (0.12 + 0.88 * s)) * (1.0 - 0.35 * s) * h / 7.0
            sx = top[0] + dx * L * s
            sz = top[2] + dz * L * s
            sy = top[1] + 0.15 + L * (0.42 * s - droop * 0.55 * s * s)
            rows.append([(sx - px * w, sy - 0.18 * w, sz - pz * w), (sx, sy + 0.05, sz), (sx + px * w, sy - 0.18 * w, sz + pz * w)])
        mb.grid_surface(rows, "PalmLeaf", double_sided=True, thickness=0.035)
    return top


def bush(mb, pos, size=1.0, seed=0, mat="Leaf", mat2="GrassDark"):
    rng = random.Random(seed)
    for i in range(rng.randint(2, 3)):
        off = (rng.uniform(-0.6, 0.6) * size, rng.uniform(0.0, 0.25) * size, rng.uniform(-0.6, 0.6) * size)
        r = rng.uniform(0.55, 0.9) * size
        mb.sphere((pos[0] + off[0], pos[1] + off[1] + r * 0.45, pos[2] + off[2]), r,
                  mat if i % 2 == 0 else mat2, scale=(1, 0.75, 1), ico=1, jitter=0.12 * r, seed=seed * 13 + i)


def dead_tree(mb, base, h=5.0, seed=0):
    rng = random.Random(seed)
    bx, by, bz = base
    top = (bx + rng.uniform(-0.6, 0.6), by + h, bz + rng.uniform(-0.6, 0.6))
    mb.cyl_between((bx, by - 0.3, bz), top, 0.28, 0.1, "DeadWood", segs=6)
    for i in range(rng.randint(3, 5)):
        t = rng.uniform(0.45, 0.9)
        p0 = (lerp(bx, top[0], t), lerp(by, top[1], t), lerp(bz, top[2], t))
        a = rng.uniform(0, 2 * math.pi)
        L = rng.uniform(1.0, 2.2) * h / 5
        p1 = (p0[0] + math.cos(a) * L, p0[1] + rng.uniform(0.4, 1.2), p0[2] + math.sin(a) * L)
        mb.cyl_between(p0, p1, 0.09, 0.03, "DeadWood", segs=5)
        if rng.random() < 0.6:
            a2 = a + rng.uniform(-1, 1)
            p2 = (p1[0] + math.cos(a2) * L * 0.5, p1[1] + 0.5, p1[2] + math.sin(a2) * L * 0.5)
            mb.cyl_between(p1, p2, 0.035, 0.015, "DeadWood", segs=4)


def bones(mb, pos, seed=0, skull=True):
    rng = random.Random(seed)
    x, y, z = pos
    if skull:
        mb.sphere((x, y + 0.13, z), 0.15, "Bone", scale=(1, 0.9, 1.1), segs=8, rings=6)
        for ex in (-0.055, 0.055):
            mb.sphere((x + ex, y + 0.15, z + 0.13), 0.04, "EyeBlack", segs=5, rings=4)
    for i in range(rng.randint(3, 5)):
        a = rng.uniform(0, math.pi)
        L = rng.uniform(0.35, 0.6)
        cx, cz = x + rng.uniform(-0.7, 0.7), z + rng.uniform(-0.7, 0.7)
        p0 = (cx - math.cos(a) * L / 2, y + 0.04, cz - math.sin(a) * L / 2)
        p1 = (cx + math.cos(a) * L / 2, y + 0.04, cz + math.sin(a) * L / 2)
        mb.cyl_between(p0, p1, 0.035, 0.035, "Bone", segs=5)
        mb.sphere(p0, 0.055, "Bone", segs=5, rings=3)
        mb.sphere(p1, 0.055, "Bone", segs=5, rings=3)


# ---------------------------------------------------------------------------
# Small objects
# ---------------------------------------------------------------------------

def barrel(mb, base, h=1.0, r=0.38, mat="Wood", lying=False, yaw=0.0):
    x, y, z = base
    if lying:
        X = Xform(base, yaw)
        p0, p1 = X.p(0, r, -h / 2), X.p(0, r, h / 2)
        mb.cyl_between(p0, p1, r * 0.9, r * 0.9, mat, segs=10)
        q0, q1 = X.p(0, r, -h * 0.32), X.p(0, r, -h * 0.25)
        mb.cyl_between(q0, q1, r * 0.95, r * 0.95, "Iron", segs=10)
        q0, q1 = X.p(0, r, h * 0.25), X.p(0, r, h * 0.32)
        mb.cyl_between(q0, q1, r * 0.95, r * 0.95, "Iron", segs=10)
        return
    mb.cyl((x, y, z), h * 0.5, r * 0.88, r, mat, segs=10)
    mb.cyl((x, y + h * 0.5, z), h * 0.5, r, r * 0.88, mat, segs=10)
    for t in (0.12, 0.82):
        mb.cyl((x, y + h * t, z), 0.07, r * (0.96 if t < 0.5 else 0.94) + 0.015, r * (0.96 if t < 0.5 else 0.94) + 0.015, "Iron", segs=10)


def crate(mb, center, s=0.8, yaw=0.0, mat="WoodLight"):
    x, y, z = center
    mb.box((x, y + s / 2, z), (s, s, s), mat, yaw=yaw)
    X = Xform((x, y, z), yaw)
    e = s / 2 + 0.01
    for (a, b) in (((-e, 0.05, -e), (e, s - 0.05, -e)), ((-e, 0.05, e), (e, s - 0.05, e)),
                   ((e, 0.05, -e), (e, s - 0.05, e)), ((-e, 0.05, -e), (-e, s - 0.05, e))):
        mb.box_between(X.p(*a), X.p(*b), 0.07, 0.07, "WoodDark", up=(0, 0, 1) if a[1] != b[1] else (0, 1, 0))


def torch(mb, base, h=1.9):
    x, y, z = base
    mb.cyl((x, y - 0.3, z), h + 0.3, 0.07, 0.05, "WoodDark", segs=6)
    mb.cyl((x, y + h - 0.05, z), 0.25, 0.09, 0.12, "Iron", segs=6)
    mb.cyl((x, y + h + 0.18, z), 0.32, 0.11, 0.0, "Fire", segs=5)


def campfire(mb, base):
    x, y, z = base
    for i in range(8):
        a = 2 * math.pi * i / 8
        mb.rock((x + 0.75 * math.cos(a), y + 0.08, z + 0.75 * math.sin(a)), 0.2, "RockDark", seed=i + 3, ico=1)
    for i in range(3):
        a = math.pi * i / 3
        mb.cyl_between((x - 0.55 * math.cos(a), y + 0.12, z - 0.55 * math.sin(a)),
                       (x + 0.55 * math.cos(a), y + 0.22, z + 0.55 * math.sin(a)), 0.09, 0.09, "WoodDark", segs=6)
    mb.cyl((x, y + 0.15, z), 0.75, 0.38, 0.0, "Fire", segs=5)


def tent(mb, base, yaw=0.0, size=1.0):
    X = Xform(base, yaw, scale=size)
    w, d, h = 1.8, 2.4, 2.0
    a, b, c, dd = X.p(-w, 0, -d), X.p(w, 0, -d), X.p(w, 0, d), X.p(-w, 0, d)
    r0, r1 = X.p(0, h, -d), X.p(0, h, d)
    mb.poly([a, r0, r1, dd], "Canvas", double_sided=True, thickness=0.03)
    mb.poly([b, c, r1, r0], "Canvas", double_sided=True, thickness=0.03)
    mb.poly([a, b, r0], "Canvas", double_sided=True, thickness=0.03)
    mb.poly([c, dd, r1], "Leather", double_sided=True, thickness=0.03)
    mb.cyl_between(X.p(0, 0, -d - 0.2), X.p(0, h + 0.25, -d - 0.2), 0.06, 0.05, "WoodDark", segs=5)
    mb.cyl_between(X.p(0, 0, d + 0.2), X.p(0, h + 0.25, d + 0.2), 0.06, 0.05, "WoodDark", segs=5)


def house(mb, base, yaw=0.0, w=5.0, d=6.0, h=3.4, wall="Plaster", roof="Roof", seed=0, door_side=1):
    rng = random.Random(seed)
    X = Xform(base, yaw)
    bx, by, bz = base
    mb.box(X.p(0, h / 2 - 0.4, 0), (w, h + 0.8, d), wall, yaw=yaw)
    # stone foundation
    mb.box(X.p(0, -0.15, 0), (w + 0.25, 0.7, d + 0.25), "StoneDark", yaw=yaw)
    ov = 0.45
    rh = w * 0.42
    e = [X.p(-w / 2 - ov, h, -d / 2 - ov), X.p(w / 2 + ov, h, -d / 2 - ov),
         X.p(w / 2 + ov, h, d / 2 + ov), X.p(-w / 2 - ov, h, d / 2 + ov)]
    r = [X.p(0, h + rh, -d / 2 - ov), X.p(0, h + rh, d / 2 + ov)]
    mb.poly([e[0], e[3], r[1], r[0]], roof, double_sided=True, thickness=0.12)
    mb.poly([e[1], r[0], r[1], e[2]], roof, double_sided=True, thickness=0.12)
    g0 = [X.p(-w / 2, h, -d / 2 - 0.01), X.p(0, h + rh * 0.92, -d / 2 - 0.01), X.p(w / 2, h, -d / 2 - 0.01)]
    g1 = [X.p(w / 2, h, d / 2 + 0.01), X.p(0, h + rh * 0.92, d / 2 + 0.01), X.p(-w / 2, h, d / 2 + 0.01)]
    mb.poly(list(reversed(g0)), wall)
    mb.poly(list(reversed(g1)), wall)
    # door (front = +z side)
    dz = door_side * (d / 2 + 0.03)
    mb.box(X.p(0, 0.95, dz), (1.1, 1.9, 0.08), "WoodDark", yaw=yaw)
    mb.box(X.p(0, 1.95, dz), (1.3, 0.14, 0.12), "WoodLight", yaw=yaw)
    shutters = rng.choice(["PaintBlue", "PaintRed", "WoodLight", "PaintYellow"])
    for wx in (-w * 0.3, w * 0.3):
        mb.box(X.p(wx, 1.6, dz), (0.7, 0.8, 0.06), "Glass", yaw=yaw)
        mb.box(X.p(wx - 0.55, 1.6, dz), (0.32, 0.85, 0.08), shutters, yaw=yaw)
        mb.box(X.p(wx + 0.55, 1.6, dz), (0.32, 0.85, 0.08), shutters, yaw=yaw)
    for wz in (-d * 0.2, d * 0.2):
        for sx in (-1, 1):
            mb.box(X.p(sx * (w / 2 + 0.03), 1.6, wz), (0.06, 0.8, 0.7), "Glass", yaw=yaw)
    # chimney
    mb.box(X.p(w * 0.22, h + rh * 0.7, -d * 0.25), (0.6, rh * 0.9, 0.6), "StoneDark", yaw=yaw)


def church(mb, base, yaw=0.0):
    X = Xform(base, yaw)
    house(mb, base, yaw, w=7.0, d=11.0, h=5.5, wall="Plaster", roof="Roof", seed=42)
    tz = 5.5 + 1.5
    mb.box(X.p(0, 5.0, tz), (3.2, 11.0, 3.2), "Plaster", yaw=yaw)
    mb.box(X.p(0, 9.2, tz + 1.62), (1.2, 1.6, 0.06), "WoodDark", yaw=yaw)  # bell window
    mb.box(X.p(0, 10.6, tz), (3.5, 0.3, 3.5), "Stone", yaw=yaw)
    top = X.p(0, 10.75, tz)
    mb.cyl(top, 3.2, 2.45, 0.0, "Roof", segs=4, yaw=yaw + 45)
    c = X.p(0, 14.0, tz)
    mb.box((c[0], c[1] + 0.5, c[2]), (0.14, 1.2, 0.14), "Gold", yaw=yaw)
    mb.box((c[0], c[1] + 0.7, c[2]), (0.7, 0.14, 0.14), "Gold", yaw=yaw)
    mb.box(X.p(0, 1.5, tz + 1.62), (1.4, 2.8, 0.08), "WoodDark", yaw=yaw)


def lighthouse(mb, base, h=14.0):
    x, y, z = base
    mats = ["PaintWhite", "PaintRed"]
    n = 5
    for i in range(n):
        y0 = y + h * i / n
        r0 = lerp(2.6, 1.7, i / n)
        r1 = lerp(2.6, 1.7, (i + 1) / n)
        mb.cyl((x, y0 - (0.5 if i == 0 else 0), z), h / n + (0.5 if i == 0 else 0), r0, r1, mats[i % 2], segs=10)
    mb.cyl((x, y + h, z), 0.35, 2.5, 2.5, "StoneDark", segs=10)
    mb.cyl((x, y + h + 0.35, z), 1.9, 1.3, 1.3, "Lantern", segs=8)
    mb.cyl((x, y + h + 2.25, z), 1.6, 1.6, 0.15, "PaintRed", segs=8)
    for i in range(12):
        a = 2 * math.pi * i / 12
        mb.cyl((x + 2.3 * math.cos(a), y + h + 0.35, z + 2.3 * math.sin(a)), 0.8, 0.05, 0.05, "Iron", segs=4)
    mb.box((x, y + 1.2, z + 2.55), (1.1, 2.2, 0.15), "WoodDark")


def fort_wall(mb, p0, p1, h=4.0, t=2.0, cannons=2):
    x0, y0, z0 = p0
    x1, y1, z1 = p1
    yb = min(y0, y1) - 1.0
    mb.box_between((x0, yb + (h + 1) / 2, z0), (x1, yb + (h + 1) / 2, z1), t, h + 1.0, "Stone")
    L = math.hypot(x1 - x0, z1 - z0)
    n = int(L / 1.4)
    dx, dz = (x1 - x0) / L, (z1 - z0) / L
    nx, nz = -dz, dx
    for i in range(n):
        if i % 2:
            continue
        s = (i + 0.5) / n
        cx, cz = lerp(x0, x1, s), lerp(z0, z1, s)
        for side in (-1, 1):
            mb.box((cx + nx * side * (t / 2 - 0.2), yb + h + 1.0 + 0.35, cz + nz * side * (t / 2 - 0.2)), (0.6, 0.7, 0.6), "Stone",
                   yaw=math.degrees(math.atan2(dx, dz)))
    for k in range(cannons):
        s = (k + 1) / (cannons + 1)
        cx, cz = lerp(x0, x1, s), lerp(z0, z1, s)
        cy = yb + h + 1.0
        mb.cyl_between((cx - nx * 0.6, cy + 0.45, cz - nz * 0.6), (cx + nx * 1.6, cy + 0.5, cz + nz * 1.6), 0.2, 0.15, "Iron", segs=8)
        mb.box((cx - nx * 0.3, cy + 0.15, cz - nz * 0.3), (0.7, 0.3, 0.7), "WoodDark")


def tower(mb, base, r=2.6, h=6.5, mat="Stone"):
    x, y, z = base
    mb.cyl((x, y - 1.0, z), h + 1.0, r, r * 0.92, mat, segs=10)
    for i in range(10):
        if i % 2:
            continue
        a = 2 * math.pi * (i + 0.5) / 10
        mb.box((x + (r * 0.85) * math.cos(a), y + h + 0.35, z + (r * 0.85) * math.sin(a)), (0.8, 0.7, 0.8), mat)


def dock(mb, p_shore, p_sea, deck_y=1.3, width=3.2):
    x0, _, z0 = p_shore
    x1, _, z1 = p_sea
    L = math.hypot(x1 - x0, z1 - z0)
    dx, dz = (x1 - x0) / L, (z1 - z0) / L
    nx, nz = -dz, dx
    yaw = math.degrees(math.atan2(dx, dz))
    n = int(L / 0.6)
    for i in range(n):
        s = (i + 0.5) / n
        cx, cz = lerp(x0, x1, s), lerp(z0, z1, s)
        mb.box((cx, deck_y, cz), (width, 0.12, 0.52), "WoodLight" if i % 3 else "Wood", yaw=yaw)
    m = int(L / 3.2) + 1
    for i in range(m + 1):
        s = i / m
        cx, cz = lerp(x0, x1, s), lerp(z0, z1, s)
        for side in (-1, 1):
            px, pz = cx + nx * side * (width / 2 - 0.1), cz + nz * side * (width / 2 - 0.1)
            mb.cyl((px, -4.0, pz), deck_y + 4.0 + (0.5 if i % 2 == 0 else 0.0), 0.16, 0.14, "WoodDark", segs=6)
    for side in (-1, 1):
        mb.cyl((x1 - dx * 0.8 + nx * side * 1.0, deck_y, z1 - dz * 0.8 + nz * side * 1.0), 0.5, 0.18, 0.2, "Iron", segs=8)


def gallows(mb, base, yaw=0.0):
    X = Xform(base, yaw)
    mb.box(X.p(0, 0.5, 0), (3.6, 1.0, 3.0), "WoodLight", yaw=yaw)
    for sx in (-1, 1):
        mb.box(X.p(sx * 1.3, 2.9, -0.8), (0.25, 4.0, 0.25), "WoodDark", yaw=yaw)
    mb.box(X.p(0, 4.95, -0.8), (3.1, 0.25, 0.3), "WoodDark", yaw=yaw)
    mb.box_between(X.p(-1.3, 3.6, -0.8), X.p(-0.5, 4.8, -0.8), 0.15, 0.15, "WoodDark")
    mb.cyl_between(X.p(0.4, 4.85, -0.8), X.p(0.4, 3.6, -0.8), 0.03, 0.03, "Rope", segs=4)
    for i in range(6):
        a0, a1 = 2 * math.pi * i / 6, 2 * math.pi * (i + 1) / 6
        mb.cyl_between(X.p(0.4 + 0.22 * math.sin(a0), 3.35 + 0.22 * math.cos(a0), -0.8),
                       X.p(0.4 + 0.22 * math.sin(a1), 3.35 + 0.22 * math.cos(a1), -0.8), 0.035, 0.035, "Rope", segs=4)
    for i in range(4):
        mb.box(X.p(0, 0.12 + i * 0.25, 1.6 + (3 - i) * 0.3), (1.4, 0.25, 0.3), "Wood", yaw=yaw)


def palisade(mb, center, radius=9.0, gap_bearing=180.0, gap=26.0, seed=0):
    rng = random.Random(seed)
    cx, cy, cz = center
    n = int(2 * math.pi * radius / 0.48)
    for i in range(n):
        b = 360.0 * i / n
        diff = (b - gap_bearing + 180) % 360 - 180
        if abs(diff) < gap / 2:
            continue
        a = math.radians(b)
        x, z = cx + radius * math.sin(a), cz + radius * math.cos(a)
        h = rng.uniform(2.6, 3.3)
        mb.cyl((x, cy - 0.8, z), h + 0.8, 0.24, 0.22, "Wood" if i % 2 else "WoodDark", segs=6)
        mb.cyl((x, cy + h, z), 0.45, 0.22, 0.0, "Wood", segs=6)


def watchtower(mb, base, h=5.0):
    x, y, z = base
    s = 1.2
    for sx in (-1, 1):
        for sz in (-1, 1):
            mb.cyl_between((x + sx * s * 1.1, y - 0.5, z + sz * s * 1.1), (x + sx * s, y + h + 1.6, z + sz * s), 0.14, 0.12, "WoodDark", segs=6)
    mb.box((x, y + h, z), (2 * s + 0.6, 0.2, 2 * s + 0.6), "WoodLight")
    for sx in (-1, 1):
        mb.box((x + sx * (s + 0.25), y + h + 0.6, z), (0.1, 0.1, 2 * s + 0.6), "WoodDark")
        mb.box((x, y + h + 0.6, z + sx * (s + 0.25)), (2 * s + 0.6, 0.1, 0.1), "WoodDark")
    mb.cyl((x, y + h + 1.6, z), 1.3, 2.3, 0.0, "Canvas", segs=4, yaw=45)
    # ladder
    for sx in (-0.3, 0.3):
        mb.cyl_between((x + sx, y - 0.2, z + s + 1.4), (x + sx, y + h, z + s + 0.3), 0.05, 0.05, "Wood", segs=4)
    for i in range(8):
        t = (i + 0.5) / 8
        mb.cyl_between((x - 0.3, lerp(y, y + h, t), lerp(z + s + 1.4, z + s + 0.3, t)),
                       (x + 0.3, lerp(y, y + h, t), lerp(z + s + 1.4, z + s + 0.3, t)), 0.035, 0.035, "Wood", segs=4)


def ruins(mb, center, seed=0, mat="Stone"):
    rng = random.Random(seed)
    cx, cy, cz = center
    for i in range(6):
        a = 2 * math.pi * i / 6
        x, z = cx + 4.0 * math.cos(a), cz + 4.0 * math.sin(a)
        h = rng.choice([0.8, 1.6, 2.5, 3.4, 3.6, 1.1])
        mb.cyl((x, cy - 0.5, z), h + 0.5, 0.45, 0.42, mat, segs=8)
        if h > 3:
            mb.box((x, cy + h + 0.15, z), (1.1, 0.3, 1.1), mat)
    mb.box((cx, cy + 0.1, cz), (6.5, 0.5, 6.5), "StoneDark")
    mb.box((cx - 1.0, cy + 1.0, cz + 4.6), (4.0, 2.0, 0.6), mat)
    mb.cyl_between((cx + 5.5, cy + 0.4, cz - 1.0), (cx + 7.5, cy + 0.4, cz + 1.5), 0.42, 0.42, mat, segs=8)
    mb.box((cx, cy + 0.6, cz), (1.6, 0.7, 1.0), "StoneDark")  # altar


def skull_rock(mb, base, size=12.0, yaw=0.0):
    X = Xform(base, yaw, scale=size)

    def P(x, y, z):
        return X.p(x, y, z)

    mb.sphere(P(0, 0.62, -0.05), size * 0.5, "Rock", scale=(1.0, 0.92, 1.05), segs=12, rings=8, jitter=size * 0.02, seed=3)
    mb.sphere(P(0, 0.34, 0.12), size * 0.37, "Rock", scale=(1.08, 0.8, 0.95), segs=10, rings=7, jitter=size * 0.015, seed=4)
    for ex in (-0.2, 0.2):
        mb.sphere(P(ex, 0.56, 0.39), size * 0.13, "RockDark", scale=(1.0, 0.9, 0.55), segs=8, rings=6)
    mb.cyl_between(P(0, 0.42, 0.44), P(0, 0.31, 0.47), size * 0.07, size * 0.02, "RockDark", segs=3)
    mb.box(P(0, 0.06, 0.12), (size * 0.72, size * 0.16, size * 0.62), "Rock", yaw=yaw)
    for i in range(6):
        tx = -0.25 + i * 0.1
        mb.box(P(tx, 0.19, 0.42), (size * 0.075, size * 0.1, size * 0.07), "Bone", yaw=yaw)
        mb.box(P(tx, 0.13, 0.4), (size * 0.075, size * 0.08, size * 0.07), "Bone", yaw=yaw)


def ship_wreck(mb, base, yaw=0.0, roll=25.0):
    from .ships import HullShape
    hs = HullShape(12.0, 4.2, 1.3, 1.4, sheer=0.6)
    X = Xform(base, yaw, roll=roll)
    tw = hs.D / (hs.D + hs.F)
    rows = [1.0, 0.9, 0.78, 0.62, tw, tw * 0.55, tw * 0.2, 0.0]
    rings = []
    for u in [0.32 + 0.68 * i / 9 for i in range(10)]:
        right = [hs.point(u, t, 1) for t in rows]
        left = [hs.point(u, t, -1) for t in reversed(rows[:-1])]
        rings.append([X.p(*p) for p in right + left])
    mats = ["WoodDark", "PaintRed", "Wood", "Wood", "Copper", "Copper", "Copper"]
    # make the hull double-sided so the broken interior is visible
    mb.loft(rings, lambda i, j: mats[j if j < 7 else 13 - j], closed=False)
    mb.loft([list(reversed(r)) for r in rings], lambda i, j: "WoodDark", closed=False)
    # broken mast lying on the sand
    mb.cyl_between(X.p(0.5, 0.5, 2.0), X.p(3.5, -0.4, 9.0), 0.22, 0.16, "Wood", segs=7)
    mb.cyl_between(X.p(-1.5, 2.2, 3.0), X.p(-1.0, 4.0, 3.2), 0.2, 0.17, "Wood", segs=7)


def turtle_rock(mb, base, yaw=0.0, size=7.0):
    X = Xform(base, yaw, scale=size)
    mb.sphere(X.p(0, 0.05, 0), size * 0.55, "GrassDark", scale=(1.0, 0.42, 1.25), segs=10, rings=7, jitter=size * 0.02, seed=9)
    mb.sphere(X.p(0, -0.05, 0), size * 0.6, "Rock", scale=(1.0, 0.3, 1.25), segs=10, rings=5, seed=10)
    mb.sphere(X.p(0, 0.08, 0.82), size * 0.2, "Rock", scale=(0.9, 0.8, 1.2), segs=8, rings=6)
    for ex in (-0.08, 0.08):
        mb.sphere(X.p(ex, 0.16, 0.98), size * 0.03, "EyeBlack", segs=5, rings=4)
    for sx in (-1, 1):
        for sz in (-1, 1):
            mb.sphere(X.p(sx * 0.55, -0.08, sz * 0.55), size * 0.15, "Rock", scale=(1.4, 0.5, 1.0), segs=7, rings=5)


def flag_pole(mb, base, h=6.0):
    from .ships import build_flag  # noqa: F401  (flag is built as separate object by islands)
    x, y, z = base
    mb.cyl((x, y - 0.5, z), h + 0.5, 0.1, 0.07, "WoodDark", segs=6)


# ---------------------------------------------------------------------------
# Standalone models (exported as their own FBX)
# ---------------------------------------------------------------------------

def build_cannonball():
    root = empty("Cannonball")
    mb = MB()
    mb.sphere((0, 0, 0), 0.17, "Iron", ico=1)
    mb.to_object("Ball", parent=root)


def build_barrel():
    root = empty("Barrel")
    mb = MB()
    barrel(mb, (0, -0.5, 0), h=1.0, r=0.4)
    mb.box((0, 0.52, 0), (0.25, 0.06, 0.25), "Gold")
    mb.to_object("Body", parent=root)


def build_chest(name="Chest", legendary=False):
    root = empty(name)
    s = 1.35 if legendary else 1.0
    w, h, d = 1.0 * s, 0.55 * s, 0.65 * s
    wood = "PaintRed" if legendary else "Wood"
    band = "Gold" if legendary else "Iron"
    mb = MB()
    mb.box((0, h / 2, 0), (w, h, d), wood)
    for bx in (-w * 0.32, w * 0.32):
        mb.box((bx, h / 2, 0), (0.08 * s, h + 0.02, d + 0.03), band)
    mb.box((0, h / 2, 0), (w + 0.03, 0.07 * s, d + 0.03), band)
    # gold mound visible when the lid opens
    mb.sphere((0, h - 0.02, 0), 0.42 * s, "Gold", scale=(1.05, 0.38, 0.65), segs=10, rings=5)
    for i in range(6):
        a = i * 1.1
        mb.cyl((0.3 * s * math.cos(a), h + 0.06 * s, 0.17 * s * math.sin(a)), 0.03, 0.08 * s, 0.08 * s, "Gold", segs=8)
    if legendary:
        mb.sphere((0.1 * s, h + 0.14 * s, 0), 0.08 * s, "EyeGlow", ico=1)
        mb.sphere((-0.2 * s, h + 0.12 * s, 0.08 * s), 0.07 * s, "PaintRed", ico=1)
    mb.to_object("ChestBase", origin=(0, 0, 0), parent=root)
    # lid: half cylinder, pivot on the back hinge
    lid = MB()
    n = 6
    r = d / 2
    rings = []
    for i in range(n + 1):
        a = math.pi * i / n
        rings.append((r * math.cos(a), h + r * math.sin(a) * 0.75))
    for sx in (-1, 1):
        pass
    # build as loft along x of the arc profile
    arcs = []
    for xx in (-w / 2, w / 2):
        arcs.append([(xx, yy, zz) for (zz, yy) in rings])
    lid.loft(arcs, lambda i, j: wood, closed=True, cap_start=wood, cap_end=wood)
    for bx in (-w * 0.32, w * 0.32):
        band_arcs = [[(xx, h + (yy - h) * 1.03, zz * 1.03) for (zz, yy) in rings] for xx in (bx - 0.045 * s, bx + 0.045 * s)]
        lid.loft(band_arcs, lambda i, j: band, closed=True, cap_start=band, cap_end=band)
    lid.box((0, h + 0.02, d / 2 + 0.03), (0.16 * s, 0.22 * s, 0.06), "Gold")
    lid.to_object("ChestLid", origin=(0, h, -d / 2), parent=root, recalc=True)


def build_xmark():
    root = empty("XMark")
    mb = MB()
    mb.sphere((0, -0.05, 0), 1.0, "Dirt", scale=(1.0, 0.18, 1.0), segs=10, rings=4)
    for a in (45, -45):
        mb.box((0, 0.16, 0), (0.28, 0.08, 1.6), "XMark", yaw=a)
    mb.to_object("Mark", parent=root)


def build_goldpile():
    root = empty("GoldPile")
    mb = MB()
    mb.sphere((0, 0.0, 0), 0.32, "Gold", scale=(1.0, 0.45, 1.0), segs=8, rings=4)
    rng = random.Random(5)
    for i in range(7):
        a = rng.uniform(0, 2 * math.pi)
        r = rng.uniform(0.15, 0.4)
        mb.cyl((r * math.cos(a), 0.05 + rng.uniform(0, 0.08), r * math.sin(a)), 0.03, 0.09, 0.09, "Gold", segs=8)
    mb.to_object("Coins", parent=root)


def build_shovel():
    root = empty("Shovel")
    mb = MB()
    # origin = grip point; the shovel hangs down along -y and slightly forward
    mb.cyl_between((0, 0.25, 0), (0, -0.95, 0.08), 0.03, 0.03, "WoodLight", segs=6)
    mb.box((0, 0.3, 0), (0.22, 0.05, 0.05), "WoodLight")
    mb.box((0, -1.13, 0.09), (0.3, 0.38, 0.04), "Iron")
    mb.to_object("Tool", parent=root)


MODELS = {
    "Cannonball": (build_cannonball, dict(yaw=30, pitch=20)),
    "Barrel": (build_barrel, dict(water=True, yaw=30, pitch=25)),
    "Chest": (lambda: build_chest("Chest"), dict(yaw=30, pitch=25)),
    "ChestLegend": (lambda: build_chest("ChestLegend", legendary=True), dict(yaw=30, pitch=25)),
    "XMark": (build_xmark, dict(yaw=30, pitch=45)),
    "GoldPile": (build_goldpile, dict(yaw=30, pitch=35)),
    "Shovel": (build_shovel, dict(yaw=60, pitch=15)),
}
