using UnityEditor;
using UnityEngine;

/// v2以降のビルダー共通ヘルパ。AGENTS.md 3章の罠対策（Cylinder凸メッシュ化・
/// トリガー可視化オフ・点数マーカー自動付与など）を全生成物に強制する。
/// ponytail: GreyboxBuilder(v1保存用)内の同種ヘルパは意図的に残置。共通化はv1を凍結してから。
public static class GreyboxKit
{
    public static Material Track, Rail, Accent;
    public static PhysicsMaterial RailPM;

    public static void Init()
    {
        Track = Mat("Greybox_Track", new Color(0.55f, 0.55f, 0.58f));
        Rail = Mat("Greybox_Rail", new Color(0.35f, 0.35f, 0.40f));
        Accent = Mat("Greybox_Accent", new Color(0.95f, 0.55f, 0.15f));
        RailPM = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/Materials/RailPM.asset");
        if (RailPM == null)
        {
            RailPM = new PhysicsMaterial("Rail") { dynamicFriction = 0.05f, staticFriction = 0.05f, bounciness = 0.1f };
            AssetDatabase.CreateAsset(RailPM, "Assets/Materials/RailPM.asset");
        }
    }

    public static Material Mat(string name, Color c)
    {
        string path = $"Assets/Materials/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c);
            AssetDatabase.CreateAsset(m, path);
        }
        return m;
    }

    public static Transform Group(Transform parent, string name)
    {
        var g = new GameObject(name).transform;
        g.SetParent(parent);
        return g;
    }

    public static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 euler, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = pos;
        go.transform.eulerAngles = euler;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (type == PrimitiveType.Cylinder)
        {
            // カプセルコライダ罠対策: 円柱は必ず凸メッシュに
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var mc = go.AddComponent<MeshCollider>();
            mc.convex = true;
            mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
        }
        go.GetComponent<Collider>().material = RailPM;
        return go;
    }

    /// コライダ無しの見た目専用バー
    public static void VisualBar(Transform parent, string name, Vector3 pos, Vector3 euler, Vector3 scale)
    {
        var go = Prim(PrimitiveType.Cube, parent, name, pos, euler, scale, Rail);
        Object.DestroyImmediate(go.GetComponent<Collider>());
    }

    /// p1→p2 の床＋両側レール。注意: レール占有域が既存レーンを横切らないこと（AGENTS 3-9）
    public static void Ramp(Transform parent, string name, Vector3 p1, Vector3 p2, float width)
    {
        var g = Group(parent, name);
        Vector3 mid = (p1 + p2) * 0.5f;
        float len = Vector3.Distance(p1, p2);
        Quaternion rot = Quaternion.LookRotation(p2 - p1);
        Prim(PrimitiveType.Cube, g, "Floor", mid, rot.eulerAngles, new Vector3(width, 0.02f, len + 0.04f), Track);
        foreach (float s in new[] { -1f, 1f })
            Prim(PrimitiveType.Cube, g, s < 0 ? "RailL" : "RailR", mid + rot * new Vector3(s * (width * 0.5f + 0.01f), 0.05f, 0),
                rot.eulerAngles, new Vector3(0.02f, 0.1f, len + 0.04f), Rail);
    }

    public static void WallRing(Transform parent, float r, float y, float h, int segs, float gapCenterDeg = -999f, float gapHalfDeg = 0f)
    {
        for (int i = 0; i < segs; i++)
        {
            float a = i / (float)segs * 360f;
            if (gapHalfDeg > 0f && Mathf.Abs(Mathf.DeltaAngle(a, gapCenterDeg)) < gapHalfDeg) continue;
            float rad = a * Mathf.Deg2Rad;
            float w = 2f * Mathf.PI * r / segs + 0.02f;
            Prim(PrimitiveType.Cube, parent, $"Wall_{i}", new Vector3(r * Mathf.Cos(rad), y, r * Mathf.Sin(rad)),
                new Vector3(0, -a + 90f, 0), new Vector3(w, h, 0.03f), Rail);
        }
    }

    /// スコアトリガー（points>0かつautoLabelなら点数マーカー自動付与）
    public static GameObject Trigger(Transform parent, string name, Vector3 pos, Vector3 size, int points, bool autoLabel = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = pos;
        go.transform.localScale = size;
        go.GetComponent<Collider>().isTrigger = true;
        go.GetComponent<Renderer>().enabled = false;
        go.AddComponent<ScoreZone>().points = points;
        if (points > 0 && autoLabel) ScoreLabel(parent, name, pos + Vector3.up * 0.12f, points);
        return go;
    }

    public static void ScoreLabel(Transform parent, string zoneName, Vector3 pos, int points)
    {
        var go = new GameObject(zoneName + "_Label");
        go.transform.SetParent(parent);
        go.transform.position = pos;
        var tm = go.AddComponent<TextMesh>();
        tm.text = $"{points}pt";
        tm.fontSize = 48;
        tm.characterSize = 0.03f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.color = new Color(1f, 0.85f, 0.2f);
        go.AddComponent<Billboard>();
    }

    public static Transform Waypoint(Transform parent, string name, Vector3 pos)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent);
        t.position = pos;
        return t;
    }

    /// 攪拌ローター（アーチ崩し標準装備品）
    public static void Stirrer(Transform parent, string name, Vector3 pos, Vector3 euler, int arms, float armLen, float degPerSec)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(euler);
        go.AddComponent<Rotator>().degreesPerSecond = degPerSec;
        for (int i = 0; i < arms; i++)
            Prim(PrimitiveType.Cube, go.transform, $"Arm_{i}", pos,
                euler + new Vector3(0, i * (180f / arms), 0), new Vector3(0.02f, 0.06f, armLen), Accent);
    }
}
