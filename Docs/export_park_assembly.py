"""ParkAssembly.blend -> Unity 用の単一FBX＋パラメータJSON（DESIGN-v2 フェーズ8-2）

出力:
    Assets/Models/ParkAssembly.fbx          … メッシュ123＋機能マーカーEmpty123＋リフトのウェイポイント
    Assets/Models/ParkAssembly.params.json  … オブジェクト名 -> 付けるコンポーネントとパラメータ

**変換の情報はJSONに一切入れない。** 位置・回転・スケールはすべてFBX側が持つ。
メッシュもマーカーも同じFBXを通るので、Unity側の軸変換が何であっても両者は必ず整合する
（＝規約Gの実現がズレても、直すのはParkBuilderの根1箇所だけで済む）。

実行: ParkAssembly.blend を開いた状態で
    exec(open(r"...\\Docs\\export_park_assembly.py").read())
"""

import bpy
import json
import os

if "PARK_REPO" in globals():
    REPO = globals()["PARK_REPO"]
else:
    REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

FBX = os.path.join(REPO, "Assets", "Models", "ParkAssembly.fbx")
PARAMS = os.path.join(REPO, "Assets", "Models", "ParkAssembly.params.json")


from mathutils import Matrix

# Blender ワールド -> Unity ワールドの規約 G（build_park_assembly.py と同一・自己逆行列）
G = Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))


def vec(v):
    return {"x": float(v[0]), "y": float(v[1]), "z": float(v[2])}


def unity_matrix(ob):
    """オブジェクトの姿勢を Unity の localToWorldMatrix 相当（上位3行=12要素）で返す。

    Unity の FBX インポータは **Empty(Null)ノードのスケールを負値×100 で表現する** ため、
    取り込んだ Empty の transform をそのまま読むと箱の寸法も回転も壊れる（2026-08-24実測）。
    メッシュ側は正しく入るので、マーカーの姿勢だけはここで数値として渡す。
    位置は Empty からも正しく読めるので、ParkBuilder 側で突き合わせてズレを検出している。
    """
    m = G @ ob.matrix_world
    return [m[0][0], m[0][1], m[0][2], m[0][3],
            m[1][0], m[1][1], m[1][2], m[1][3],
            m[2][0], m[2][1], m[2][2], m[2][3]]


def main():
    # Unity の JsonUtility は辞書を読めないので配列で出す（パーサを書かずに済ませる）
    scene = bpy.context.scene
    meshes, markers = [], []

    for ob in sorted(scene.objects, key=lambda o: o.name):
        if ob.type == 'MESH':
            c = list(ob.get("material_rgb", [0.5, 0.5, 0.5]))
            meshes.append({
                "name": ob.name,
                "path": ob.get("unity_path", ""),
                "collider": ob.get("collider", "mesh"),
                "material": ob.get("material", "") or ob.name.split(".")[0],
                "rgb": {"r": c[0], "g": c[1], "b": c[2], "a": 1.0},
            })
        elif ob.type == 'EMPTY' and ob.get("kind") not in (None, "waypoint"):
            kind = ob["kind"]
            m = {
                "name": ob.name, "path": ob.get("unity_path", ""), "kind": kind,
                "points": int(ob.get("points", 0)), "grantMultiplier": int(ob.get("grantMultiplier", 0)),
                "axis": vec(ob.get("rot_axis") or ob.get("osc_axis") or (0.0, 1.0, 0.0)),
                "dps": float(ob.get("rot_dps", 0.0)),
                "a": float(ob.get("osc_a", 0.0)), "b": float(ob.get("osc_b", 0.0)),
                "period": float(ob.get("osc_period", 0.0)), "phase": float(ob.get("osc_phase", 0.0)),
                "speed": float(ob.get("lift_speed", 0.0)), "releaseJitter": float(ob.get("lift_jitter", 0.0)),
                "waypoints": [vec((G @ c.matrix_world).translation)
                              for c in sorted(ob.children, key=lambda o: o.name)],
                "text": str(ob.get("text", "")), "fontSize": float(ob.get("fontSize", 1.5)),
                "billboard": bool(ob.get("billboard", False)),
                "m": unity_matrix(ob),
            }
            c = list(ob.get("color", [1.0, 1.0, 1.0]))
            m["rgb"] = {"r": c[0], "g": c[1], "b": c[2], "a": 1.0}
            markers.append(m)

    os.makedirs(os.path.dirname(FBX), exist_ok=True)
    with open(PARAMS, "w", encoding="utf-8") as f:
        json.dump({"meshes": meshes, "markers": markers}, f, indent=1, ensure_ascii=False)

    # use_selection は使わない: MCP 経由の制限コンテキストでは context.selected_objects が無く落ちる。
    # シーンには組み立て対象しか入っていないので全体書き出しでよい。
    bpy.ops.export_scene.fbx(
        filepath=FBX,
        use_selection=False,
        object_types={'MESH', 'EMPTY'},
        apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_NONE',
        use_mesh_modifiers=True,
        mesh_smooth_type='FACE',
        add_leaf_bones=False,
        bake_anim=False,
        path_mode='STRIP',
    )
    return {"fbx": FBX, "params": PARAMS, "meshes": len(meshes), "markers": len(markers)}


result = main()
print(result)
