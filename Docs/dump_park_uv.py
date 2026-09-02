"""ParkAssembly.blend のメッシュUVをマテリアル別に Temp/park_uv_dump.json へ書き出す（gen_park_texture_psd.py の入力）。

ParkAssembly.blend を開いた状態で:
    exec(open(r"...\\Docs\\dump_park_uv.py").read())
出力: {マテリアル名: {メッシュデータ名: [[[u,v],...], ...]}}（マテリアルは params.json の割り当てに従う＝ParkBuilder と同じ対応）
"""
import bpy
import json
import os
from collections import defaultdict

REPO = globals().get("PARK_REPO") or os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
params = json.load(open(os.path.join(REPO, "Assets", "Models", "ParkAssembly.params.json"), encoding="utf-8"))
mat_of = {m["name"]: m["material"] for m in params["meshes"]}
dg = bpy.context.evaluated_depsgraph_get()
out = defaultdict(dict)
for ob in bpy.context.scene.objects:
    if ob.type != 'MESH' or ob.name not in mat_of or ob.data.name in out[mat_of[ob.name]]:
        continue
    ev = ob.evaluated_get(dg)
    me = ev.to_mesh()
    uv = me.uv_layers.active
    out[mat_of[ob.name]][ob.data.name] = [
        [[round(uv.data[li].uv.x, 4), round(uv.data[li].uv.y, 4)] for li in poly.loop_indices]
        for poly in me.polygons] if uv else []
    ev.to_mesh_clear()
os.makedirs(os.path.join(REPO, "Temp"), exist_ok=True)
json.dump(out, open(os.path.join(REPO, "Temp", "park_uv_dump.json"), "w"), separators=(",", ":"))
result = {m: len(d) for m, d in out.items()}
print(result)
