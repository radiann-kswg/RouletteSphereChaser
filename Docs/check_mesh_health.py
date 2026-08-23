"""メッシュ健全性チェック（AGENTS.md 罠40・罠45）。エクスポート前後に必ず通すこと。

判定するもの:
  - 縮退面（面積 < 1e-9）      … FBXにゼロ長法線として書き出され、Unityで反転に見える（罠45）
  - 極小面（面積 < 1e-6）      … 参考値。多くても直ちに害は無いが要注意
  - ゼロ長／NaN／非正規化の法線 … 1本でもあればエクスポート不可
  - 非多様体エッジ・符号付き体積 … ブーリアン＋ベベルの破綻検出（罠40）

使い方:
  BL="/Users/snine9801/Library/Application Support/Steam/steamapps/common/Blender/Blender.app/Contents/MacOS/Blender"

  # .blend の全メッシュを検査
  "$BL" --background BlenderSources/ParkBase.blend --python Docs/check_mesh_health.py -- blend

  # 書き出し済みFBXを検査
  "$BL" --background --python Docs/check_mesh_health.py -- fbx Assets/Models/LiftGuide.fbx

  # Assets/Models 以下を総なめ
  for f in Assets/Models/*.fbx; do
    "$BL" --background --python Docs/check_mesh_health.py -- fbx "$f" 2>/dev/null | grep -E "^  "
  done

終了コード: 問題ゼロなら 0、1つでも NG があれば 1。
"""
import math
import sys

import bmesh
import bpy
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else ["blend"]
mode = argv[0]

if mode == "fbx":
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=argv[1])

failed = False

for obj in bpy.data.objects:
    if obj.type != "MESH":
        continue
    me = obj.data

    degenerate = sum(1 for p in me.polygons if p.area < 1e-9)
    tiny = sum(1 for p in me.polygons if 1e-9 <= p.area < 1e-6)

    zero_n = nan_n = nonunit_n = 0
    for loop in me.loops:
        n = Vector(loop.normal)
        if any(math.isnan(c) for c in n):
            nan_n += 1
        elif n.length < 1e-6:
            zero_n += 1
        elif abs(n.length - 1.0) > 1e-3:
            nonunit_n += 1

    # 面法線とワインディングの向きが食い違う面（縮退面もここに現れる）
    mismatch = 0
    for poly in me.polygons:
        acc = Vector((0, 0, 0))
        for li in poly.loop_indices:
            acc += Vector(me.loops[li].normal)
        if acc.length > 1e-9 and acc.normalized().dot(poly.normal) < 0:
            mismatch += 1

    bm = bmesh.new()
    bm.from_mesh(me)
    bm.normal_update()
    nonmanifold = sum(1 for e in bm.edges if not e.is_manifold)
    volume = 0.0
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    for f in bm.faces:
        a, b, c = (v.co for v in f.verts)
        volume += a.dot(b.cross(c)) / 6.0
    bm.free()

    problems = []
    if degenerate:
        problems.append(f"縮退面{degenerate}")
    if zero_n:
        problems.append(f"ゼロ法線{zero_n}")
    if nan_n:
        problems.append(f"NaN法線{nan_n}")
    if nonunit_n:
        problems.append(f"非正規化法線{nonunit_n}")
    if nonmanifold:
        problems.append(f"非多様体{nonmanifold}")
    if volume <= 0:
        problems.append(f"体積{volume:+.6f}")

    verdict = "NG: " + " / ".join(problems) if problems else "OK"
    failed = failed or bool(problems)
    print(f"  {obj.name:28s} polys={len(me.polygons):5d} 縮退={degenerate:4d} "
          f"極小={tiny:4d} 法線不整合={mismatch:4d} 非多様体={nonmanifold:3d} "
          f"体積={volume:+.6f}  {verdict}")

sys.exit(1 if failed else 0)
