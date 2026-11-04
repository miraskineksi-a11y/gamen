"""Procedural islands.

Every island FBX contains:
  Terrain                 - the ground mesh (Unity adds a MeshCollider)
  Palm_*, Bush_*, Rock_*  - props (Unity adds simple colliders by name)
  House_*, Church, Fort_*, Lighthouse, Dock, Tent_*, Ruins, Gallows,
  Palisade, Watchtower, Wreck, SkullRock, TurtleRock, DeadTree_*, Bones_* ...
  Torch_*, Campfire_*     - Unity adds fire particles + a point light
  Empties (markers read by Unity):
    Landing               - where the captain goes ashore
    Anchor                - where the ship waits while the captain is ashore
    Treasure_Main         - chest with a piece of the treasure map
    Treasure_Legend       - the legendary treasure (Skull Island)
    Treasure_<n>          - chests with gold
    Spawn_Pirate_<n>, Spawn_Crab_<n>, Spawn_Skeleton_<n>
    PortDock              - entering the harbour (Port island only)
    KrakenLair            - where the Kraken lives (Skull Island only)
Coordinates are game space (x east, y up, z north) relative to the island centre.
"""

import math
import random

import bmesh
import bpy
from mathutils import Vector, noise
from mathutils.bvhtree import BVHTree

from . import props
from .core import MB, V, empty, lerp, smoothstep
from .ships import build_flag, build_rowboat


def bearing_dir(b):
    a = math.radians(b)
    return math.sin(a), math.cos(a)


class IslandSpec:
    def __init__(self, name, R, H, seed, landing=180.0, lobes=None, cliffs=(), flats=(),
                 grass=("Grass", "GrassDark"), rocky_top=False, beach=1.5):
        self.name, self.R, self.H, self.seed = name, R, H, seed
        self.landing = landing
        rng = random.Random(seed)
        self.lobes = lobes or [(k, rng.uniform(0.04, 0.12) / (k * 0.5), rng.uniform(0, 6.28)) for k in (2, 3, 4, 5)]
        self.cliffs = list(cliffs)        # (bearing, half_width_deg, strength 0..1)
        self.flats = list(flats)          # (x, z, radius, height)
        self.grass = grass
        self.rocky_top = rocky_top
        self.beach = beach
        self.noff = Vector((rng.uniform(0, 100), rng.uniform(0, 100), rng.uniform(0, 100)))

    def r_eff(self, b):
        a = math.radians(b)
        return self.R * (1 + sum(amp * math.sin(k * a + ph) for (k, amp, ph) in self.lobes))

    def cliff(self, b):
        c = 0.0
        for (cb, hw, s) in self.cliffs:
            d = abs((b - cb + 180) % 360 - 180)
            c = max(c, s * (1 - smoothstep(hw * 0.6, hw, d)))
        return c

    def height(self, x, z):
        d = math.hypot(x, z)
        b = math.degrees(math.atan2(x, z)) % 360
        t = d / self.r_eff(b)
        H = self.H
        dome = H * (1 - smoothstep(0.02, 0.84, t)) ** 1.15
        n = noise.fractal(Vector((x * 0.022, z * 0.022, 0.0)) + self.noff, 0.6, 2.0, 4)
        hills = n * H * 0.32 * (1 - smoothstep(0.5, 0.82, t))
        land = dome + hills
        beach = self.beach * (1 - smoothstep(0.78, 1.0, t))
        h = max(land, beach) if t < 1.0 else 0.0
        c = self.cliff(b)
        if c > 0:
            ch = (0.45 * H + 2.0) * c * (1 - smoothstep(0.86, 0.99, t))
            h = max(h, ch + dome * 0.3 * c) if t < 1.0 else h
        if t >= 1.0 or h < 0.05:
            under = -2.4 * smoothstep(1.0, 1.3, t) - 13.0 * smoothstep(1.3, 1.75, t)
            under -= 0.4 * abs(noise.noise(Vector((x * 0.05, z * 0.05, 3.0)) + self.noff))
            if t >= 1.0:
                h = under - 3.0 * c * smoothstep(1.0, 1.1, t)
        # flatten areas (town, camps, treasure spots)
        for (fx, fz, fr, fh) in self.flats:
            w = 1 - smoothstep(fr * 0.65, fr, math.hypot(x - fx, z - fz))
            if w > 0:
                h = lerp(h, fh, w)
        return h

    def polar(self, b, t):
        dx, dz = bearing_dir(b)
        r = self.r_eff(b) * t
        return dx * r, dz * r


class Island:
    def __init__(self, spec):
        self.spec = spec
        self.root = empty(spec.name, (0, 0, 0), size=5)
        self.rng = random.Random(spec.seed)
        self.exclusions = []      # (x, z, r)
        self.counters = {}
        self.bvh = None
        self.markers = {}

    # ---------------- terrain -------------------------------------------------
    def build_terrain(self):
        s = self.spec
        size = 2 * 1.85 * s.R
        n = int(max(44, min(92, size / 3.4)))
        step = size / n
        rng = random.Random(s.seed + 1)
        verts = []
        grid = []
        for j in range(n + 1):
            row = []
            for i in range(n + 1):
                x = -size / 2 + i * step
                z = -size / 2 + j * step
                if 0 < i < n and 0 < j < n:
                    x += rng.uniform(-0.28, 0.28) * step
                    z += rng.uniform(-0.28, 0.28) * step
                y = s.height(x, z)
                row.append(len(verts))
                verts.append((x, y, z))
            grid.append(row)
        bm = bmesh.new()
        bv = [bm.verts.new(V(*p)) for p in verts]
        mats = ["SandWet", "Sand", "Rock", s.grass[0], s.grass[1], "RockDark", "Dirt"]
        for j in range(n):
            for i in range(n):
                a, b_, c, d = grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]
                ys = [verts[k][1] for k in (a, b_, c, d)]
                if max(ys) < -12.5:
                    continue
                # alternate diagonal for a nicer triangle pattern
                if (i + j) % 2 == 0:
                    tris = [(a, d, c), (a, c, b_)]
                else:
                    tris = [(a, d, b_), (b_, d, c)]
                for tri in tris:
                    f = bm.faces.new([bv[k] for k in tri])
                    f.material_index = self._face_mat(tri, verts, mats)
        bm.normal_update()
        for f in bm.faces:
            if f.normal.z < 0:
                f.normal_flip()
        me = bpy.data.meshes.new("Terrain")
        bm.to_mesh(me)
        bm.free()
        from .core import get_material
        for m in mats:
            me.materials.append(get_material(m))
        ob = bpy.data.objects.new("Terrain", me)
        bpy.context.scene.collection.objects.link(ob)
        from .core import set_parent
        set_parent(ob, self.root)
        # BVH for prop placement (Blender space)
        bpy.context.view_layer.update()
        self.bvh = BVHTree.FromPolygons([Vector(v.co) for v in me.vertices], [tuple(p.vertices) for p in me.polygons])
        return ob

    def _face_mat(self, tri, verts, mats):
        s = self.spec
        p = [Vector(verts[k]) for k in tri]
        c = (p[0] + p[1] + p[2]) / 3
        nrm = (p[1] - p[0]).cross(p[2] - p[0])
        if nrm.length > 0:
            nrm.normalize()
        ny = abs(nrm.y)
        hc = c.y
        if hc < -0.35:
            return mats.index("SandWet")
        if hc < s.beach + 0.45 and ny > 0.8:
            return mats.index("Sand")
        if ny < 0.72:
            return mats.index("Rock")
        if s.rocky_top and hc > s.H * 0.78:
            return mats.index("RockDark")
        k = noise.noise(Vector((c.x * 0.045, c.z * 0.045, 7.0)) + s.noff)
        return mats.index(s.grass[1] if k > 0.12 else s.grass[0])

    # ---------------- queries -------------------------------------------------
    def ground(self, x, z):
        """Exact terrain height (game space) via raycast on the generated mesh."""
        if self.bvh is not None:
            hit = self.bvh.ray_cast(V(x, 500.0, z), Vector((0, 0, -1)))
            if hit[0] is not None:
                return hit[0].z
        return self.spec.height(x, z)

    def slope(self, x, z, e=1.5):
        hx = self.ground(x + e, z) - self.ground(x - e, z)
        hz = self.ground(x, z + e) - self.ground(x, z - e)
        return math.hypot(hx, hz) / (2 * e)

    def free(self, x, z, r):
        for (ex, ez, er) in self.exclusions:
            if math.hypot(x - ex, z - ez) < r + er:
                return False
        return True

    def exclude(self, x, z, r):
        self.exclusions.append((x, z, r))

    def name(self, prefix):
        self.counters[prefix] = self.counters.get(prefix, 0) + 1
        return "%s_%02d" % (prefix, self.counters[prefix])

    def marker(self, name, x, z, y=None, above=0.0):
        if y is None:
            y = self.ground(x, z)
        e = empty(name, (x, y + above, z), parent=self.root, size=1.5)
        self.markers[name] = (x, y + above, z)
        return e

    def obj(self, mb, prefix, origin, exact_name=None):
        return mb.to_object(exact_name or self.name(prefix), origin=origin, parent=self.root)

    # ---------------- placement helpers ---------------------------------------
    def scatter(self, count, fn, hmin, hmax, max_slope, r_excl, tmin=0.0, tmax=1.0, tries=40):
        placed = 0
        s = self.spec
        for _ in range(count * tries):
            if placed >= count:
                break
            b = self.rng.uniform(0, 360)
            t = math.sqrt(self.rng.uniform(tmin * tmin, tmax * tmax))
            x, z = s.polar(b, t)
            h = self.ground(x, z)
            if h < hmin or h > hmax:
                continue
            if self.slope(x, z) > max_slope:
                continue
            if not self.free(x, z, r_excl):
                continue
            fn(x, h, z)
            self.exclude(x, z, r_excl * 0.6)
            placed += 1
        return placed

    def palms(self, count, hmin=0.6, hmax=7.0, tmin=0.3, tmax=1.0):
        def f(x, y, z):
            mb = MB()
            seed = self.rng.randint(0, 99999)
            r = random.Random(seed)
            h = r.uniform(5.5, 8.5)
            # lean towards the sea
            d = math.hypot(x, z) or 1.0
            lean = (x / d * r.uniform(0.12, 0.32), z / d * r.uniform(0.12, 0.32))
            props.palm(mb, (x, y - 0.35, z), h=h, lean=lean, seed=seed)
            self.obj(mb, "Palm", (x, y, z))
        return self.scatter(count, f, hmin, hmax, 0.55, 2.2, tmin, tmax)

    def bushes(self, count, hmin=1.2, hmax=40, tmin=0.0, tmax=0.95, mat="Leaf", mat2="GrassDark"):
        def f(x, y, z):
            mb = MB()
            props.bush(mb, (x, y - 0.25, z), size=self.rng.uniform(0.8, 1.6), seed=self.rng.randint(0, 9999), mat=mat, mat2=mat2)
            self.obj(mb, "Bush", (x, y, z))
        return self.scatter(count, f, hmin, hmax, 0.7, 1.6, tmin, tmax)

    def rocks(self, count, hmin=-3.0, hmax=60, tmin=0.0, tmax=1.15, big=1.0, mat="Rock"):
        def f(x, y, z):
            mb = MB()
            r = self.rng.uniform(0.6, 2.2) * big
            mb.rock((x, y + r * 0.25, z), r, mat, scale=(1, self.rng.uniform(0.55, 0.9), self.rng.uniform(0.8, 1.2)),
                    seed=self.rng.randint(0, 999), ico=1 if r < 1.5 else 2)
            self.obj(mb, "Rock", (x, y, z))
        return self.scatter(count, f, hmin, hmax, 1.2, 1.5, tmin, tmax)

    def dead_trees(self, count, tmin=0.0, tmax=0.9):
        def f(x, y, z):
            mb = MB()
            props.dead_tree(mb, (x, y, z), h=self.rng.uniform(3.5, 6.5), seed=self.rng.randint(0, 9999))
            self.obj(mb, "DeadTree", (x, y, z))
        return self.scatter(count, f, 1.0, 40, 0.6, 2.0, tmin, tmax)

    def bone_piles(self, count, tmin=0.3, tmax=0.95):
        def f(x, y, z):
            mb = MB()
            props.bones(mb, (x, y - 0.02, z), seed=self.rng.randint(0, 9999))
            self.obj(mb, "Bones", (x, y, z))
        return self.scatter(count, f, 0.4, 40, 0.5, 1.5, tmin, tmax)

    def torch_at(self, x, z):
        y = self.ground(x, z)
        mb = MB()
        props.torch(mb, (x, y, z))
        self.obj(mb, "Torch", (x, y, z))
        self.exclude(x, z, 0.8)

    # ---------------- landing / anchor ------------------------------------------
    def landing(self, bearing=None, boat=True):
        s = self.spec
        b = s.landing if bearing is None else bearing
        dx, dz = bearing_dir(b)
        # walk outwards until we reach the water line
        t = 0.6
        x = z = 0.0
        while t < 1.4:
            x, z = s.polar(b, t)
            if self.ground(x, z) < 0.55:
                break
            t += 0.005
        lx, lz = x - dx * 3.0, z - dz * 3.0
        self.marker("Landing", lx, lz, above=0.1)
        self.exclude(lx, lz, 8.0)
        # anchor: in deeper water further out
        t2 = t
        while t2 < 2.5:
            ax, az = s.polar(b, t2)
            if self.ground(ax, az) < -7.0:
                break
            t2 += 0.01
        ax, az = s.polar(b, t2 + 0.1)
        self.marker("Anchor", ax, az, y=0.0)
        if boat:
            bx, bz = x + dx * 0.6, z + dz * 0.6
            by = max(self.ground(bx, bz), -0.25)
            self._place_rowboat(bx, by, bz, b)
        return lx, lz

    def _place_rowboat(self, x, y, z, bearing):
        # bow (game +z) pointing out to sea along 'bearing'; one mesh, one level deep
        build_rowboat(parent=self.root, pos=(x, y + 0.05, z), bearing=bearing, mesh_name=self.name("Rowboat"))
        self.exclude(x, z, 3.0)

    # ---------------- generic population ---------------------------------------
    def treasure(self, name, b, t, flat=True):
        s = self.spec
        x, z = s.polar(b, t)
        self.marker(name, x, z, above=0.05)
        self.exclude(x, z, 5.0)
        return x, z

    def spawns(self, kind, around, count, rmin=4.0, rmax=12.0, hmin=0.3):
        cx, cz = around
        made = 0
        for _ in range(count * 40):
            if made >= count:
                break
            a = self.rng.uniform(0, 2 * math.pi)
            r = self.rng.uniform(rmin, rmax)
            x, z = cx + math.cos(a) * r, cz + math.sin(a) * r
            h = self.ground(x, z)
            if h < hmin or self.slope(x, z) > 0.45:
                continue
            self.counters["Spawn_" + kind] = self.counters.get("Spawn_" + kind, 0) + 1
            self.marker("Spawn_%s_%d" % (kind, self.counters["Spawn_" + kind]), x, z, above=0.1)
            made += 1
        return made


# ---------------------------------------------------------------------------
# Specific islands
# ---------------------------------------------------------------------------

def _pre_flats(spec, entries):
    """entries: (bearing, t, radius, height or None). Height None = natural height."""
    for (b, t, r, h) in entries:
        x, z = spec.polar(b, t)
        if h is None:
            h = max(spec.height(x, z), spec.beach + 0.2)
        spec.flats.append((x, z, r, h))


def build_port():
    s = IslandSpec("Island_Port", R=85, H=13, seed=11, landing=180, cliffs=[(60, 40, 0.8)])
    _pre_flats(s, [(180, 0.5, 34, 2.4), (115, 0.74, 9, None)])
    isl = Island(s)
    isl.build_terrain()
    tx, tz = s.polar(180, 0.5)
    isl.exclude(tx, tz, 30)
    # houses along two streets
    rng = random.Random(3)
    walls = ["Plaster", "PlasterPink", "PlasterYellow", "PlasterBlue"]
    k = 0
    for row, zoff in enumerate((-9.0, 9.0)):
        for i in range(5):
            xoff = -16 + i * 8.2 + rng.uniform(-0.8, 0.8)
            if row == 1 and i == 2:
                continue
            x, z = tx + xoff, tz + zoff
            y = isl.ground(x, z)
            mb = MB()
            yaw = 180 if row == 1 else 0
            props.house(mb, (x, y, z), yaw=yaw, w=rng.uniform(5, 6.5), d=rng.uniform(5.5, 7), h=rng.uniform(3.2, 4.4),
                        wall=walls[k % 4], seed=k)
            isl.obj(mb, "House", (x, y, z))
            k += 1
    # church at the end of the street
    cx, cz = tx + 3.0, tz + 22.0
    mb = MB()
    props.church(mb, (cx, isl.ground(cx, cz), cz), yaw=180)
    isl.obj(mb, "Church", (cx, isl.ground(cx, cz), cz), exact_name="Church")
    # market crates / barrels in the square
    mb = MB()
    for i in range(6):
        x, z = tx + rng.uniform(-6, 6), tz + rng.uniform(-3, 3)
        if i % 2:
            props.crate(mb, (x, isl.ground(x, z), z), s=rng.uniform(0.7, 1.0), yaw=rng.uniform(0, 90))
        else:
            props.barrel(mb, (x, isl.ground(x, z), z))
    isl.obj(mb, "Market", (tx, isl.ground(tx, tz), tz), exact_name="Market")
    # fort on the south-west corner of the town with a tower
    f0 = s.polar(245, 0.8)
    f1 = s.polar(214, 0.84)
    mb = MB()
    props.fort_wall(mb, (f0[0], isl.ground(*f0), f0[1]), (f1[0], isl.ground(*f1), f1[1]), h=3.5, t=2.2, cannons=3)
    props.tower(mb, (f0[0], isl.ground(*f0), f0[1]), r=2.8, h=6.0)
    isl.obj(mb, "Fort", (f0[0], isl.ground(*f0), f0[1]), exact_name="Fort")
    # lighthouse on the eastern point
    lx, lz = s.polar(115, 0.74)
    mb = MB()
    props.lighthouse(mb, (lx, isl.ground(lx, lz), lz), h=13)
    isl.obj(mb, "Lighthouse", (lx, isl.ground(lx, lz), lz), exact_name="Lighthouse")
    isl.marker("LighthouseLamp", lx, lz, y=isl.ground(lx, lz) + 14.3)
    isl.exclude(lx, lz, 5)
    # dock: from the town to deep water
    b = 180
    t = 0.7
    while t < 1.5 and isl.ground(*s.polar(b, t)) > 0.3:
        t += 0.01
    p0 = s.polar(b, t - 0.08)
    p1 = s.polar(b, t + 0.42)
    mb = MB()
    props.dock(mb, (p0[0], 0, p0[1]), (p1[0], 0, p1[1]), deck_y=1.35)
    for i in range(4):
        u = 0.35 + 0.15 * i
        x, z = lerp(p0[0], p1[0], u) + (1.0 if i % 2 else -1.0), lerp(p0[1], p1[1], u)
        props.barrel(mb, (x, 1.4, z)) if i % 2 else props.crate(mb, (x, 1.4, z), s=0.8, yaw=20 * i)
    isl.obj(mb, "Dock", (p0[0], 0, p0[1]), exact_name="Dock")
    dx, dz = bearing_dir(b)
    isl.marker("PortDock", p1[0] + dx * 8, p1[1] + dz * 8, y=0.0)
    for k in range(3):
        u = 0.15 + 0.3 * k
        px, pz = lerp(p0[0], p1[0], u), lerp(p0[1], p1[1], u)
        isl.torch_at(px + 1.4, pz) if isl.ground(px + 1.4, pz) > 0.5 else None
    # torches in town
    for (ox, oz) in ((-6, -4), (6, -4), (-6, 4), (6, 4)):
        isl.torch_at(tx + ox, tz + oz)
    isl.palms(22, tmin=0.35)
    isl.bushes(14)
    isl.rocks(10, tmin=0.6)
    # moored boat by the dock
    isl._place_rowboat(p1[0] - 4.0, -0.2, lerp(p0[1], p1[1], 0.7), 200)
    return isl


def build_parrot():
    s = IslandSpec("Island_Parrot", R=66, H=17, seed=21, landing=225, cliffs=[(60, 35, 0.7)],
                   grass=("Grass", "GrassDark"))
    _pre_flats(s, [(20, 0.45, 9, None), (250, 0.55, 5, None)])
    isl = Island(s)
    isl.build_terrain()
    isl.landing()
    mx, mz = isl.treasure("Treasure_Main", 20, 0.45)
    isl.treasure("Treasure_1", 250, 0.55)
    rx, rz = s.polar(28, 0.47)
    mb = MB()
    props.ruins(mb, (rx + 4, isl.ground(rx + 4, rz), rz), seed=2)
    isl.obj(mb, "Ruins", (rx, isl.ground(rx, rz), rz), exact_name="Ruins")
    isl.exclude(rx + 4, rz, 7)
    isl.spawns("Crab", s.polar(140, 0.9), 2, 2, 10)
    isl.spawns("Crab", s.polar(300, 0.85), 2, 2, 10)
    isl.spawns("Pirate", (mx, mz), 2, 4, 9)
    isl.palms(34)
    isl.bushes(30)
    isl.rocks(12)
    return isl


def build_turtle():
    s = IslandSpec("Island_Turtle", R=58, H=10, seed=31, landing=200, grass=("Grass", "GrassDry"))
    _pre_flats(s, [(60, 0.35, 16, None), (200, 0.62, 5, None)])
    isl = Island(s)
    isl.build_terrain()
    isl.landing()
    cx, cz = s.polar(60, 0.35)
    cy = isl.ground(cx, cz)
    mx, mz = isl.treasure("Treasure_Main", 60, 0.35)
    isl.treasure("Treasure_1", 200, 0.62)
    # pirate camp
    mb = MB()
    fx, fz = cx + 4.5, cz - 3.0
    props.campfire(mb, (fx, isl.ground(fx, fz), fz))
    isl.obj(mb, "Campfire", (fx, isl.ground(fx, fz), fz))
    isl.exclude(fx, fz, 2.0)
    for i, (ox, oz, yaw) in enumerate(((-7, 5, 150), (7, 6, 210), (0, 10, 180))):
        mb = MB()
        x, z = cx + ox, cz + oz
        props.tent(mb, (x, isl.ground(x, z), z), yaw=yaw)
        isl.obj(mb, "Tent", (x, isl.ground(x, z), z))
        isl.exclude(x, z, 3)
    mb = MB()
    for i in range(7):
        x, z = cx + random.Random(i).uniform(-9, 9), cz + random.Random(i + 50).uniform(-9, -4)
        y = isl.ground(x, z)
        if i % 3 == 0:
            props.barrel(mb, (x, y, z), lying=(i == 3), yaw=40)
        else:
            props.crate(mb, (x, y, z), s=0.75, yaw=i * 17)
    isl.obj(mb, "Supplies", (cx, cy, cz), exact_name="Supplies")
    px, pz = cx - 9, cz - 2
    mb = MB()
    props.flag_pole(mb, (px, isl.ground(px, pz), pz), h=6.0)
    isl.obj(mb, "FlagPole", (px, isl.ground(px, pz), pz), exact_name="FlagPole")
    fl = build_flag(isl.root, (px, isl.ground(px, pz) + 6.0, pz), mat="PaintBlack", skull=True)
    fl.name = "CampFlag"
    isl.exclude(px, pz, 1.5)
    tx, tz = s.polar(110, 0.92)
    mb = MB()
    props.turtle_rock(mb, (tx, isl.ground(tx, tz) - 0.5, tz), yaw=110, size=8.0)
    isl.obj(mb, "TurtleRock", (tx, isl.ground(tx, tz), tz), exact_name="TurtleRock")
    isl.exclude(tx, tz, 7)
    isl.torch_at(cx - 4, cz + 1)
    isl.torch_at(cx + 9, cz + 1)
    isl.spawns("Pirate", (cx, cz), 5, 3, 11)
    isl.spawns("Crab", s.polar(300, 0.85), 2, 2, 8)
    isl.palms(18)
    isl.bushes(14)
    isl.rocks(9)
    return isl


def build_hangman():
    s = IslandSpec("Island_Hangman", R=70, H=21, seed=41, landing=160, cliffs=[(330, 55, 1.0), (60, 25, 0.8)],
                   grass=("GrassDry", "GrassDark"), rocky_top=True)
    _pre_flats(s, [(0, 0.3, 14, None), (120, 0.6, 5, None), (200, 0.45, 7, None)])
    isl = Island(s)
    isl.build_terrain()
    isl.landing()
    mx, mz = isl.treasure("Treasure_Main", 0, 0.3)
    isl.treasure("Treasure_1", 120, 0.6)
    my = isl.ground(mx, mz)
    mb = MB()
    props.palisade(mb, (mx, my, mz), radius=10.5, gap_bearing=180, gap=30, seed=4)
    isl.obj(mb, "Palisade", (mx, my, mz), exact_name="Palisade")
    isl.exclude(mx, mz, 12)
    wx, wz = mx + 6.5, mz + 4.5
    mb = MB()
    props.watchtower(mb, (wx, isl.ground(wx, wz), wz), h=5.0)
    isl.obj(mb, "Watchtower", (wx, isl.ground(wx, wz), wz), exact_name="Watchtower")
    gx, gz = s.polar(200, 0.45)
    mb = MB()
    props.gallows(mb, (gx, isl.ground(gx, gz), gz), yaw=160)
    isl.obj(mb, "Gallows", (gx, isl.ground(gx, gz), gz), exact_name="Gallows")
    isl.exclude(gx, gz, 4)
    for (ox, oz) in ((-3, -10.5), (3, -10.5)):
        isl.torch_at(mx + ox, mz + oz)
    isl.torch_at(gx + 2.5, gz + 2.5)
    isl.spawns("Pirate", (mx, mz), 4, 2, 8)
    isl.spawns("Pirate", (gx, gz), 2, 3, 7)
    isl.palms(12, tmin=0.5)
    isl.bushes(16)
    isl.rocks(18, big=1.3)
    isl.dead_trees(5)
    return isl


def build_mist():
    s = IslandSpec("Island_Mist", R=60, H=16, seed=51, landing=135, cliffs=[(300, 50, 0.9)],
                   grass=("GrassDead", "GrassDry"), rocky_top=True)
    _pre_flats(s, [(40, 0.4, 5, None), (300, 0.45, 5, None)])
    isl = Island(s)
    isl.build_terrain()
    isl.landing()
    isl.treasure("Treasure_1", 40, 0.4)
    isl.treasure("Treasure_2", 300, 0.45)
    wb = 200
    wx, wz = s.polar(wb, 0.97)
    mb = MB()
    props.ship_wreck(mb, (wx, isl.ground(wx, wz) - 1.0, wz), yaw=wb + 70, roll=28)
    isl.obj(mb, "Wreck", (wx, isl.ground(wx, wz), wz), exact_name="Wreck")
    isl.exclude(wx, wz, 8)
    isl.spawns("Skeleton", s.polar(40, 0.4), 2, 4, 9)
    isl.spawns("Skeleton", s.polar(300, 0.45), 2, 4, 9)
    isl.spawns("Crab", s.polar(80, 0.9), 2, 2, 8)
    isl.dead_trees(14)
    isl.bone_piles(6)
    isl.rocks(24, big=1.2)
    isl.palms(4, tmin=0.6)
    isl.bushes(8, mat="GrassDead", mat2="DeadWood")
    return isl


def build_sandbar():
    s = IslandSpec("Island_Sandbar", R=30, H=3.2, seed=61, landing=180, beach=1.3)
    isl = Island(s)
    isl.build_terrain()
    isl.landing()
    isl.treasure("Treasure_1", 20, 0.25)
    mb = MB()
    bx, bz = s.polar(300, 0.5)
    props.bones(mb, (bx, isl.ground(bx, bz), bz), seed=4)
    props.barrel(mb, (bx + 1.2, isl.ground(bx + 1.2, bz), bz + 0.4), lying=True, yaw=30)
    isl.obj(mb, "Castaway", (bx, isl.ground(bx, bz), bz), exact_name="Castaway")
    isl.spawns("Crab", (0, 0), 2, 5, 14)
    isl.palms(5, tmin=0.0, tmax=0.8)
    isl.rocks(4, tmin=0.6)
    return isl


def build_skull():
    s = IslandSpec("Island_Skull", R=80, H=25, seed=71, landing=200, cliffs=[(0, 70, 1.0), (100, 30, 0.7), (260, 30, 0.7)],
                   grass=("GrassDead", "RockDark"), rocky_top=True)
    _pre_flats(s, [(180, 0.55, 16, 3.0)])
    isl = Island(s)
    isl.build_terrain()
    isl.landing()
    # skull rock looking south over the bay
    kx, kz = s.polar(165, 0.42)
    ky = isl.ground(kx, kz)
    mb = MB()
    props.skull_rock(mb, (kx, ky - 1.0, kz), size=15.0, yaw=165)
    isl.obj(mb, "SkullRock", (kx, ky, kz), exact_name="SkullRock")
    isl.exclude(kx, kz, 10)
    lx, lz = s.polar(180, 0.6)
    isl.marker("Treasure_Legend", lx, lz, above=0.05)
    isl.exclude(lx, lz, 5)
    dx, dz = bearing_dir(180)
    r = s.r_eff(180)
    isl.marker("KrakenLair", dx * r * 2.1, dz * r * 2.1, y=0.0)
    for k in range(6):
        a = math.radians(140 + k * 16)
        x, z = lx + math.sin(a) * 9, lz + math.cos(a) * 9
        isl.torch_at(x, z)
    isl.spawns("Skeleton", (lx, lz), 6, 4, 13)
    isl.dead_trees(16)
    isl.bone_piles(8)
    isl.rocks(26, big=1.4, mat="RockDark")
    isl.rocks(10, big=1.0)
    isl.bushes(6, mat="GrassDead", mat2="DeadWood")
    return isl


MODELS = {
    "Island_Port": (build_port, dict(water=True, yaw=200, pitch=35, dist_mul=0.75)),
    "Island_Parrot": (build_parrot, dict(water=True, yaw=225, pitch=35, dist_mul=0.75)),
    "Island_Turtle": (build_turtle, dict(water=True, yaw=200, pitch=35, dist_mul=0.75)),
    "Island_Hangman": (build_hangman, dict(water=True, yaw=160, pitch=35, dist_mul=0.75)),
    "Island_Mist": (build_mist, dict(water=True, yaw=135, pitch=35, dist_mul=0.75)),
    "Island_Sandbar": (build_sandbar, dict(water=True, yaw=180, pitch=35, dist_mul=0.75)),
    "Island_Skull": (build_skull, dict(water=True, yaw=190, pitch=30, dist_mul=0.75)),
}
