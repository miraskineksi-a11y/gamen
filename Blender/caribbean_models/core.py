"""
Core helpers for building low-poly game models in Blender (bpy + bmesh).

Coordinate convention used by ALL model scripts
-----------------------------------------------
We design models in "game space" (the same axes Unity uses):
    x = right, y = up, z = forward
and convert to Blender space with  V(x, y, z) -> Blender (-x, -z, y).
In Blender this means the model faces -Y (the "front" view) with Z up.
The FBX exporter (forward=-Z, up=Y, "Apply Transform") plus Unity's
right->left handed conversion maps it back to exactly (x, y, z) in Unity,
so the model faces +Z (forward) in Unity with its right side on +X.
"""

import json
import math
import os
import random

import bmesh
import bpy
from mathutils import Euler, Matrix, Vector, noise

# ---------------------------------------------------------------------------
# Paths
# ---------------------------------------------------------------------------
BLENDER_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PROJECT_DIR = os.path.dirname(BLENDER_DIR)
PALETTE_PATH = os.path.join(PROJECT_DIR, "Assets", "Resources", "Palette.json")
FBX_DIR = os.path.join(PROJECT_DIR, "Assets", "Resources", "Models")
BLEND_DIR = os.path.join(BLENDER_DIR, "blend")
RENDER_DIR = os.path.join(BLENDER_DIR, "renders")

# ---------------------------------------------------------------------------
# Coordinates
# ---------------------------------------------------------------------------


def V(x=0.0, y=0.0, z=0.0):
    """Game-space (x right, y up, z forward) -> Blender Vector."""
    return Vector((-x, -z, y))


def SZ(w=1.0, h=1.0, d=1.0):
    """Game-space size (width x, height y, depth z) -> Blender scale tuple."""
    return (w, d, h)


def yaw_matrix(deg):
    """Rotation about the game 'up' axis. Positive = clockwise seen from above
    (Unity convention: turns forward towards right)."""
    return Matrix.Rotation(math.radians(-deg), 4, "Z")


def mat4(loc=(0, 0, 0), rot=(0, 0, 0), scale=(1, 1, 1)):
    """Blender-space TRS matrix. rot is Blender XYZ euler in degrees."""
    if isinstance(scale, (int, float)):
        scale = (scale, scale, scale)
    R = Euler([math.radians(a) for a in rot], "XYZ").to_matrix().to_4x4()
    S = Matrix.Diagonal((scale[0], scale[1], scale[2], 1.0))
    return Matrix.Translation(Vector(loc)) @ R @ S


def smoothstep(a, b, x):
    if a == b:
        return 0.0 if x < a else 1.0
    t = max(0.0, min(1.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


def lerp(a, b, t):
    return a + (b - a) * t


# ---------------------------------------------------------------------------
# Palette / materials
# ---------------------------------------------------------------------------
PALETTE = {}


def _hex_to_rgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4))


def _srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def load_palette(path=PALETTE_PATH):
    with open(path, "r", encoding="utf-8") as f:
        data = json.load(f)
    PALETTE.clear()
    for m in data["materials"]:
        PALETTE[m["name"]] = m
    return PALETTE


def _set_input(node, names, value):
    for n in names:
        if n in node.inputs:
            node.inputs[n].default_value = value
            return True
    return False


def get_material(name):
    """Get or create a Principled material whose name matches the palette entry.
    The name is what Unity uses to look the material up at runtime."""
    mat = bpy.data.materials.get(name)
    if mat is not None:
        return mat
    if not PALETTE:
        load_palette()
    p = PALETTE.get(name)
    if p is None:
        raise KeyError("Material '%s' is not in Palette.json" % name)
    rgb = _hex_to_rgb(p["color"])
    lin = tuple(_srgb_to_linear(c) for c in rgb)
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.diffuse_color = (lin[0], lin[1], lin[2], 1.0)
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf is not None:
        bsdf.inputs["Base Color"].default_value = (lin[0], lin[1], lin[2], 1.0)
        bsdf.inputs["Roughness"].default_value = 1.0 - float(p.get("smoothness", 0.1))
        bsdf.inputs["Metallic"].default_value = float(p.get("metallic", 0.0))
        _set_input(bsdf, ["Specular IOR Level", "Specular"], 0.25)
        em = float(p.get("emission", 0.0))
        if em > 0:
            _set_input(bsdf, ["Emission Color", "Emission"], (lin[0], lin[1], lin[2], 1.0))
            _set_input(bsdf, ["Emission Strength"], em)
    return mat


# ---------------------------------------------------------------------------
# Mesh builder
# ---------------------------------------------------------------------------
class MB:
    """Accumulates geometry (with per-face material names) into one bmesh."""

    def __init__(self):
        self.bm = bmesh.new()
        self.mats = []

    # -- material slots ----------------------------------------------------
    def mi(self, name):
        if name not in self.mats:
            self.mats.append(name)
        return self.mats.index(name)

    def _assign(self, verts, mat):
        idx = self.mi(mat)
        faces = set()
        for v in verts:
            faces.update(v.link_faces)
        for f in faces:
            f.material_index = idx
        return list(faces)

    # -- primitives (Blender-space matrices) ---------------------------------
    def box_m(self, M, mat):
        r = bmesh.ops.create_cube(self.bm, size=1.0, matrix=M)
        return self._assign(r["verts"], mat)

    def box(self, center, size, mat, yaw=0.0):
        """Axis-aligned (optionally yawed) box. center/size in game space."""
        M = Matrix.Translation(V(*center)) @ yaw_matrix(yaw) @ Matrix.Diagonal((*SZ(*size), 1.0))
        return self.box_m(M, mat)

    def frustum(self, c0, c1, w0, d0, w1, d1, mat):
        """Tapered box from bottom centre c0 (w0 x d0) to top centre c1 (w1 x d1),
        game space, axis aligned. Call to_object(recalc=True) to fix winding."""
        x0, y0, z0 = c0
        x1, y1, z1 = c1
        b = [(x0 - w0 / 2, y0, z0 - d0 / 2), (x0 + w0 / 2, y0, z0 - d0 / 2),
             (x0 + w0 / 2, y0, z0 + d0 / 2), (x0 - w0 / 2, y0, z0 + d0 / 2)]
        t = [(x1 - w1 / 2, y1, z1 - d1 / 2), (x1 + w1 / 2, y1, z1 - d1 / 2),
             (x1 + w1 / 2, y1, z1 + d1 / 2), (x1 - w1 / 2, y1, z1 + d1 / 2)]
        vb = [self.bm.verts.new(V(*p)) for p in b]
        vt = [self.bm.verts.new(V(*p)) for p in t]
        idx = self.mi(mat)
        faces = [vb[::-1], vt, (vb[0], vb[1], vt[1], vt[0]), (vb[1], vb[2], vt[2], vt[1]),
                 (vb[2], vb[3], vt[3], vt[2]), (vb[3], vb[0], vt[0], vt[3])]
        out = []
        for fv in faces:
            f = self.bm.faces.new(fv)
            f.material_index = idx
            out.append(f)
        return out

    def box_between(self, p0, p1, w, h, mat, up=(0, 1, 0)):
        """Box (beam) from game-space point p0 to p1 with cross-section w x h."""
        a, b = V(*p0), V(*p1)
        d = b - a
        length = d.length
        if length < 1e-6:
            return []
        z = d.normalized()
        upv = V(*up).normalized()
        x = upv.cross(z)
        if x.length < 1e-4:
            x = Vector((1, 0, 0)).cross(z)
            if x.length < 1e-4:
                x = Vector((0, 1, 0)).cross(z)
        x.normalize()
        y = z.cross(x).normalized()
        R = Matrix((x, y, z)).transposed().to_4x4()
        M = Matrix.Translation((a + b) * 0.5) @ R @ Matrix.Diagonal((w, h, length, 1.0))
        return self.box_m(M, mat)

    def cyl_between(self, p0, p1, r0, r1, mat, segs=8, caps=True):
        """Cylinder/cone from game-space point p0 (radius r0) to p1 (radius r1)."""
        a, b = V(*p0), V(*p1)
        d = b - a
        length = d.length
        if length < 1e-6:
            return []
        q = Vector((0, 0, 1)).rotation_difference(d.normalized())
        M = Matrix.Translation((a + b) * 0.5) @ q.to_matrix().to_4x4()
        r = bmesh.ops.create_cone(self.bm, cap_ends=caps, cap_tris=False, segments=segs,
                                  radius1=r0, radius2=r1, depth=length, matrix=M)
        return self._assign(r["verts"], mat)

    def cyl(self, base, h, r0, r1=None, mat="Wood", segs=8, yaw=0.0):
        """Vertical cylinder standing on game-space point 'base'."""
        if r1 is None:
            r1 = r0
        M = Matrix.Translation(V(*base) + Vector((0, 0, h * 0.5))) @ yaw_matrix(yaw)
        r = bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=segs,
                                  radius1=r0, radius2=r1, depth=h, matrix=M)
        return self._assign(r["verts"], mat)

    def sphere(self, center, radius, mat, scale=(1, 1, 1), segs=8, rings=6, ico=0, jitter=0.0, seed=0):
        """UV sphere (or icosphere if ico>0) at game-space center with game-space scale."""
        M = Matrix.Translation(V(*center)) @ Matrix.Diagonal((*SZ(*scale), 1.0))
        if ico > 0:
            r = bmesh.ops.create_icosphere(self.bm, subdivisions=ico, radius=radius, matrix=M)
        else:
            r = bmesh.ops.create_uvsphere(self.bm, u_segments=segs, v_segments=rings, radius=radius, matrix=M)
        verts = r["verts"]
        if jitter > 0:
            rng = random.Random(seed)
            c = V(*center)
            for v in verts:
                n = (v.co - c)
                if n.length > 1e-6:
                    n.normalize()
                v.co += n * (rng.uniform(-1, 1) * jitter)
        return self._assign(verts, mat)

    def rock(self, center, radius, mat="Rock", scale=(1, 0.7, 1), seed=0, rough=0.28, ico=1):
        M = Matrix.Translation(V(*center)) @ Matrix.Diagonal((*SZ(*scale), 1.0))
        r = bmesh.ops.create_icosphere(self.bm, subdivisions=ico, radius=radius, matrix=M)
        c = V(*center)
        off = Vector((seed * 7.31, seed * 3.17, seed * 1.93))
        for v in r["verts"]:
            n = v.co - c
            if n.length < 1e-6:
                continue
            k = noise.noise(v.co * (1.3 / max(radius, 0.1)) + off)
            v.co = c + n * (1.0 + rough * k)
        return self._assign(r["verts"], mat)

    # -- custom geometry ------------------------------------------------------
    def poly(self, pts, mat, double_sided=False, thickness=0.0):
        """Polygon from game-space points (winding: counter-clockwise when seen from
        the side it should face).  double_sided adds a reversed copy offset by
        'thickness' along -normal so it is visible from both sides in Unity."""
        verts = [self.bm.verts.new(V(*p)) for p in pts]
        f = self.bm.faces.new(verts)
        f.material_index = self.mi(mat)
        faces = [f]
        if double_sided:
            f.normal_update()
            off = -f.normal * thickness
            verts2 = [self.bm.verts.new(v.co + off) for v in reversed(verts)]
            f2 = self.bm.faces.new(verts2)
            f2.material_index = self.mi(mat)
            faces.append(f2)
        return faces

    def grid_surface(self, rows, mat, double_sided=True, thickness=0.03):
        """rows: list of rows, each a list of game-space points (same count).
        Builds a quad surface; double_sided creates the back layer offset along
        -normal so both sides render with back-face culling."""
        nr, nc = len(rows), len(rows[0])
        vv = [[self.bm.verts.new(V(*p)) for p in row] for row in rows]
        idx = self.mi(mat)
        front = []
        for i in range(nr - 1):
            for j in range(nc - 1):
                f = self.bm.faces.new((vv[i][j], vv[i][j + 1], vv[i + 1][j + 1], vv[i + 1][j]))
                f.material_index = idx
                front.append(f)
        if double_sided:
            # vertex normals of the front layer
            normals = {}
            for f in front:
                f.normal_update()
                for v in f.verts:
                    normals.setdefault(v, Vector((0, 0, 0)))
                    normals[v] += f.normal
            bb = [[self.bm.verts.new(v.co - normals[v].normalized() * thickness) for v in row] for row in vv]
            for i in range(nr - 1):
                for j in range(nc - 1):
                    f = self.bm.faces.new((bb[i][j], bb[i + 1][j], bb[i + 1][j + 1], bb[i][j + 1]))
                    f.material_index = idx
        return front

    def loft(self, rings, mat_fn, closed=True, cap_start=None, cap_end=None):
        """Skin consecutive rings (lists of game-space points, equal length).
        mat_fn(ring_index, point_index) -> material name for the quad.
        cap_start/cap_end: material name to close the first/last ring."""
        vr = [[self.bm.verts.new(V(*p)) for p in ring] for ring in rings]
        n = len(rings[0])
        last = n if closed else n - 1
        for i in range(len(vr) - 1):
            for j in range(last):
                j2 = (j + 1) % n
                f = self.bm.faces.new((vr[i][j], vr[i][j2], vr[i + 1][j2], vr[i + 1][j]))
                f.material_index = self.mi(mat_fn(i, j))
        if cap_start:
            f = self.bm.faces.new(list(reversed(vr[0])))
            f.material_index = self.mi(cap_start)
        if cap_end:
            f = self.bm.faces.new(vr[-1])
            f.material_index = self.mi(cap_end)
        return vr

    def recalc_normals(self):
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces[:])

    # -- finalize ---------------------------------------------------------------
    def to_object(self, name, origin=(0, 0, 0), recalc=False, parent=None):
        """Create a Blender object. 'origin' (game space) becomes the object pivot."""
        o = V(*origin)
        if recalc:
            self.recalc_normals()
        bmesh.ops.remove_doubles(self.bm, verts=self.bm.verts[:], dist=1e-5)
        bmesh.ops.translate(self.bm, verts=self.bm.verts[:], vec=-o)
        me = bpy.data.meshes.new(name)
        self.bm.to_mesh(me)
        self.bm.free()
        for mname in self.mats:
            me.materials.append(get_material(mname))
        for p in me.polygons:
            p.use_smooth = False
        ob = bpy.data.objects.new(name, me)
        ob.location = o
        bpy.context.scene.collection.objects.link(ob)
        if parent is not None:
            set_parent(ob, parent)
        return ob


# ---------------------------------------------------------------------------
# Objects / hierarchy
# ---------------------------------------------------------------------------

def empty(name, pos=(0, 0, 0), parent=None, size=0.5):
    ob = bpy.data.objects.new(name, None)
    ob.empty_display_type = "PLAIN_AXES"
    ob.empty_display_size = size
    ob.location = V(*pos)
    bpy.context.scene.collection.objects.link(ob)
    if parent is not None:
        set_parent(ob, parent)
    return ob


def set_parent(child, parent):
    """Parent while keeping the child's world transform."""
    bpy.context.view_layer.update()
    mw = child.matrix_world.copy()
    child.parent = parent
    child.matrix_parent_inverse = parent.matrix_world.inverted()
    bpy.context.view_layer.update()
    child.matrix_world = mw


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0


def all_children(ob):
    out = []
    for c in ob.children:
        out.append(c)
        out.extend(all_children(c))
    return out


# ---------------------------------------------------------------------------
# Save / export / render
# ---------------------------------------------------------------------------

def save_blend(name):
    os.makedirs(BLEND_DIR, exist_ok=True)
    path = os.path.join(BLEND_DIR, name + ".blend")
    bpy.ops.wm.save_as_mainfile(filepath=path, compress=True, check_existing=False)
    return path


def export_fbx(name):
    """Export the whole scene (meshes + empties) as <FBX_DIR>/<name>.fbx with
    Unity-friendly settings: 1 unit = 1 m, Y-up, transforms baked."""
    os.makedirs(FBX_DIR, exist_ok=True)
    path = os.path.join(FBX_DIR, name + ".fbx")
    bpy.ops.object.select_all(action="DESELECT")
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=False,
        object_types={"MESH", "EMPTY"},
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=True,
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        use_triangles=True,
        use_tspace=False,
        use_custom_props=False,
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="STRIP",
        embed_textures=False,
    )
    return path


def scene_bounds(objs=None):
    """World-space bounding box (Blender coords) of all mesh objects."""
    bpy.context.view_layer.update()
    mn = Vector((1e9, 1e9, 1e9))
    mx = Vector((-1e9, -1e9, -1e9))
    for ob in (objs or bpy.context.scene.objects):
        if ob.type != "MESH":
            continue
        for c in ob.bound_box:
            w = ob.matrix_world @ Vector(c)
            mn = Vector((min(mn.x, w.x), min(mn.y, w.y), min(mn.z, w.z)))
            mx = Vector((max(mx.x, w.x), max(mx.y, w.y), max(mx.z, w.z)))
    return mn, mx


def render_preview(name, water=False, yaw=35.0, pitch=22.0, dist_mul=1.0, size=(640, 480),
                   samples=24, target=None, ortho=False):
    """Render a quick Cycles preview of the current scene into Blender/renders/<name>.png."""
    os.makedirs(RENDER_DIR, exist_ok=True)
    scene = bpy.context.scene
    mn, mx = scene_bounds()
    center = (mn + mx) * 0.5 if target is None else Vector(target)
    radius = max((mx - mn).length * 0.5, 0.5)

    if water:
        bm = bmesh.new()
        bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=radius * 4)
        me = bpy.data.meshes.new("PreviewWater")
        bm.to_mesh(me)
        bm.free()
        wmat = bpy.data.materials.new("PreviewWaterMat")
        wmat.use_nodes = True
        b = wmat.node_tree.nodes.get("Principled BSDF")
        b.inputs["Base Color"].default_value = (0.02, 0.25, 0.32, 1)
        b.inputs["Roughness"].default_value = 0.08
        _set_input(b, ["Transmission Weight", "Transmission"], 0.85)
        b.inputs["IOR"].default_value = 1.33
        me.materials.append(wmat)
        wo = bpy.data.objects.new("PreviewWater", me)
        scene.collection.objects.link(wo)

    cam_data = bpy.data.cameras.new("PreviewCam")
    cam_data.lens = 50
    cam_data.clip_end = 5000
    cam = bpy.data.objects.new("PreviewCam", cam_data)
    scene.collection.objects.link(cam)
    yr, pr = math.radians(yaw), math.radians(pitch)
    # view from front-right in game space (model faces -Y in Blender)
    dirv = Vector((-math.sin(yr) * math.cos(pr), -math.cos(yr) * math.cos(pr), math.sin(pr)))
    dist = radius / math.tan(math.radians(18)) * dist_mul
    if ortho:
        cam_data.type = "ORTHO"
        cam_data.ortho_scale = radius * 2.2 * dist_mul
    cam.location = center + dirv * dist
    cam.rotation_euler = (-dirv).to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam

    sun_data = bpy.data.lights.new("PreviewSun", "SUN")
    sun_data.energy = 3.5
    sun_data.angle = math.radians(8)
    sun = bpy.data.objects.new("PreviewSun", sun_data)
    sun.rotation_euler = (math.radians(50), math.radians(10), math.radians(-35))
    scene.collection.objects.link(sun)

    world = bpy.data.worlds.new("PreviewWorld")
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    bg.inputs["Color"].default_value = (0.45, 0.68, 0.95, 1)
    bg.inputs["Strength"].default_value = 0.9
    scene.world = world

    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = samples
    # Ubuntu's Blender is built without OpenImageDenoise, so denoise only if available
    scene.cycles.use_denoising = bool(getattr(bpy.app.build_options, "openimagedenoise", False))
    scene.render.resolution_x, scene.render.resolution_y = size
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = False
    scene.view_settings.view_transform = "Standard"
    path = os.path.join(RENDER_DIR, name + ".png")
    scene.render.filepath = path
    try:
        bpy.ops.render.render(write_still=True)
    except RuntimeError:
        scene.cycles.use_denoising = False
        bpy.ops.render.render(write_still=True)

    # remove preview helpers so they are never exported/saved
    for ob in [cam, sun] + ([wo] if water else []):
        bpy.data.objects.remove(ob, do_unlink=True)
    return path
