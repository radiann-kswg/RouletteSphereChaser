"""ParkAssembly.blend ブートストラップ生成（DESIGN-v2 フェーズ8 手順1）

Unity の ParkScene_v2 からダンプした配置（Docs/park_layout.json / park_markers.json）と、
実測で確定した Blender→Unity メッシュ変換（Docs/mesh_calib.json）を使って、
6つの原本 .blend から **メッシュをライブラリリンク**したまま現行配置どおりに並べた
BlenderSources/ParkAssembly.blend を生成する。

実行:
    Blender の Python コンソール / MCP から
        exec(open(r"...\\Docs\\build_park_assembly.py").read())
    ヘッドレスなら
        blender --background --factory-startup --python Docs/build_park_assembly.py

冪等: 実行するたびに空ファイルから作り直して同じ結果を書き出す。

--- 座標規約（2026-08-24 実測確定・38メッシュ全件で残差 <= 1e-6 m）---
1) FBX ごとの「Blender ローカル頂点 -> Unity メッシュローカル頂点」変換 C は2種類しかない:
     base 系（ParkBase.blend の4オブジェクトのみ）: C = s * diag( 1, 1, -1)
     mech 系（それ以外すべて）                    : C = s * diag(-1, 1,  1)
   s は FBX インポータのスケール係数（0.01 か 1.0）。値は mesh_calib.json に実測値がある。
2) 本アセンブリが採る**唯一の全体規約** G（Blender ワールド -> Unity ワールド）:
     unity = (bx, bz, by)      ... y と z の入れ替え。G は自己逆行列（G^-1 = G）。
   これは Blender 標準の「+Z上 / +Y前」を Unity の「+Y上 / +Z前」へ写す慣用変換で、
   鏡映（det = -1）を1回だけ含む。以後 per-part の軸規約（AGENTS 罠19）は不要になる。
3) よって各インスタンスの Blender 側行列は  T = G . M . C  （M = Unity の localToWorldMatrix）。
   det(T) > 0 なので Blender 側に負スケールのオブジェクトは出ない。
"""

import bpy
import json
import os
from mathutils import Matrix

# ---- パス ----
# exec() で読み込むときは __file__ が無いので、事前に globals()["PARK_REPO"] を置いてもよい
if "PARK_REPO" in globals():
    REPO = globals()["PARK_REPO"]
else:
    REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DOCS = os.path.join(REPO, "Docs")
SRC_DIR = os.path.join(REPO, "BlenderSources")
OUT_BLEND = os.path.join(SRC_DIR, "ParkAssembly.blend")

# 原本 .blend ごとの提供オブジェクト（Unity で実際に使っているものだけ）
SOURCES = {
    "ParkBase.blend": ["DrainStation", "DrainStirrer", "LiftGuide", "ParkBase"],
    "TowerA.blend": ["AgitatorA", "CollectorFunnel", "DistributorA", "HighLane", "MiniKuruun",
                     "MiniRouletteBowl", "MiniRouletteWheel", "RouletteBowl", "RouletteWheel",
                     "ScoreGate", "Spiral3"],
    "TowerBCH.blend": ["BPachiBoard", "BStepChucker", "CCatchTurn", "CSeesaw", "HDrum",
                       "HKarakoDish", "LiftTopRail", "TowerB_CatchChute", "TowerC_ZigzagShort",
                       "TowerH_MissPan"],
    "TowerDE.blend": ["TowerD_Kuruun", "TowerE_FeedPan", "TowerE_PocketDisc", "TowerE_StarGear"],
    "TowerFG.blend": ["FCatchTray", "JPSpinnerDish", "NumaKuruun_L", "NumaKuruun_M", "NumaKuruun_S",
                      "TowerF_JPTube", "TowerF_MissTray", "TowerG_FeedCatch", "TowerG_MergeTray"],
}

# Unity のメッシュ datablock 名 -> Blender のオブジェクト名（食い違う1件だけ）
MESH_NAME_OVERRIDE = {"CZigzagShort": "TowerC_ZigzagShort"}

# 全体規約 G: unity = (bx, bz, by)。自己逆行列
G = Matrix(((1, 0, 0, 0),
            (0, 0, 1, 0),
            (0, 1, 0, 0),
            (0, 0, 0, 1)))


def u_matrix(row12):
    """Unity の localToWorldMatrix 上位3行(12要素) -> 4x4"""
    r = row12
    return Matrix(((r[0], r[1], r[2], r[3]),
                   (r[4], r[5], r[6], r[7]),
                   (r[8], r[9], r[10], r[11]),
                   (0.0, 0.0, 0.0, 1.0)))


def get_collection(path_parts, cache):
    """'TowerA23/Chain_45' のような階層をコレクションとして掘る"""
    key = "/".join(path_parts)
    if key in cache:
        return cache[key]
    parent = get_collection(path_parts[:-1], cache) if len(path_parts) > 1 else bpy.context.scene.collection
    col = bpy.data.collections.new(path_parts[-1])
    parent.children.link(col)
    cache[key] = col
    return col


def main():
    layout = json.load(open(os.path.join(DOCS, "park_layout.json"), encoding="utf-8"))
    markers = json.load(open(os.path.join(DOCS, "park_markers.json"), encoding="utf-8"))["markers"]
    calib = json.load(open(os.path.join(DOCS, "mesh_calib.json"), encoding="utf-8"))
    mats = json.load(open(os.path.join(DOCS, "park_materials.json"), encoding="utf-8"))
    labels = json.load(open(os.path.join(DOCS, "park_labels.json"), encoding="utf-8"))["labels"]

    bpy.ops.wm.read_homefile(use_empty=True)
    bpy.context.scene.name = "ParkAssembly"

    # ---- 原本からメッシュをリンク（テンプレート用オブジェクトはシーンに入れない）----
    tmpl = {}
    for blend, names in SOURCES.items():
        path = os.path.join(SRC_DIR, blend)
        with bpy.data.libraries.load(path, link=True) as (src, dst):
            miss = [n for n in names if n not in src.objects]
            if miss:
                raise RuntimeError("%s に %s が無い" % (blend, miss))
            dst.objects = list(names)
        for n in names:
            tmpl[n] = bpy.data.objects[n]

    cache = {}
    placed = 0
    for idx, inst in enumerate(layout["instances"]):
        fbx = os.path.splitext(os.path.basename(inst["fbx"]))[0]
        cal = calib[fbx]
        obj_name = MESH_NAME_OVERRIDE.get(inst["mesh"], inst["mesh"])
        me = tmpl[obj_name].data

        sx, sy, sz = cal["sign"]
        s = cal["scale"]
        C = Matrix.Diagonal((sx * s, sy * s, sz * s, 1.0))
        T = G @ u_matrix(inst["m"]) @ C

        rel = inst["path"].split("/")[1:]          # 先頭の "Park" を落とす
        ob = bpy.data.objects.new("%s.%03d" % (rel[-1], idx), me)
        ob.matrix_world = T
        ob["unity_path"] = inst["path"]
        ob["fbx"] = inst["fbx"]
        ob["conv"] = cal["conv"]
        ob["collider"] = inst["collider"]
        mi = mats.get(inst["path"], {})
        ob["material"] = mi.get("mat", "")
        ob["material_rgb"] = mi.get("rgb", [0.5, 0.5, 0.5])
        get_collection(rel[:-1] if len(rel) > 1 else ["_root"], cache).objects.link(ob)
        placed += 1

    # ---- 機能マーカー（スコアトリガー・回転体・リフト等）は Empty で表現 ----
    # Empty の変換は Unity の localToWorldMatrix をそのまま写す（スケール＝トリガー箱の寸法）。
    # Blender の CUBE エンプティは半径 display_size なので 0.5 にすると Unity の 1辺1 キューブと一致する。
    mcol = bpy.data.collections.new("_Markers")
    bpy.context.scene.collection.children.link(mcol)
    mcache = {"": mcol}
    for idx, mk in enumerate(markers):
        rel = mk["path"].split("/")[1:]
        kind = ("T" if "scoreZone" in mk else
                "ROT" if "rotator" in mk else
                "OSC" if "oscillator" in mk else
                "LIFT" if "ballLift" in mk else
                "LAP" if "lapGate" in mk else "X")
        e = bpy.data.objects.new("%s_%s.%03d" % (kind, rel[-1], idx), None)
        e.empty_display_type = 'CUBE' if mk["isTrigger"] else 'PLAIN_AXES'
        e.empty_display_size = 0.5 if mk["isTrigger"] else 0.15
        e.matrix_world = G @ u_matrix(mk["m"])
        e["unity_path"] = mk["path"]
        e["kind"] = kind
        if "scoreZone" in mk:
            e["points"] = mk["scoreZone"]["points"]
            e["grantMultiplier"] = mk["scoreZone"]["grantMultiplier"]
        if "rotator" in mk:
            e["rot_axis"] = mk["rotator"]["axis"]
            e["rot_dps"] = mk["rotator"]["dps"]
        if "oscillator" in mk:
            for k, v in mk["oscillator"].items():
                e["osc_" + k] = v
        if "ballLift" in mk:
            e["lift_speed"] = mk["ballLift"]["speed"]
            e["lift_jitter"] = mk["ballLift"]["releaseJitter"]
        if "lapGate" in mk:
            e["lapGate"] = True
        sub = "/".join(rel[:-1])
        if sub not in mcache:
            c = bpy.data.collections.new("M_" + (sub.replace("/", "_") or "root"))
            mcol.children.link(c)
            mcache[sub] = c
        mcache[sub].objects.link(e)
        # リフトのウェイポイントも子 Empty に。これで搬送経路まで Blender 側が正になる
        if "ballLift" in mk:
            for wi, w in enumerate(mk["ballLift"]["waypoints"]):
                wp = bpy.data.objects.new("W%d_%s.%03d" % (wi, rel[-1], idx), None)
                wp.empty_display_type = 'SPHERE'
                wp.empty_display_size = 0.15
                wp.location = (w[0], w[2], w[1])   # unity -> blender（G は自己逆行列）
                wp["kind"] = "waypoint"
                mcache[sub].objects.link(wp)
                wp.parent = e
                wp.matrix_parent_inverse = e.matrix_world.inverted()

    # ---- 得点表示（TMP）も配置物なので Empty で持つ ----
    lcol = bpy.data.collections.new("_Labels")
    bpy.context.scene.collection.children.link(lcol)
    for idx, lb in enumerate(labels):
        e = bpy.data.objects.new("LBL_%s.%03d" % (lb["path"].split("/")[-1], idx), None)
        e.empty_display_type = 'SINGLE_ARROW'
        e.empty_display_size = 0.2
        e.matrix_world = G @ u_matrix(lb["m"])
        e["kind"] = "LBL"
        e["unity_path"] = lb["path"]
        e["text"] = lb["text"]
        e["fontSize"] = lb["fontSize"]
        e["color"] = lb["color"]
        e["billboard"] = bool(lb["billboard"])
        lcol.objects.link(e)

    bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND, relative_remap=True)
    return {"placed": placed, "markers": len(markers), "labels": len(labels), "out": OUT_BLEND}


result = main()
print(result)
