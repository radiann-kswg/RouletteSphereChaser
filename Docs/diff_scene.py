"""旧シーンのダンプ（park_layout / park_markers / park_labels）と、
解釈器で作り直したシーンのダンプ（park_after）を突き合わせる。

    python3 Docs/diff_scene.py

判定: 位置・回転・トリガー寸法・コンポーネントのパラメータがすべて許容内で一致すること。
**回転ピボットのスケールだけは意図的に変わる**（罠46: スケール1ピボットへ統一）ので
スケール差はピボットに限って許容し、件数だけ報告する。
"""

import json
import math
import os
import collections

DOCS = os.path.dirname(os.path.abspath(__file__))
TOL = 2e-4


def load(name):
    return json.load(open(os.path.join(DOCS, name), encoding="utf-8"))


def pos(m):
    return (m[3], m[7], m[11])


def rot(m):
    """列を正規化した回転基底（スケールを除いた向き）"""
    out = []
    for j in range(3):
        col = (m[j], m[4 + j], m[8 + j])
        n = math.sqrt(sum(x * x for x in col)) or 1.0
        out += [x / n for x in col]
    return out


def scale(m):
    return [math.sqrt(m[j] ** 2 + m[4 + j] ** 2 + m[8 + j] ** 2) for j in range(3)]


def nearest(cands, f, val):
    return min(max(abs(a - b) for a, b in zip(val, f(c))) for c in cands) if cands else 9.9


def main():
    A = load("park_after.json")
    L = load("park_layout.json")["instances"]
    M = load("park_markers.json")["markers"]
    LB = load("park_labels.json")["labels"]

    fails = []

    # ---- メッシュ: ワールドAABB・コライダ種別・マテリアル ----
    # 回転ピボットと同名のメッシュは "<名前>_Mesh" として子に入る（罠46）ので正規化して照合する
    def norm(p):
        s = p.split("/")
        return "/".join(s[:-1]) if s[-1].endswith("_Mesh") else p

    byp = collections.defaultdict(list)
    for i in A["instances"]:
        byp[norm(i["path"])].append(i)
    worst = (0.0, "")
    for o in L:
        cs = byp.get(o["path"], [])
        if not cs:
            fails.append(("mesh missing", o["path"]))
            continue
        e = min(max(max(abs(o["worldCenter"][k] - c["worldCenter"][k]) for k in range(3)),
                    max(abs(o["worldExtents"][k] - c["worldExtents"][k]) for k in range(3))) for c in cs)
        worst = max(worst, (e, o["path"]))
    if worst[0] > TOL:
        fails.append(("mesh bounds", worst))

    oc = collections.Counter(i["collider"] for i in L)
    nc = collections.Counter(i["collider"] for i in A["instances"])
    if oc != nc:
        fails.append(("collider counts", dict(oc), dict(nc)))
    mats = load("park_materials.json")
    om = collections.Counter(mats.get(i["path"], {}).get("mat", "") for i in L)
    nm = collections.Counter(i["mat"] for i in A["instances"])
    if om != nm:
        fails.append(("material counts", {k: (om.get(k, 0), nm.get(k, 0)) for k in set(om) | set(nm) if om.get(k, 0) != nm.get(k, 0)}))

    # ---- 機能マーカー ----
    mbyp = collections.defaultdict(list)
    for m in A["markers"]:
        mbyp[m["path"]].append(m)
    wp = wr = 0.0
    pivot_scale_changed = 0
    for o in M:
        cs = mbyp.get(o["path"], [])
        if not cs:
            fails.append(("marker missing", o["path"]))
            continue
        wp = max(wp, nearest(cs, lambda c: pos(c["m"]), pos(o["m"])))
        wr = max(wr, nearest(cs, lambda c: rot(c["m"]), rot(o["m"])))
        se = nearest(cs, lambda c: scale(c["m"]), scale(o["m"]))
        if se > TOL:
            if "rotator" in o or "oscillator" in o:
                pivot_scale_changed += 1        # 罠46の意図的な差
            else:
                fails.append(("trigger size", o["path"], scale(o["m"]), scale(cs[0]["m"])))
    if wp > TOL:
        fails.append(("marker position", wp))
    if wr > TOL:
        fails.append(("marker rotation", wr))

    op = sorted((o["path"], o["scoreZone"]["points"], o["scoreZone"]["grantMultiplier"]) for o in M if "scoreZone" in o)
    np_ = sorted((o["path"], o["points"], o["mult"]) for o in A["markers"] if "points" in o)
    if op != np_:
        fails.append(("ScoreZone params", len(op), len(np_)))

    for oldk, newk, keys in (("rotator", "rot", ("dps",)),
                             ("oscillator", "osc", ("a", "b", "period", "phase"))):
        o = {x["path"]: x[oldk] for x in M if oldk in x}
        n = {x["path"]: x[newk] for x in A["markers"] if newk in x}
        if set(o) != set(n):
            fails.append((oldk + " paths", sorted(set(o) ^ set(n))))
            continue
        for p in o:
            if max(abs(o[p][k] - n[p][k]) for k in keys) > 1e-3:
                fails.append((oldk + " params", p, o[p], n[p]))
            if max(abs(a - b) for a, b in zip(o[p]["axis"], n[p]["axis"])) > 1e-4:
                fails.append((oldk + " axis", p, o[p]["axis"], n[p]["axis"]))

    lo = {x["path"]: x["ballLift"] for x in M if "ballLift" in x}
    ln = {x["path"]: x["lift"] for x in A["markers"] if "lift" in x}
    if set(lo) != set(ln):
        fails.append(("lift paths", sorted(set(lo) ^ set(ln))))
    for p in lo:
        if abs(lo[p]["speed"] - ln[p]["speed"]) > 1e-3 or abs(lo[p]["releaseJitter"] - ln[p]["jitter"]) > 1e-3:
            fails.append(("lift params", p))
        if len(lo[p]["waypoints"]) != len(ln[p]["wp"]):
            fails.append(("lift waypoint count", p))
        else:
            e = max(max(abs(a - b) for a, b in zip(x, y)) for x, y in zip(lo[p]["waypoints"], ln[p]["wp"]))
            if e > TOL:
                fails.append(("lift waypoints", p, e))

    # ---- 得点表示 ----
    lbyp = collections.defaultdict(list)
    for x in A["labels"]:
        lbyp[x["path"]].append(x)
    lp = lr = 0.0
    for o in LB:
        cs = lbyp.get(o["path"], [])
        if not cs:
            fails.append(("label missing", o["path"]))
            continue
        lp = max(lp, nearest(cs, lambda c: pos(c["m"]), pos(o["m"])))
        lr = max(lr, nearest(cs, lambda c: rot(c["m"]), rot(o["m"])))
    if lp > TOL:
        fails.append(("label position", lp))
    if lr > TOL:
        fails.append(("label rotation", lr))
    key = lambda o: (o["path"], o["text"], round(o["fontSize"], 2), tuple(round(v, 3) for v in o["color"]), o["billboard"])
    to, tn = collections.Counter(map(key, LB)), collections.Counter(map(key, A["labels"]))
    if to != tn:
        fails.append(("label content", list(to - tn)[:5], list(tn - to)[:5]))

    print(f"mesh      {len(A['instances'])}/{len(L)}  worst bounds err = {worst[0]:.7f} m ({worst[1]})")
    print(f"marker    {len(A['markers'])}/{len(M)}  worst pos = {wp:.7f} m  worst rot = {wr:.7f}")
    print(f"label     {len(A['labels'])}/{len(LB)}  worst pos = {lp:.7f} m  worst rot = {lr:.7f}")
    print(f"回転ピボットのスケールを1に統一した件数（罠46・意図的）: {pivot_scale_changed}")
    print("FAILURES:", len(fails))
    for f in fails[:20]:
        print("  ", f)
    return 0 if not fails else 1


if __name__ == "__main__":
    raise SystemExit(main())
