"""
Builds every 3D model of the game procedurally, saves an editable .blend for each
one (Blender/blend/) and exports an FBX for Unity (Assets/Resources/Models/).

Usage (from the project root):
    blender -b --factory-startup -P Blender/build_models.py
    blender -b --factory-startup -P Blender/build_models.py -- --only Ship_Player,Captain
    blender -b --factory-startup -P Blender/build_models.py -- --render        (also renders previews)
    blender -b --factory-startup -P Blender/build_models.py -- --list

After re-exporting, switch back to Unity: it re-imports the changed FBX files
automatically.
"""

import argparse
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from caribbean_models import core  # noqa: E402
from caribbean_models import ships  # noqa: E402

try:
    from caribbean_models import characters, monsters, props, islands  # noqa: E402
except ImportError as e:  # allows building ships while other modules are WIP
    print("WARNING: optional model module missing:", e)
    characters = monsters = props = islands = None


def _models():
    m = {}
    # name: (builder, preview options)
    m["Ship_Player"] = (lambda: ships.build_ship(
        "Ship_Player", L=14.0, B=4.6, masts=1, sail_mat="Sail", stripe="PaintBlue", guns=5,
        flag_mat="PaintBlue", skull=False, seed=3), dict(water=True, yaw=40, pitch=18))
    m["Ship_PirateSloop"] = (lambda: ships.build_ship(
        "Ship_PirateSloop", L=13.0, B=4.3, masts=1, sail_mat="SailBlack", stripe="PaintRed",
        stripe_sail="SailRed", guns=3, flag_mat="PaintBlack", skull=True, mast_h=11.5, seed=5),
        dict(water=True, yaw=40, pitch=18))
    m["Ship_PirateBrig"] = (lambda: ships.build_ship(
        "Ship_PirateBrig", L=19.0, B=5.8, F=1.7, D=1.9, masts=2, sail_mat="SailBlack", stripe="PaintRed",
        stripe_sail="SailRed", guns=4, flag_mat="PaintBlack", skull=True, mast_h=15.0, seed=7),
        dict(water=True, yaw=40, pitch=18))
    m["Rowboat"] = (lambda: ships.build_rowboat("Rowboat"), dict(water=True, yaw=35, pitch=30))
    if props:
        m.update(props.MODELS)
    if characters:
        m.update(characters.MODELS)
    if monsters:
        m.update(monsters.MODELS)
    if islands:
        m.update(islands.MODELS)
    return m


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", default="", help="comma separated model names")
    ap.add_argument("--render", action="store_true", help="render preview PNGs (Cycles, slow)")
    ap.add_argument("--render-only", action="store_true", help="render without exporting")
    ap.add_argument("--no-blend", action="store_true", help="do not save .blend files")
    ap.add_argument("--samples", type=int, default=24)
    ap.add_argument("--list", action="store_true")
    args = ap.parse_args(argv)

    models = _models()
    if args.list:
        print("\n".join(sorted(models)))
        return
    names = [n.strip() for n in args.only.split(",") if n.strip()] or list(models)
    t0 = time.time()
    for name in names:
        if name not in models:
            print("Unknown model:", name)
            continue
        builder, preview = models[name]
        core.reset_scene()
        core.load_palette()
        t = time.time()
        builder()
        if not args.render_only:
            if not args.no_blend:
                core.save_blend(name)
            core.export_fbx(name)
        if args.render or args.render_only:
            opts = dict(preview)
            opts.setdefault("samples", args.samples)
            core.render_preview(name, **opts)
        print("BUILT %-22s %.1fs" % (name, time.time() - t))
    print("DONE %d models in %.1fs" % (len(names), time.time() - t0))


if __name__ == "__main__":
    main()
