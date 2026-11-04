"""
Витринные рендеры (Cycles) из тех же генераторов моделей, что и для игры.
    blender -b --factory-startup -P Blender/render_showcase.py [-- --samples 64]
Картинки: Blender/renders/showcase_*.jpg
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

from caribbean_models import characters, core, islands, monsters, ships  # noqa: E402
from caribbean_models.core import V  # noqa: E402


def place(root, pos, yaw=0.0, scale=1.0):
    """Переместить модель (корень + дети) в игровую точку pos, повернуть по Y (как в Unity)."""
    root.location = V(*pos)
    root.rotation_euler = (0.0, 0.0, math.radians(-yaw))
    root.scale = (scale, scale, scale)


def water_and_sky(sun_elev=32.0, sun_rot=210.0):
    scene = bpy.context.scene
    bpy.ops.mesh.primitive_plane_add(size=6000, location=(0, 0, 0))
    w = bpy.context.active_object
    w.name = "ShowWater"
    m = bpy.data.materials.new("ShowWaterMat")
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (0.02, 0.32, 0.36, 1)
    b.inputs["Roughness"].default_value = 0.12
    for n in ("Transmission Weight", "Transmission"):
        if n in b.inputs:
            b.inputs[n].default_value = 0.9
    b.inputs["IOR"].default_value = 1.33
    w.data.materials.append(m)
    bpy.ops.mesh.primitive_plane_add(size=6000, location=(0, 0, -26))
    f = bpy.context.active_object
    fm = bpy.data.materials.new("ShowFloor")
    fm.use_nodes = True
    fm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.01, 0.06, 0.1, 1)
    f.data.materials.append(fm)

    world = bpy.data.worlds.new("ShowWorld")
    world.use_nodes = True
    nt = world.node_tree
    bg = nt.nodes["Background"]
    sky = nt.nodes.new("ShaderNodeTexSky")
    try:
        sky.sky_type = "NISHITA"
        sky.sun_elevation = math.radians(sun_elev)
        sky.sun_rotation = math.radians(sun_rot)
        sky.altitude = 100
        sky.air_density = 1.2
        sky.dust_density = 0.6
        nt.links.new(sky.outputs["Color"], bg.inputs["Color"])
        bg.inputs["Strength"].default_value = 0.14
    except Exception:
        bg.inputs["Color"].default_value = (0.45, 0.68, 0.95, 1)
    scene.world = world
    sun_data = bpy.data.lights.new("ShowSun", "SUN")
    sun_data.energy = 2.4
    sun_data.angle = math.radians(1.5)
    sun_data.color = (1.0, 0.95, 0.86)
    sun = bpy.data.objects.new("ShowSun", sun_data)
    sun.rotation_euler = (math.radians(90 - sun_elev), 0, math.radians(sun_rot - 90))
    scene.collection.objects.link(sun)


def camera(pos, look, lens=35):
    scene = bpy.context.scene
    cd = bpy.data.cameras.new("ShowCam")
    cd.lens = lens
    cd.clip_end = 6000
    cam = bpy.data.objects.new("ShowCam", cd)
    scene.collection.objects.link(cam)
    p, t = V(*pos), V(*look)
    cam.location = p
    cam.rotation_euler = (t - p).to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam


def render(name, samples, size=(1280, 720), exposure=0.0):
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = samples
    scene.cycles.use_denoising = bool(getattr(bpy.app.build_options, "openimagedenoise", False))
    scene.cycles.max_bounces = 6
    scene.cycles.transmission_bounces = 4
    scene.render.resolution_x, scene.render.resolution_y = size
    scene.view_settings.view_transform = "Filmic" if "Filmic" in [i.identifier for i in scene.view_settings.bl_rna.properties["view_transform"].enum_items] else "Standard"
    scene.view_settings.look = "None"
    scene.view_settings.exposure = exposure
    path = os.path.join(core.RENDER_DIR, "showcase_" + name + ".jpg")
    scene.render.image_settings.file_format = "JPEG"
    scene.render.image_settings.quality = 90
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("RENDERED", path)


def tentacle(kraken_root, base, yaw, bends, scale=1.0):
    """Цепочка сегментов щупальца (как в Kraken.cs) из мешей модели Kraken."""
    seg = bpy.data.objects["TentacleSeg"]
    tip = bpy.data.objects["TentacleTip"]
    M = Matrix.Translation(V(*base)) @ Matrix.Rotation(math.radians(-yaw), 4, "Z")
    n = len(bends)
    for i, b in enumerate(bends):
        s = (1.05 + (0.55 - 1.05) * i / (n - 1)) * scale
        M = M @ Matrix.Rotation(math.radians(b), 4, "X")  # +X: верх сегмента наклоняется вперёд (к -Y Blender = +Z игры)
        o = seg.copy()
        o.matrix_world = M @ Matrix.Diagonal((s, s, s, 1.0))
        bpy.context.scene.collection.objects.link(o)
        M = M @ Matrix.Translation((0, 0, 1.8 * s))
    o = tip.copy()
    o.matrix_world = M @ Matrix.Diagonal((0.55 * scale,) * 3 + (1.0,))
    bpy.context.scene.collection.objects.link(o)


def scene_harbor(samples):
    core.reset_scene()
    core.load_palette()
    isl = islands.build_parrot()
    sh = ships.build_ship("Ship_Player", L=14.0, B=4.6, masts=1, sail_mat="Sail", stripe="PaintBlue", guns=5,
                          flag_mat="PaintBlue", skull=False, seed=3)
    place(sh, (60, 0.1, -95), yaw=95)
    pb = ships.build_ship("Ship_PirateBrig", L=19.0, B=5.8, F=1.7, D=1.9, masts=2, sail_mat="SailBlack", stripe="PaintRed",
                          stripe_sail="SailRed", guns=4, flag_mat="PaintBlack", skull=True, mast_h=15.0, seed=7)
    place(pb, (92, 0.1, -62), yaw=250)
    water_and_sky(sun_elev=28, sun_rot=230)
    camera((105, 9, -150), (45, 6, -40), lens=30)
    render("harbor", samples, exposure=-0.5)


def scene_crew(samples):
    core.reset_scene()
    core.load_palette()
    names = ["Pirate_A", "Captain", "Skeleton", "Pirate_B", "Crab"]
    xs = [-2.6, -0.9, 0.9, 2.6, 4.9]
    for n, x in zip(names, xs):
        builder = characters.MODELS[n][0]
        root = builder()
        place(root, (x, 0, 0), yaw=180 + (x * 4))
    bpy.ops.mesh.primitive_plane_add(size=80, location=(0, 0, 0))
    g = bpy.context.active_object
    g.data.materials.append(core.get_material("SandWet"))
    # пара пальм и сундук для настроения
    from caribbean_models import props
    mb = core.MB()
    props.palm(mb, (-6.5, 0, -4), h=7, lean=(0.2, 0.1), seed=3)
    props.palm(mb, (8, 0, -5), h=6, lean=(-0.25, 0.05), seed=8)
    mb.to_object("Palms")
    props.build_chest("ShowChest")
    chest = bpy.data.objects["ShowChest"]
    place(chest, (1.0, 0, -1.4), yaw=170)
    water_and_sky(sun_elev=35, sun_rot=160)
    bpy.data.objects["ShowWater"].location.z = -0.6
    camera((0.8, 1.5, -8.2), (0.8, 1.05, 0), lens=30)
    render("crew", samples, exposure=-0.9)


def scene_monsters(samples):
    core.reset_scene()
    core.load_palette()
    kr = monsters.build_kraken()
    head = bpy.data.objects["Head"]
    head.parent = None
    head.location = V(0, -0.5, 30)
    head.rotation_euler = (0, 0, math.radians(180))
    sh = ships.build_ship("Ship_Player", L=14.0, B=4.6, masts=1, sail_mat="Sail", stripe="PaintBlue", guns=5,
                          flag_mat="PaintBlue", skull=False, seed=3)
    place(sh, (0, 0.1, 6), yaw=75)
    tentacle(kr, (-12, -1.5, 9), 120, [6, 8, 10, 12, 14, 16, 18, 20], 1.0)
    tentacle(kr, (13, -1.5, 14), -110, [-8, -10, -12, -12, -10, -6, 4, 12], 1.0)
    tentacle(kr, (9, -1.5, 0), -60, [10, 14, 16, 18, 16, 14, 12, 10], 0.9)
    tentacle(kr, (-8, -1.5, 22), 160, [4, 6, 8, 10, 12, 10, 8, 6], 1.1)
    for name in ("TentacleSeg", "TentacleTip"):
        bpy.data.objects[name].hide_render = True
    water_and_sky(sun_elev=14, sun_rot=250)
    camera((2, 6, -26), (0, 6, 12), lens=28)
    render("kraken", samples, exposure=-0.3)


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    samples = int(argv[argv.index("--samples") + 1]) if "--samples" in argv else 48
    only = argv[argv.index("--only") + 1].split(",") if "--only" in argv else ["harbor", "crew", "kraken"]
    if "harbor" in only:
        scene_harbor(samples)
    if "crew" in only:
        scene_crew(samples)
    if "kraken" in only:
        scene_monsters(samples)
