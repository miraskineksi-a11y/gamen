"""Ships: player sloop, pirate sloop, pirate brig, rowboat and a wreck.

Naming contract with the Unity scripts (see Assets/Scripts/Ship/Ship.cs):
  Hull                  - hull, deck, cabin, deck props (gets the BoxCollider)
  Rigging               - masts, yards, bowsprit, ropes
  Sail_*                - each sail; pivot on its yard so scaling Y furls it
  Flag                  - pivot on the flag pole (rotated to follow the wind)
  Wheel                 - ship's wheel, pivot at the hub, axis = local Z
  CannonGun_<L|R>_<k>   - one cannon (k = 1..n from stern to bow)
  CannonMuzzle_<L|R>_<k>- empty at the muzzle (direct child of the root, like everything)
  Helm, Wake, Bow       - empties
"""

import math

from .core import MB, V, empty, lerp, smoothstep


class HullShape:
    def __init__(self, L, B, F, D, sheer=0.8, rake=0.08, transom=0.74):
        self.L, self.B, self.F, self.D = L, B, F, D
        self.sheer, self.rake, self.transom = sheer, rake, transom

    def halfw(self, u):
        if u <= 0.45:
            k = self.transom + (1 - self.transom) * math.sin(u / 0.45 * math.pi / 2)
        else:
            k = max(0.0, math.cos((u - 0.45) / 0.55 * math.pi / 2)) ** 0.7
        return self.B * 0.5 * k

    def ztop(self, u):
        if u > 0.5:
            return self.F + self.sheer * ((u - 0.5) / 0.5) ** 2
        return self.F + self.sheer * 0.55 * ((0.5 - u) / 0.5) ** 2

    def zbot(self, u):
        return -self.D * (1 - 0.85 * smoothstep(0.72, 1.0, u)) * (1 - 0.15 * smoothstep(0.2, 0.0, u))

    @staticmethod
    def shape(t):
        s = math.sin(min(1.0, t / 0.75) * math.pi / 2) ** 0.55
        if t > 0.75:
            s *= 1 - 0.07 * (t - 0.75) / 0.25
        return s

    def fwd(self, u, t):
        z = -self.L / 2 + u * self.L
        z += self.rake * self.L * (t - 0.4) * smoothstep(0.6, 1.0, u)
        z -= 0.04 * self.L * t * smoothstep(0.15, 0.0, u)
        return z

    def point(self, u, t, side=1):
        w = self.halfw(u) * self.shape(t)
        y = lerp(self.zbot(u), self.ztop(u), t)
        return (side * w, y, self.fwd(u, t))

    def deck_point(self, u, side=1):
        return self.point(u, 1.0, side)


def build_hull(mb, hs, stripe="PaintRed", bottom="Copper", stations=16, rail_h=0.75, rail_t=0.12,
               deck_mat="WoodLight", open_boat=False):
    tw = hs.D / (hs.D + hs.F)
    rows = [1.0, 0.9, 0.78, 0.62, tw, tw * 0.55, tw * 0.2, 0.0]
    us = [i / (stations - 1) for i in range(stations)]
    rings = []
    for u in us:
        right = [hs.point(u, t, 1) for t in rows]           # top -> keel
        left = [hs.point(u, t, -1) for t in reversed(rows[:-1])]  # keel+1 -> top
        rings.append(right + left)

    band_mats = ["WoodDark", stripe, "Wood", "Wood", bottom, bottom, bottom]

    def mat_fn(i, j):
        band = j if j < 7 else 13 - j
        return band_mats[band]

    mb.loft(rings, mat_fn, closed=False, cap_start="Wood")

    # deck cap (polygon through top points; bow point only once)
    outline_r = [hs.deck_point(u, 1) for u in us]
    outline_l = [hs.deck_point(u, -1) for u in reversed(us[:-1])]
    deck = outline_r + outline_l
    deck_drop = 0.0 if not open_boat else hs.F * 0.55
    deck_pts = [(x, y - deck_drop, z) for (x, y, z) in deck]
    # winding: we want the normal pointing up; poly() uses the given order
    mb.poly(list(reversed(deck_pts)), deck_mat)

    # bulwark / gunwale ribbon around the deck edge (closed loop)
    loop = outline_r + outline_l + [outline_r[0]]
    rrings = []
    for (x, y, z) in loop:
        s = 1 if x > 1e-4 else (-1 if x < -1e-4 else 0)
        xi = x - s * min(rail_t, abs(x))
        yb = y - (deck_drop if open_boat else 0.0) - 0.02
        rrings.append([(x, yb, z), (x, y + rail_h, z), (xi, y + rail_h, z), (xi, yb, z)])
    mats = ["Wood", "WoodDark", "Wood", "Wood"]
    mb.loft(rrings, lambda i, j: mats[j], closed=True)
    return outline_r, outline_l


def sail_rows(top_y, bot_y, w_top, w_bot, z0, depth, nx=6, ny=4):
    rows = []
    for j in range(ny + 1):
        yi = j / ny
        y = lerp(top_y, bot_y, yi)
        w = lerp(w_top, w_bot, yi)
        row = []
        for i in range(nx + 1):
            xi = i / nx
            x = lerp(-w / 2, w / 2, xi)
            bulge = depth * math.sin(math.pi * xi) * math.sin(math.pi * min(yi, 1.0) * 0.9)
            row.append((x, y, z0 + bulge))
        rows.append(row)
    return rows


def build_sail(name, parent, mast_z, deck_y, top, bot, w_top, w_bot, depth, mat, stripe=None):
    mb = MB()
    rows = sail_rows(deck_y + top, deck_y + bot, w_top, w_bot, mast_z + 0.32, depth)
    if stripe:
        # three bands: sail / stripe / sail
        n = len(rows)
        a, b = rows[: n // 2], rows[n // 2 - 1:]
        mb.grid_surface(a, mat)
        mb.grid_surface(b[:2], stripe)
        mb.grid_surface(b[1:], mat)
    else:
        mb.grid_surface(rows, mat)
    return mb.to_object(name, origin=(0, deck_y + top, mast_z + 0.32), parent=parent)


def build_flag(parent, pos, w=1.7, h=1.05, mat="SailBlack", skull=True, emblem_mat="PaintWhite"):
    """Flag on top of a pole at game-space 'pos' (pole top). Flag trails towards -z."""
    mb = MB()
    x, y, z = pos
    mb.cyl((x, y - 1.3, z), 1.45, 0.05, 0.04, "WoodDark", segs=6)
    mb.sphere((x, y + 0.17, z), 0.08, "Gold", segs=6, rings=4)
    fy = y - 0.05 - h / 2
    mb.box((x, fy, z - 0.05 - w / 2), (0.04, h, w), mat)
    if skull:
        cx, cz = fy + 0.1, z - 0.05 - w / 2
        for sx in (1, -1):
            ex = x + sx * 0.03
            mb.cyl_between((ex, cx + 0.05, cz), (ex + sx * 0.02, cx + 0.05, cz), 0.2, 0.2, emblem_mat, segs=10)
            # jaw
            mb.box((ex + sx * 0.01, cx - 0.17, cz), (0.02, 0.12, 0.2), emblem_mat)
            # eyes
            for ez in (-0.07, 0.07):
                mb.box((ex + sx * 0.022, cx + 0.06, cz + ez), (0.01, 0.07, 0.06), "PaintBlack" if mat != "PaintBlack" else "PaintRed")
            # crossed bones
            for a in (1, -1):
                p0 = (ex + sx * 0.01, cx - 0.38 - 0.18 * a, cz - 0.42)
                p1 = (ex + sx * 0.01, cx - 0.38 + 0.18 * a, cz + 0.42)
                mb.box_between(p0, p1, 0.02, 0.07, emblem_mat, up=(1, 0, 0))
    return mb.to_object("Flag", origin=(x, y, z), parent=parent)


def build_cannon(parent, side, k, hs, u, rail_t=0.12):
    """One cannon on deck pointing outwards. side: +1 right, -1 left."""
    deck_y = hs.ztop(u)
    z = hs.fwd(u, 1.0)
    xo = hs.halfw(u) * hs.shape(1.0)
    y = deck_y + 0.42
    mb = MB()
    inner = (side * (xo - 1.15), y, z)
    muzzle = (side * (xo + 0.38), y, z)
    mb.cyl_between(inner, muzzle, 0.17, 0.12, "Iron", segs=8)
    mb.cyl_between((side * (xo + 0.28), y, z), (side * (xo + 0.4), y, z), 0.15, 0.15, "Iron", segs=8)
    mb.sphere((side * (xo - 1.2), y, z), 0.13, "Iron", segs=6, rings=4)
    mb.box((side * (xo - 0.75), deck_y + 0.17, z), (0.95, 0.3, 0.5), "WoodDark")
    for dz in (-0.22, 0.22):
        for dx in (-0.3, 0.3):
            mb.cyl_between((side * (xo - 0.75 + dx), deck_y + 0.12, z + dz),
                           (side * (xo - 0.75 + dx), deck_y + 0.12, z + dz + 0.06 * (1 if dz > 0 else -1)),
                           0.12, 0.12, "Wood", segs=6)
    # dark gun port on the outer face of the bulwark
    mb.box((side * (xo + 0.01), y, z), (0.03, 0.55, 0.6), "PaintBlack")
    sname = "R" if side > 0 else "L"
    gun = mb.to_object("CannonGun_%s_%d" % (sname, k), origin=(side * (xo - 0.75), deck_y, z), parent=parent)
    empty("CannonMuzzle_%s_%d" % (sname, k), (side * (xo + 0.45), y, z), parent=parent, size=0.3)
    return gun


def build_wheel(parent, pos):
    mb = MB()
    x, y, z = pos
    r = 0.55
    n = 10
    for i in range(n):
        a0 = 2 * math.pi * i / n
        a1 = 2 * math.pi * (i + 1) / n
        p0 = (x + r * math.cos(a0), y + r * math.sin(a0), z)
        p1 = (x + r * math.cos(a1), y + r * math.sin(a1), z)
        mb.box_between(p0, p1, 0.07, 0.07, "WoodDark", up=(0, 0, 1))
    for i in range(8):
        a = 2 * math.pi * i / 8 + 0.2
        p1 = (x + (r + 0.2) * math.cos(a), y + (r + 0.2) * math.sin(a), z)
        mb.cyl_between((x, y, z), p1, 0.03, 0.03, "Wood", segs=5)
    mb.cyl_between((x, y, z - 0.1), (x, y, z + 0.12), 0.1, 0.1, "Gold", segs=8)
    return mb.to_object("Wheel", origin=pos, parent=parent)


def deck_props(mb, hs, rng_seed=1):
    import random
    rng = random.Random(rng_seed)
    # barrels and crates near the main deck
    spots = [(0.62, 0.45), (0.66, -0.4), (0.38, 0.5), (0.72, 0.05)]
    for (u, sx) in spots:
        x = sx * hs.halfw(u) * 0.75
        y = hs.ztop(u)
        z = hs.fwd(u, 1.0)
        if rng.random() < 0.55:
            mb.cyl((x, y, z), 0.8, 0.3, 0.3, "Wood", segs=8)
            mb.cyl((x, y + 0.15, z), 0.06, 0.315, 0.315, "Iron", segs=8)
            mb.cyl((x, y + 0.6, z), 0.06, 0.315, 0.315, "Iron", segs=8)
        else:
            mb.box((x, y + 0.3, z), (0.6, 0.6, 0.6), "WoodLight", yaw=rng.uniform(-20, 20))
    # coiled rope
    u = 0.3
    mb.cyl((hs.halfw(u) * 0.5, hs.ztop(u), hs.fwd(u, 1.0)), 0.12, 0.32, 0.3, "Rope", segs=10)


def build_ship(name, L=14.0, B=4.6, F=1.4, D=1.5, masts=1, sail_mat="Sail", stripe="PaintRed",
               stripe_sail=None, guns=5, flag_mat="PaintBlue", skull=False, mast_h=12.5, seed=1):
    hs = HullShape(L, B, F, D, sheer=0.75 * L / 14.0)
    root = empty(name, (0, 0, 0))

    # ---------------- hull + cabin + props --------------------------------------
    mb = MB()
    build_hull(mb, hs, stripe=stripe)
    # stern cabin / quarterdeck
    u0, u1 = 0.02, 0.24
    zc0, zc1 = hs.fwd(u0, 1.0) + 0.15, hs.fwd(u1, 1.0)
    cw = hs.halfw(0.12) * 2 * 0.86
    cy = hs.ztop(0.12)
    ch = 1.25
    mb.box((0, cy + ch / 2, (zc0 + zc1) / 2), (cw, ch, zc1 - zc0), "Wood")
    mb.box((0, cy + ch + 0.06, (zc0 + zc1) / 2 - 0.05), (cw + 0.3, 0.12, zc1 - zc0 + 0.25), "WoodLight")
    mb.box((0, cy + 0.55, zc1 + 0.02), (0.8, 1.05, 0.06), "WoodDark")             # door
    mb.box((0, cy + 1.02, zc1 + 0.05), (0.9, 0.08, 0.06), "PaintYellow")
    for sx in (-1, 1):
        mb.box((sx * cw / 2, cy + 0.75, (zc0 + zc1) / 2), (0.06, 0.45, 0.6), "Glass")
        mb.box((sx * (cw / 2 + 0.01), cy + 0.75, (zc0 + zc1) / 2), (0.04, 0.55, 0.7), "PaintYellow")
    for wx in (-0.9, 0.0, 0.9):
        mb.box((wx * cw / 2.6, cy + 0.7, zc0 - 0.03), (0.5, 0.55, 0.06), "Glass")
        mb.box((wx * cw / 2.6, cy + 0.7, zc0 - 0.01), (0.6, 0.65, 0.04), "PaintYellow")
    # quarterdeck railing
    ry = cy + ch + 0.12
    corners = [(-cw / 2, zc0), (-cw / 2, zc1), (cw / 2, zc1), (cw / 2, zc0)]
    for (x, z) in corners:
        mb.box((x, ry + 0.3, z), (0.1, 0.6, 0.1), "WoodDark")
    mb.box_between((-cw / 2, ry + 0.6, zc0), (-cw / 2, ry + 0.6, zc1), 0.08, 0.08, "WoodDark")
    mb.box_between((cw / 2, ry + 0.6, zc0), (cw / 2, ry + 0.6, zc1), 0.08, 0.08, "WoodDark")
    mb.box_between((-cw / 2, ry + 0.6, zc0), (cw / 2, ry + 0.6, zc0), 0.08, 0.08, "WoodDark")
    # stern lanterns
    for sx in (-1, 1):
        lx, lz = sx * cw / 2, zc0
        mb.box((lx, ry + 0.95, lz), (0.22, 0.32, 0.22), "Lantern")
        mb.box((lx, ry + 1.13, lz), (0.28, 0.05, 0.28), "Iron")
        mb.box((lx, ry + 0.78, lz), (0.28, 0.05, 0.28), "Iron")
    # wheel pedestal
    wz = zc1 + 1.1
    wy = hs.ztop(0.27)
    mb.box((0, wy + 0.5, wz - 0.12), (0.2, 1.0, 0.2), "WoodDark")
    deck_props(mb, hs, seed)
    hull = mb.to_object("Hull", origin=(0, 0, 0), parent=root, recalc=False)

    build_wheel(root, (0, wy + 1.05, wz))
    empty("Helm", (0, wy, wz - 0.75), parent=root)
    empty("Wake", (0, 0.1, -L / 2 - 0.3), parent=root)
    empty("Bow", (0, hs.ztop(1.0), L / 2), parent=root)

    # ---------------- rigging: masts, yards, bowsprit, ropes ------------------------
    rg = MB()
    bow_top = hs.deck_point(0.97)
    bs0 = (0, bow_top[1] + 0.3, bow_top[2] - 1.0)
    bs1 = (0, bow_top[1] + 1.7, bow_top[2] + 3.2)
    rg.cyl_between(bs0, bs1, 0.16, 0.09, "WoodDark", segs=8)
    mast_us = [0.55] if masts == 1 else [0.36, 0.68]
    mast_tops = []
    sails = []
    for mi_, mu in enumerate(mast_us):
        mz = hs.fwd(mu, 1.0)
        dy = hs.ztop(mu)
        mh = mast_h * (1.0 if mi_ == 0 or masts == 1 else 0.92)
        rg.cyl((0, dy - 0.3, mz), mh + 0.3, 0.22, 0.12, "Wood", segs=8)
        top = (0, dy + mh, mz)
        mast_tops.append(top)
        # yards and sails (course + topsail)
        spec = [(0.58 * mh, 0.2 * mh, 0.6 * B * 1.25, 0.66 * B * 1.25, 0.95),
                (0.9 * mh, 0.6 * mh, 0.46 * B * 1.25, 0.57 * B * 1.25, 0.7)]
        for si, (ty, by, wt, wb, dep) in enumerate(spec):
            rg.cyl_between((-wt / 2 - 0.35, dy + ty, mz + 0.2), (wt / 2 + 0.35, dy + ty, mz + 0.2), 0.08, 0.08, "WoodDark", segs=6)
            sname = "Sail_%d_%d" % (mi_ + 1, si + 1)
            sails.append(build_sail(sname, root, mz, dy, ty, by, wt, wb, dep, sail_mat,
                                    stripe=stripe_sail if si == 0 else None))
        if mi_ == 0 and masts > 1 or (masts == 1):
            # crow's nest
            rg.cyl((0, dy + 0.74 * mh, mz), 0.35, 0.65, 0.7, "WoodDark", segs=8)
        # shrouds
        for sx in (-1, 1):
            for dz in (-0.5, 0.4):
                p = hs.deck_point(mu - dz / L * 2, sx)
                rg.cyl_between((p[0], p[1] + 0.75, p[2]), (0, dy + 0.74 * mh, mz), 0.025, 0.025, "Rope", segs=4)
    # stays
    rg.cyl_between(mast_tops[-1], bs1, 0.025, 0.025, "Rope", segs=4)
    if masts > 1:
        rg.cyl_between(mast_tops[0], (0, mast_tops[-1][1] * 0.8, mast_tops[-1][2]), 0.025, 0.025, "Rope", segs=4)
    rg.cyl_between(mast_tops[0], (0, cy + ch + 0.7, zc0 + 0.4), 0.025, 0.025, "Rope", segs=4)
    rg.to_object("Rigging", origin=(0, 0, 0), parent=root)

    # jib (triangular head sail) from the bowsprit to the front mast
    jb = MB()
    ft = mast_tops[-1]
    jt = (0, ft[1] * 0.82, ft[2] + 0.35)
    ja = (0, bs1[1] - 0.15, bs1[2] - 0.2)
    jk = (0, bow_top[1] + 1.0, bow_top[2] - 0.9)
    jb.poly([jt, ja, jk], sail_mat, double_sided=True, thickness=0.03)
    jb.poly([jt, jk, ja], sail_mat, double_sided=False)
    jb.to_object("Sail_Jib", origin=jt, parent=root)

    # flag on the main mast
    mt = mast_tops[0]
    build_flag(root, (0, mt[1] + 1.3, mt[2]), mat=flag_mat, skull=skull)

    # cannons
    for side in (1, -1):
        for k in range(guns):
            u = lerp(0.3, 0.78, (k + 0.5) / guns) if guns > 1 else 0.55
            # keep cannons clear of masts
            for mu in mast_us:
                if abs(u - mu) < 0.035:
                    u += 0.045
            build_cannon(root, side, k + 1, hs, u)
    return root


def build_rowboat(name="Rowboat", parent=None, pos=None, bearing=0.0, mesh_name="Boat"):
    """Standalone: root empty + mesh 'Boat'.  With 'parent': a single mesh object
    placed at game-space 'pos' with its bow pointing along 'bearing' (baked into the
    mesh so the hierarchy stays one level deep)."""
    hs = HullShape(3.8, 1.35, 0.45, 0.28, sheer=0.25, rake=0.05, transom=0.55)
    root = empty(name, (0, 0, 0)) if parent is None else parent
    mb = MB()
    build_hull(mb, hs, stripe="PaintBlue", bottom="Wood", stations=10, rail_h=0.12, rail_t=0.07,
               deck_mat="WoodLight", open_boat=True)
    for u in (0.3, 0.62):
        w = hs.halfw(u) * 1.7
        mb.box((0, hs.ztop(u) - 0.12, hs.fwd(u, 1.0)), (w, 0.06, 0.32), "Wood")
    # oars resting across the boat
    for sx in (-1, 1):
        mb.cyl_between((sx * 0.35, 0.45, -1.2), (sx * 0.5, 0.42, 1.5), 0.035, 0.035, "WoodLight", segs=5)
        mb.box((sx * 0.5, 0.42, 1.6), (0.08, 0.04, 0.5), "WoodLight")
    ob = mb.to_object(mesh_name, origin=(0, 0, 0), parent=root if parent is None else None)
    if parent is not None:
        import math as _m
        from mathutils import Matrix
        ob.data.transform(Matrix.Rotation(_m.radians(2.0), 4, "X"))
        ob.data.transform(Matrix.Rotation(_m.radians(-bearing), 4, "Z"))
        ob.location = V(*pos)
        from .core import set_parent
        set_parent(ob, parent)
        return ob
    return root
