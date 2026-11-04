"""
Validates exported FBX files for Unity:
  * 1 unit = 1 metre (UnitScaleFactor = 100)
  * exactly one root object, every other object a DIRECT child of it
    (Blender's "Apply Transform" option breaks deeper hierarchies)
  * no rotation / scale on nodes (transforms are baked)

Usage:  python3 Blender/check_fbx.py [Assets/Resources/Models]
Needs Blender's io_scene_fbx/fbx2json.py (found automatically on common paths).
"""
import glob
import json
import os
import shutil
import subprocess
import sys
import tempfile

CANDIDATES = glob.glob("/usr/share/blender/scripts/addons/io_scene_fbx/fbx2json.py") + \
    glob.glob("/usr/share/blender/*/scripts/addons*/io_scene_fbx/fbx2json.py") + \
    glob.glob("C:/Program Files/Blender Foundation/Blender*/*/scripts/addons*/io_scene_fbx/fbx2json.py") + \
    glob.glob("/Applications/Blender.app/Contents/Resources/*/scripts/addons*/io_scene_fbx/fbx2json.py")


def check(folder):
    if not CANDIDATES:
        print("fbx2json.py not found - install Blender")
        return 1
    tool = CANDIDATES[0]
    tmp = tempfile.mkdtemp()
    bad = 0
    for fbx in sorted(glob.glob(os.path.join(folder, "*.fbx"))):
        out = os.path.join(tmp, os.path.basename(fbx))
        shutil.copy(fbx, out)
        subprocess.run([sys.executable, tool, out], capture_output=True)
        d = json.load(open(out[:-4] + ".json"))
        models, parent, xform, unit = {}, {}, {}, None
        for top in d:
            if top[0] == "GlobalSettings":
                for p in top[3]:
                    if p[0] == "Properties70":
                        for q in p[3]:
                            if q[1][0] == "UnitScaleFactor":
                                unit = q[1][-1]
            if top[0] == "Objects":
                for c in top[3]:
                    if c[0] != "Model":
                        continue
                    mid = c[1][0]
                    models[mid] = c[1][1].split("\x00")[0].replace("::Model", "")
                    for p in c[3]:
                        if p[0] != "Properties70":
                            continue
                        for q in p[3]:
                            name, v = q[1][0], q[1][-3:]
                            if name == "Lcl Rotation" and any(abs(x) > 0.01 for x in v):
                                xform[mid] = (name, v)
                            if name == "Lcl Scaling" and any(abs(x - 1) > 0.001 for x in v):
                                xform[mid] = (name, v)
            if top[0] == "Connections":
                for c in top[3]:
                    if c[1][0] == "OO":
                        parent[c[1][1]] = c[1][2]

        def depth(m):
            k = 0
            while parent.get(m, 0) in models:
                m = parent[m]
                k += 1
            return k

        deep = [models[m] for m in models if depth(m) > 1]
        roots = [models[m] for m in models if depth(m) == 0]
        ok = not deep and not xform and unit == 100.0 and len(roots) == 1
        bad += 0 if ok else 1
        print("%s %-20s objects=%3d unit=%s%s%s" % ("OK " if ok else "BAD", os.path.basename(fbx)[:-4], len(models), unit,
                                                 (" nested=%s" % deep[:5]) if deep else "",
                                                 (" transforms=%s" % [models[k] for k in xform][:5]) if xform else ""))
    shutil.rmtree(tmp, ignore_errors=True)
    print("problems:", bad)
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(check(sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), "..", "Assets", "Resources", "Models")))
