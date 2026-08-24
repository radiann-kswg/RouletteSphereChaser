"""ParkAssembly.blend が Unity の ParkScene_v2 と等価かを検証する（DESIGN-v2 フェーズ8）。

ParkAssembly.blend を開いた状態で実行する:
    exec(open(r"...\\Docs\\verify_park_assembly.py").read())

検査内容
 1. **配置の等価性**: 全インスタンスについて、Blender側のワールドAABB（ローカルAABBの8隅を
    ワールドへ送って再AABB化＝Unity の Renderer.bounds と同じ算法）を規約 G で Unity 座標へ写し、
    park_layout.json の worldCenter/worldExtents と比較する。
    許容 1e-4 m（ダンプが float32 なので 1e-4 未満には詰められない）。
 2. **マーカー原点の一致**: park_markers.json の各トリガー/回転体の原点と突き合わせる。
 3. **AABB重なりの棚卸し**: 干渉の目安。件数そのものより「移行前後で増えていないか」を見る。
    ブートストラップ直後の基準値は AGENTS.md / DESIGN-v2.md に記録してある。
"""

import bpy
import json
import os
from mathutils import Vector

if "PARK_REPO" in globals():
    REPO = globals()["PARK_REPO"]
else:
    REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DOCS = os.path.join(REPO, "Docs")

TOL = 1e-4


def to_unity(v):
    """Blender ワールド -> Unity ワールド（規約 G: unity = (bx, bz, by)）"""
    return (v[0], v[2], v[1])


def world_aabb(ob):
    pts = [to_unity(ob.matrix_world @ Vector(c)) for c in ob.bound_box]
    mn = [min(p[i] for p in pts) for i in range(3)]
    mx = [max(p[i] for p in pts) for i in range(3)]
    return mn, mx


def main():
    layout = json.load(open(os.path.join(DOCS, "park_layout.json"), encoding="utf-8"))
    markers = json.load(open(os.path.join(DOCS, "park_markers.json"), encoding="utf-8"))["markers"]

    meshes = {int(o.name.rsplit(".", 1)[1]): o
              for o in bpy.context.scene.objects if o.type == 'MESH' and "unity_path" in o}
    # 得点表示(LBL)とウェイポイントは別採番なので機能マーカーだけ拾う
    empties = {int(o.name.rsplit(".", 1)[1]): o
               for o in bpy.context.scene.objects
               if o.type == 'EMPTY' and "unity_path" in o and o.get("kind") in ("T", "ROT", "OSC", "LIFT", "LAP")}

    bad = []
    worst = 0.0
    for idx, inst in enumerate(layout["instances"]):
        ob = meshes.get(idx)
        if ob is None:
            bad.append((999.0, inst["path"], "missing"))
            continue
        mn, mx = world_aabb(ob)
        c = [(mn[i] + mx[i]) / 2 for i in range(3)]
        e = [(mx[i] - mn[i]) / 2 for i in range(3)]
        uc, ue = inst["worldCenter"], inst["worldExtents"]
        err = max(max(abs(c[i] - uc[i]) for i in range(3)), max(abs(e[i] - ue[i]) for i in range(3)))
        worst = max(worst, err)
        if err > TOL:
            bad.append((err, inst["path"], "bounds"))

    mworst = 0.0
    for idx, mk in enumerate(markers):
        ob = empties.get(idx)
        if ob is None:
            bad.append((999.0, mk["path"], "missing marker"))
            continue
        p = to_unity(ob.matrix_world.translation)
        up = (mk["m"][3], mk["m"][7], mk["m"][11])
        err = max(abs(p[i] - up[i]) for i in range(3))
        mworst = max(mworst, err)
        if err > TOL:
            bad.append((err, mk["path"], "marker"))

    labels = json.load(open(os.path.join(DOCS, "park_labels.json"), encoding="utf-8"))["labels"]
    lempties = {int(o.name.rsplit(".", 1)[1]): o
                for o in bpy.context.scene.objects if o.type == 'EMPTY' and o.get("kind") == "LBL"}
    lworst = 0.0
    for idx, lb in enumerate(labels):
        ob = lempties.get(idx)
        if ob is None:
            bad.append((999.0, lb["path"], "missing label"))
            continue
        p = to_unity(ob.matrix_world.translation)
        up = (lb["m"][3], lb["m"][7], lb["m"][11])
        err = max(abs(p[i] - up[i]) for i in range(3))
        lworst = max(lworst, err)
        if err > TOL:
            bad.append((err, lb["path"], "label"))

    # AABB重なりの棚卸し（当たり判定ではなく監視用の目安）
    boxes = []
    for idx, ob in meshes.items():
        mn, mx = world_aabb(ob)
        boxes.append((ob["unity_path"], mn, mx))
    overlaps = 0
    for i in range(len(boxes)):
        _, a0, a1 = boxes[i]
        for j in range(i + 1, len(boxes)):
            _, b0, b1 = boxes[j]
            if all(a0[k] < b1[k] and b0[k] < a1[k] for k in range(3)):
                overlaps += 1

    bad.sort(reverse=True)
    return {
        "instances": len(layout["instances"]),
        "markers": len(markers),
        "labels": len(labels),
        "maxInstanceErr": round(worst, 8),
        "maxMarkerErr": round(mworst, 8),
        "maxLabelErr": round(lworst, 8),
        "failures": [[round(b[0], 6), b[1], b[2]] for b in bad[:20]],
        "aabbOverlapPairs": overlaps,
        "ok": len(bad) == 0,
    }


result = main()
print(result)
