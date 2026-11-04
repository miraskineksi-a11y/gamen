"""Characters made of rigid parts that Unity animates procedurally.

All parts are DIRECT children of the root (Blender's FBX exporter with
"Apply Transform" breaks deeper hierarchies); the logical hierarchy is rebuilt
in Unity by Assets/Scripts/Characters/HumanoidRig.cs:
  Body   pivot = hips        (logical parent of Head, ArmL, ArmR)
  Head   pivot = neck
  ArmL / ArmR  pivot = shoulder (arm hangs down along -y)
  Sword  pivot = hand grip   (logical child of ArmR)
  HandR  empty at the grip point - tools attach here
  LegL / LegR  pivot = hip joint (leg hangs down along -y)
Crab contract (CrabRig.cs): Body, ClawL, ClawR, Leg_L1..3, Leg_R1..3 (pivot at body)
"""

import math

from .core import MB, empty


class Look:
    def __init__(self, **kw):
        self.skin = "Skin"
        self.torso = "CoatRed"          # main torso material
        self.stripes = None             # (mat_a, mat_b) striped shirt instead of torso
        self.shirt = "ShirtWhite"
        self.coat = True
        self.vest = None
        self.pants = "Pants"
        self.boots = "Boots"
        self.belt = "Leather"
        self.sleeve = None              # defaults to torso / shirt
        self.cuff = "PaintYellow"
        self.hat = "tricorn"            # tricorn | bandana | none
        self.hat_mat = "Hat"
        self.bandana = "Bandana"
        self.beard = "Hair"
        self.hair = "Hair"
        self.eyepatch = False
        self.peg_leg = False
        self.earring = False
        self.epaulettes = False
        self.belly = False
        self.blade = "Steel"
        self.scale = 1.0
        self.__dict__.update(kw)


def _limb_leg(root, side, o):
    mb = MB()
    hx = side * 0.13
    if o.peg_leg and side > 0:
        mb.frustum((hx, 0.55, 0), (hx, 0.95, 0), 0.17, 0.19, 0.2, 0.22, o.pants)
        mb.frustum((hx, 0.5, 0), (hx, 0.58, 0), 0.15, 0.16, 0.17, 0.18, o.pants)
        mb.cyl_between((hx, 0.52, 0), (hx, 0.0, 0.02), 0.06, 0.04, "WoodLight", segs=6)
    else:
        mb.frustum((hx, 0.42, 0), (hx, 0.95, 0), 0.17, 0.19, 0.21, 0.23, o.pants)
        mb.frustum((hx, 0.0, 0.0), (hx, 0.46, 0.0), 0.2, 0.24, 0.19, 0.22, o.boots)
        mb.frustum((hx, 0.4, 0.0), (hx, 0.52, 0.0), 0.23, 0.26, 0.24, 0.27, o.boots if o.boots != "Skin" else o.pants)
        mb.box((hx, 0.06, 0.11), (0.2, 0.12, 0.26), o.boots)
    return mb.to_object("LegR" if side > 0 else "LegL", origin=(hx, 0.94, 0), parent=root, recalc=True)


def _torso(body_parent, o):
    mb = MB()
    # hips / belt
    mb.frustum((0, 0.86, 0), (0, 1.03, 0), 0.4, 0.26, 0.42, 0.27, o.belt)
    mb.box((0, 0.945, 0.14), (0.09, 0.08, 0.02), "Gold")
    # torso
    if o.stripes:
        a, b = o.stripes
        ys = [1.0, 1.12, 1.24, 1.36, 1.46]
        for i in range(4):
            t0 = (ys[i] - 1.0) / 0.46
            t1 = (ys[i + 1] - 1.0) / 0.46
            w0, w1 = 0.42 + 0.1 * t0, 0.42 + 0.1 * t1
            d0, d1 = 0.27 + 0.03 * t0, 0.27 + 0.03 * t1
            mb.frustum((0, ys[i], 0), (0, ys[i + 1], 0), w0, d0, w1, d1, a if i % 2 == 0 else b)
    else:
        mb.frustum((0, 1.0, 0), (0, 1.46, 0), 0.42, 0.27, 0.52, 0.3, o.torso)
    if o.belly:
        mb.sphere((0, 1.1, 0.08), 0.22, o.torso if not o.stripes else o.stripes[0], scale=(1.0, 0.8, 0.8), segs=8, rings=5)
    if o.coat:
        mb.box((0, 1.25, 0.152), (0.16, 0.36, 0.02), o.shirt)
        mb.box((0, 1.38, 0.165), (0.09, 0.1, 0.03), o.shirt)
        mb.box((0, 0.74, -0.11), (0.44, 0.46, 0.08), o.torso)
        for sx in (-1, 1):
            mb.box((sx * 0.17, 0.76, 0.1), (0.11, 0.38, 0.07), o.torso)
            mb.box((sx * 0.1, 1.2, 0.153), (0.03, 0.42, 0.02), "PaintYellow")
    if o.vest:
        for sx in (-1, 1):
            mb.box((sx * 0.16, 1.24, 0.0), (0.16, 0.42, 0.31), o.vest)
    if o.epaulettes:
        for sx in (-1, 1):
            mb.box((sx * 0.27, 1.46, 0), (0.15, 0.05, 0.17), "Gold")
    mb.cyl((0, 1.44, 0), 0.09, 0.075, 0.07, o.skin, segs=6)
    return mb.to_object("Body", origin=(0, 0.94, 0), parent=body_parent, recalc=True)


def _head(root, o):
    mb = MB()
    mb.frustum((0, 1.5, 0), (0, 1.82, 0), 0.27, 0.28, 0.3, 0.31, o.skin)
    mb.box((0, 1.64, 0.165), (0.06, 0.08, 0.06), o.skin)
    for sx in (-1, 1):
        if o.eyepatch and sx > 0:
            mb.box((sx * 0.07, 1.69, 0.158), (0.08, 0.07, 0.02), "EyeBlack")
        else:
            mb.box((sx * 0.07, 1.69, 0.157), (0.045, 0.05, 0.02), "EyeBlack")
        mb.box((sx * 0.07, 1.735, 0.158), (0.08, 0.025, 0.02), o.hair)
        mb.box((sx * 0.148, 1.66, 0.0), (0.03, 0.08, 0.06), o.skin)          # ears
    if o.eyepatch:
        mb.frustum((0, 1.71, 0), (0, 1.735, 0), 0.305, 0.315, 0.305, 0.315, "EyeBlack")
    if o.earring:
        mb.sphere((-0.165, 1.6, 0.0), 0.025, "Gold", segs=6, rings=4)
    if o.beard:
        mb.frustum((0, 1.46, 0.09), (0, 1.62, 0.12), 0.24, 0.08, 0.28, 0.09, o.beard)
        mb.box((0, 1.615, 0.163), (0.17, 0.035, 0.03), o.beard)
    if o.hair and o.hat != "bald":
        mb.box((0, 1.66, -0.135), (0.3, 0.3, 0.07), o.hair)
    if o.hat == "tricorn":
        mb.cyl((0, 1.795, 0), 0.025, 0.33, 0.33, "Gold", segs=3, yaw=90)
        mb.cyl((0, 1.81, 0), 0.07, 0.32, 0.3, o.hat_mat, segs=3, yaw=90)
        mb.frustum((0, 1.82, 0), (0, 1.99, -0.01), 0.27, 0.28, 0.22, 0.23, o.hat_mat)
        mb.box_between((0.12, 1.9, -0.05), (0.32, 2.12, -0.2), 0.02, 0.08, "ShirtWhite")
    elif o.hat == "bandana":
        mb.frustum((0, 1.76, 0), (0, 1.86, 0), 0.315, 0.325, 0.29, 0.3, o.bandana)
        mb.box((0, 1.79, -0.17), (0.08, 0.08, 0.06), o.bandana)
        mb.box_between((0.02, 1.78, -0.18), (0.08, 1.62, -0.25), 0.07, 0.03, o.bandana)
    return mb.to_object("Head", origin=(0, 1.5, 0), parent=root, recalc=True)


def _arm(root, side, o):
    mb = MB()
    ax = side * 0.32
    sleeve = o.sleeve or (o.torso if o.coat else o.shirt)
    mb.frustum((ax, 1.0, 0), (ax, 1.46, 0), 0.14, 0.15, 0.16, 0.17, sleeve)
    mb.frustum((ax, 0.97, 0), (ax, 1.05, 0), 0.17, 0.18, 0.17, 0.18, o.cuff)
    mb.box((ax, 0.905, 0.01), (0.11, 0.13, 0.12), o.skin)
    arm = mb.to_object("ArmR" if side > 0 else "ArmL", origin=(ax, 1.4, 0), parent=root, recalc=True)
    return arm


def _sword(root, o, side=1):
    mb = MB()
    gx = side * 0.32
    gy, gz = 0.9, 0.02
    mb.cyl_between((gx, gy, gz - 0.07), (gx, gy, gz + 0.07), 0.025, 0.025, "Leather", segs=6)
    mb.box((gx, gy + 0.01, gz + 0.09), (0.04, 0.14, 0.035), "Gold")
    mb.box_between((gx, gy - 0.05, gz - 0.06), (gx, gy - 0.07, gz + 0.08), 0.02, 0.02, "Gold", up=(1, 0, 0))
    # slightly curved cutlass blade
    pts = [(gx, gy + 0.0, gz + 0.1), (gx, gy + 0.02, gz + 0.45), (gx, gy + 0.07, gz + 0.75), (gx, gy + 0.14, gz + 0.9)]
    widths = [0.06, 0.065, 0.055, 0.02]
    for i in range(3):
        mb.box_between(pts[i], pts[i + 1], 0.014, (widths[i] + widths[i + 1]) / 2, o.blade, up=(0, 1, 0))
    sword = mb.to_object("Sword", origin=(gx, gy, gz), parent=root, recalc=True)
    empty("HandR", (gx, gy, gz), parent=root, size=0.1)
    return sword


def build_humanoid(name, o):
    root = empty(name)
    _limb_leg(root, -1, o)
    _limb_leg(root, 1, o)
    _torso(root, o)
    _head(root, o)
    _arm(root, -1, o)
    _arm(root, 1, o)
    _sword(root, o)
    return root


# ---------------------------------------------------------------------------
# Skeleton warrior (same part contract as humanoids)
# ---------------------------------------------------------------------------

def build_skeleton(name="Skeleton"):
    root = empty(name)
    for side in (-1, 1):
        mb = MB()
        hx = side * 0.12
        mb.cyl_between((hx, 0.94, 0), (hx, 0.5, 0), 0.05, 0.045, "Bone", segs=6)
        mb.sphere((hx, 0.49, 0), 0.06, "Bone", segs=6, rings=4)
        mb.cyl_between((hx, 0.48, 0), (hx, 0.08, 0), 0.045, 0.04, "Bone", segs=6)
        mb.box((hx, 0.04, 0.07), (0.11, 0.07, 0.22), "Bone")
        mb.frustum((hx, 0.72, 0), (hx, 0.95, 0), 0.17, 0.19, 0.2, 0.22, "PantsDark")  # torn trousers
        mb.to_object("LegR" if side > 0 else "LegL", origin=(hx, 0.94, 0), parent=root, recalc=True)
    mb = MB()
    mb.frustum((0, 0.86, 0), (0, 1.0, 0), 0.32, 0.18, 0.34, 0.2, "Bone")      # pelvis
    mb.cyl_between((0, 0.98, -0.06), (0, 1.46, -0.06), 0.04, 0.04, "Bone", segs=6)  # spine
    for i, y in enumerate((1.12, 1.22, 1.32, 1.42)):
        w = 0.36 + 0.03 * i
        mb.box((0, y, 0.07), (w, 0.04, 0.03), "Bone")
        for sx in (-1, 1):
            mb.box((sx * w / 2, y, -0.0), (0.03, 0.04, 0.16), "Bone")
    mb.box((0, 1.47, -0.02), (0.5, 0.05, 0.08), "Bone")                         # collar bones
    mb.frustum((0, 0.98, 0), (0, 1.2, 0), 0.36, 0.25, 0.4, 0.27, "Rust")         # rusty belt/rags
    mb.cyl((0, 1.46, -0.04), 0.06, 0.035, 0.035, "Bone", segs=6)
    mb.to_object("Body", origin=(0, 0.94, 0), parent=root, recalc=True)
    mb = MB()
    mb.frustum((0, 1.56, 0), (0, 1.8, 0), 0.26, 0.27, 0.28, 0.3, "Bone")
    mb.frustum((0, 1.49, 0.03), (0, 1.57, 0.04), 0.2, 0.18, 0.24, 0.22, "Bone")  # jaw
    for sx in (-1, 1):
        mb.box((sx * 0.065, 1.68, 0.142), (0.085, 0.08, 0.02), "EyeBlack")
        mb.sphere((sx * 0.065, 1.68, 0.15), 0.025, "EyeGlow", segs=6, rings=4)
    mb.box((0, 1.6, 0.15), (0.05, 0.06, 0.02), "EyeBlack")
    for i in range(5):
        mb.box((-0.08 + i * 0.04, 1.555, 0.148), (0.025, 0.03, 0.01), "PaintWhite")
    mb.frustum((0, 1.77, 0), (0, 1.86, 0), 0.29, 0.31, 0.27, 0.29, "Bandana")
    mb.to_object("Head", origin=(0, 1.5, 0), parent=root, recalc=True)
    for side in (-1, 1):
        mb = MB()
        ax = side * 0.27
        mb.sphere((ax, 1.42, 0), 0.06, "Bone", segs=6, rings=4)
        mb.cyl_between((ax, 1.42, 0), (ax, 1.17, 0), 0.04, 0.035, "Bone", segs=6)
        mb.cyl_between((ax, 1.17, 0), (ax, 0.95, 0.02), 0.035, 0.03, "Bone", segs=6)
        mb.box((ax, 0.9, 0.02), (0.08, 0.1, 0.1), "Bone")
        mb.to_object("ArmR" if side > 0 else "ArmL", origin=(ax, 1.4, 0), parent=root, recalc=True)
        if side > 0:
            o = Look(blade="Rust")
            sw = MB()
            gx, gy, gz = ax, 0.9, 0.02
            sw.cyl_between((gx, gy, gz - 0.07), (gx, gy, gz + 0.07), 0.025, 0.025, "WoodDark", segs=6)
            sw.box((gx, gy, gz + 0.09), (0.04, 0.12, 0.035), "Iron")
            sw.box_between((gx, gy, gz + 0.1), (gx, gy + 0.08, gz + 0.85), 0.016, 0.06, "Rust")
            sw.to_object("Sword", origin=(gx, gy, gz), parent=root, recalc=True)
            empty("HandR", (gx, gy, gz), parent=root, size=0.1)
    return root


# ---------------------------------------------------------------------------
# Giant crab
# ---------------------------------------------------------------------------

def build_crab(name="Crab"):
    root = empty(name)
    mb = MB()
    by = 0.55
    mb.cyl((0, by - 0.18, 0), 0.22, 0.62, 0.66, "CrabBelly", segs=8)
    mb.sphere((0, by + 0.04, 0), 0.68, "CrabShell", scale=(1.0, 0.42, 0.78), segs=10, rings=6)
    for sx in (-1, 1):
        mb.cyl_between((sx * 0.16, by + 0.18, 0.4), (sx * 0.2, by + 0.48, 0.48), 0.04, 0.035, "CrabDark", segs=5)
        mb.sphere((sx * 0.2, by + 0.52, 0.49), 0.075, "EyeBlack", segs=6, rings=4)
        mb.box((sx * 0.3, by + 0.05, 0.5), (0.12, 0.06, 0.06), "CrabDark")
    # spikes on the shell
    for i in range(5):
        x = -0.4 + i * 0.2
        mb.cyl_between((x, by + 0.2, 0.42), (x * 1.1, by + 0.27, 0.6), 0.05, 0.0, "CrabDark", segs=4)
    mb.to_object("Body", origin=(0, by, 0), parent=root, recalc=True)
    for side in (-1, 1):
        sname = "R" if side > 0 else "L"
        c = MB()
        jx, jy, jz = side * 0.45, by, 0.42
        e = (side * 0.75, by + 0.12, 0.85)
        c.cyl_between((jx, jy, jz), e, 0.09, 0.08, "CrabShell", segs=6)
        c.sphere(e, 0.1, "CrabDark", segs=6, rings=4)
        p = (side * 0.8, by + 0.2, 1.05)
        c.sphere(p, 0.2, "CrabShell", scale=(0.8, 0.85, 1.25), segs=8, rings=5)
        c.cyl_between((side * 0.8, by + 0.28, 1.2), (side * 0.78, by + 0.3, 1.55), 0.09, 0.02, "CrabDark", segs=5)
        c.cyl_between((side * 0.8, by + 0.1, 1.2), (side * 0.8, by + 0.06, 1.45), 0.07, 0.02, "CrabDark", segs=5)
        c.to_object("Claw" + sname, origin=(jx, jy, jz), parent=root, recalc=True)
        for k, z in enumerate((0.25, 0.0, -0.25)):
            g = MB()
            jx, jy = side * 0.55, by - 0.05
            knee = (side * 1.0, by + 0.25, z * 1.3)
            foot = (side * 1.22, 0.0, z * 1.5)
            g.cyl_between((jx, jy, z), knee, 0.06, 0.05, "CrabShell", segs=5)
            g.sphere(knee, 0.06, "CrabDark", segs=5, rings=3)
            g.cyl_between(knee, foot, 0.05, 0.015, "CrabShell", segs=5)
            g.to_object("Leg_%s%d" % (sname, k + 1), origin=(jx, jy, z), parent=root, recalc=True)
    return root


MODELS = {
    "Captain": (lambda: build_humanoid("Captain", Look(
        torso="CoatBlue", coat=True, cuff="PaintYellow", hat="tricorn", epaulettes=True, beard="Hair",
        pants="Pants", boots="Boots")), dict(yaw=25, pitch=10)),
    "Pirate_A": (lambda: build_humanoid("Pirate_A", Look(
        stripes=("ShirtWhite", "ShirtRed"), coat=False, sleeve="ShirtWhite", cuff="ShirtWhite", hat="bandana",
        bandana="Bandana", skin="SkinDark", pants="Pants", boots="Boots", earring=True, beard="Hair")),
        dict(yaw=25, pitch=10)),
    "Pirate_B": (lambda: build_humanoid("Pirate_B", Look(
        torso="Skin", coat=False, vest="ShirtBlue", sleeve="Skin", cuff="Leather", hat="bandana",
        bandana="BandanaBlack", eyepatch=True, peg_leg=True, pants="PantsDark", boots="Boots", beard="Hair",
        belly=True)), dict(yaw=25, pitch=10)),
    "Skeleton": (lambda: build_skeleton("Skeleton"), dict(yaw=25, pitch=10)),
    "Crab": (lambda: build_crab("Crab"), dict(yaw=30, pitch=30)),
}
