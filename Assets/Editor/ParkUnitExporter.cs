using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// Tools > Export Park Units (prefabs + unitypackage)
/// ビルド済みシーンの `Park` を「タワー × ユニット役割」に切り分けてPrefab化し、素材パッケージを出す（AGENTS.md 7章）。
///
/// 役割は Blender 側の `unit_role`（= params.json の `role`）が正本:
///   mech = 抽選機（機構メッシュ＋トリガー・回転体・得点ラベル） / tray = 受け皿・樋 / leg = 支柱・フレーム /
///   relay = タワー間中継レーン（HighLane / JPRail / JPTube ごと） / base = 土台・回収系
/// 出力: Assets/Prefabs/Units/<Group>_<Role>.prefab（git管轄） と Exports/RouletteSphereChaser_Units.unitypackage（LFS）
/// Prefabは座標をワールドのまま持つ（ルートを動かせば塔ごと移動できる）。
public static class ParkUnitExporter
{
    const string PrefabDir = "Assets/Prefabs/Units";
    const string Package = "Exports/RouletteSphereChaser_Units.unitypackage";

    [System.Serializable] class MeshRow { public string name, path, role; }
    [System.Serializable] class ParkParams { public MeshRow[] meshes; }

    [MenuItem("Tools/Export Park Units (prefabs + unitypackage)")]
    public static void Export()
    {
        var park = GameObject.Find("Park");
        if (park == null) { Debug.LogError("[ParkUnitExporter] Park が無い。先に Tools > Build RouletteSphere Park (v2)"); return; }
        var pp = JsonUtility.FromJson<ParkParams>(System.IO.File.ReadAllText("Assets/Models/ParkAssembly.params.json"));

        // ユニット = (トップレベルグループ, 役割)。中継レーンだけは種類（名前の接頭辞）で分ける
        var units = new Dictionary<string, List<MeshRow>>();
        foreach (var m in pp.meshes)
        {
            var parts = m.path.Split('/');
            if (parts.Length < 3) continue;
            string group = parts[1];
            string role = string.IsNullOrEmpty(m.role) ? "mech" : m.role;
            string key = role == "relay" ? group + "_" + m.name.Split('_')[0]
                       : group + "_" + (role == "leg" ? "Legs" : char.ToUpper(role[0]) + role.Substring(1));
            if (!units.TryGetValue(key, out var list)) units[key] = list = new List<MeshRow>();
            list.Add(m);
        }

        System.IO.Directory.CreateDirectory(PrefabDir);
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Package));
        int made = 0;
        foreach (var kv in units.OrderBy(k => k.Key))
        {
            string group = kv.Value[0].path.Split('/')[1];
            bool mech = kv.Key.EndsWith("_Mech") || kv.Key.EndsWith("_Base");   // 土台系はリフト・撹拌のマーカーも同梱
            var src = park.transform.Find(group);
            if (src == null) { Debug.LogWarning("[ParkUnitExporter] group欠落: " + group); continue; }

            var copy = Object.Instantiate(src.gameObject);
            copy.name = kv.Key;

            // 残すもの: このユニットのメッシュ（＋祖先）。抽選機ユニットは機能マーカー（トリガー・回転体・ラベル等）とその配下も残す
            var keep = new HashSet<Transform>();
            void KeepUp(Transform t) { for (var x = t; x != null && x != copy.transform; x = x.parent) keep.Add(x); }
            // 同名メッシュ（ScoreGate_10 ×4 等）があるので Find ではなく相対パスの文字列一致で拾う。
            // 回転ピボット直下のメッシュは ParkBuilder が Leaf_Mesh に改名している（パスはピボットと同じ）
            var unitPaths = new HashSet<string>(kv.Value.Select(m => m.path.Substring(("Park/" + group).Length).TrimStart('/')));
            int hit = 0;
            foreach (var t in copy.GetComponentsInChildren<Transform>(true))
            {
                if (t == copy.transform || t.GetComponent<MeshRenderer>() == null) continue;
                string rel = Rel(t, copy.transform);
                if (unitPaths.Contains(rel) || (t.name.EndsWith("_Mesh") && unitPaths.Contains(Rel(t.parent, copy.transform))))
                { KeepUp(t); hit++; }
            }
            if (hit < kv.Value.Count) Debug.LogWarning($"[ParkUnitExporter] {kv.Key}: メッシュ {hit}/{kv.Value.Count} しか拾えていない");
            if (mech)
                foreach (var t in copy.GetComponentsInChildren<Transform>(true))
                    if (t.GetComponent<ScoreZone>() || t.GetComponent<Rotator>() || t.GetComponent<Oscillator>() ||
                        t.GetComponent<BallLift>() || t.GetComponent<LapGate>() || t.GetComponent<TMPro.TextMeshPro>())
                    { KeepUp(t); foreach (var c in t.GetComponentsInChildren<Transform>(true)) keep.Add(c); }

            var all = copy.GetComponentsInChildren<Transform>(true).Where(t => t != copy.transform)
                          .OrderByDescending(t => Depth(t)).ToList();
            foreach (var t in all) if (t != null && !keep.Contains(t)) Object.DestroyImmediate(t.gameObject);

            PrefabUtility.SaveAsPrefabAsset(copy, $"{PrefabDir}/{kv.Key}.prefab");
            Object.DestroyImmediate(copy);
            made++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.ExportPackage(PrefabDir, Package, ExportPackageOptions.Recurse | ExportPackageOptions.IncludeDependencies);
        Debug.Log($"[ParkUnitExporter] prefabs={made} -> {Package} ({new System.IO.FileInfo(Package).Length / 1024} KB)");
    }

    static int Depth(Transform t) { int d = 0; for (; t.parent != null; t = t.parent) d++; return d; }
    static string Rel(Transform t, Transform root)
    {
        var s = t.name;
        for (var x = t.parent; x != null && x != root; x = x.parent) s = x.name + "/" + s;
        return s;
    }
}
