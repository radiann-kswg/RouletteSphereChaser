"""Unityインポータに self-intersecting で捨てられるN-gonの修復（AGENTS.md 罠50）。

やることは2つだけ。**どちらも境界を1辺も動かさない**ので、形（符号付き体積・バウンズ）は保たれる。

  A. `remove_doubles(1e-6)` … 面ループ内に完全同一座標の頂点が2回出てくる不正面を潰す
     （DrainStirrer 実測: 不正面8→0・面数もΔvolも不変）。
  B. **N-gonを全部 poke**（重心へのファン分割）… Unityの三角化が破綻するのはN-gonだけなので、
     N-gonを無くせば警告は原理的に出ない。境界を保つので Δvol ≈ 0。

**三角化（bmesh.ops.triangulate）は使わない。** 罠50の通り、非平面N-gonは面が張り替わって形が変わり、
細いスリバーN-gonでは面積0の三角形を量産して罠49（ゼロ長法線＝Unityで反転表示）を作る。
実測比較（NumaKuruun_L）: poke Δvol −3.8e-05・縮退面0 ／ EAR_CLIP Δvol +5.0e-05・縮退面4。
LiftGuide（14mの1.9mm幅スリバー12枚）は poke で Δvol −3.9e-11・非多様体0→0。

使い方（GUIのBlenderを塞がずにヘッドレスで走らせる）:
    blender.exe --background <原本.blend> --python Docs/fix_selfint_faces.py -- [--save] [mesh名...]

mesh名を省略すると全メッシュが対象。`--save` を付けたときだけ .blend を上書きする。
検算NG（バウンズが動いた／縮退面が残った）のときは保存しない＝終了コード1。
"""
import sys

import bmesh
import bpy
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
save = "--save" in argv
names = [a for a in argv if not a.startswith("--")]


def signed_volume(bm):
    bm2 = bm.copy()
    bmesh.ops.triangulate(bm2, faces=bm2.faces[:])
    v = 0.0
    for f in bm2.faces:
        a, b, c = (x.co for x in f.verts)
        v += a.dot(b.cross(c)) / 6.0
    bm2.free()
    return v


def bounds(bm):
    lo = Vector((1e9, 1e9, 1e9))
    hi = -lo
    for v in bm.verts:
        for i in range(3):
            lo[i] = min(lo[i], v.co[i])
            hi[i] = max(hi[i], v.co[i])
    return lo, hi


def dup_vert_faces(bm):
    n = 0
    for f in bm.faces:
        co = [tuple(round(x, 6) for x in v.co) for v in f.verts]
        if len(set(co)) != len(co):
            n += 1
    return n


failed = False
for obj in bpy.data.objects:
    if obj.type != "MESH":
        continue
    if names and obj.data.name not in names and obj.name not in names:
        continue
    me = obj.data

    bm = bmesh.new()
    bm.from_mesh(me)
    bm.normal_update()
    v0, (lo0, hi0), f0 = signed_volume(bm), bounds(bm), len(bm.faces)
    nm0, dup0 = sum(1 for e in bm.edges if not e.is_manifold), dup_vert_faces(bm)

    if dup0:
        bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-6)
        bm.normal_update()

    ngons = [f for f in bm.faces if len(f.verts) > 4]
    if ngons:
        bmesh.ops.poke(bm, faces=ngons, center_mode="MEAN")
        bm.normal_update()

    v1, (lo1, hi1), f1 = signed_volume(bm), bounds(bm), len(bm.faces)
    nm1 = sum(1 for e in bm.edges if not e.is_manifold)
    degen = sum(1 for f in bm.faces if f.calc_area() < 1e-9)   # 罠49
    dbound = max(max(abs(lo1[i] - lo0[i]), abs(hi1[i] - hi0[i])) for i in range(3))

    if dup0 or ngons:
        bm.to_mesh(me)
        me.update()
    bm.free()

    print(f"  {me.name:24s} faces {f0}->{f1} 重複頂点面={dup0} poke={len(ngons)} "
          f"Δvol={v1 - v0:+.3e} Δbounds={dbound:.3e} 非多様体 {nm0}->{nm1} 縮退面={degen}")
    if dbound > 1e-6:
        print(f"    !! バウンズが動いた: {dbound}")
        failed = True
    if degen:
        print(f"    !! 縮退面が残っている（罠49）: {degen}")
        failed = True
    if nm1 > nm0:
        print(f"    !! 非多様体が増えた（罠40）: {nm0}->{nm1}")
        failed = True

if save and not failed:
    bpy.ops.wm.save_mainfile()
    print("  saved:", bpy.data.filepath)
elif save:
    print("  !! 検算NGのため保存しない")

sys.exit(1 if failed else 0)
