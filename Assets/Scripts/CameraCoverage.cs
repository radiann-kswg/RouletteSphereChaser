using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// 定点カメラの「死角」を実測する計測器（ソーク中に常駐）。
///
/// 死角の定義を**ボール基準**にしているのが要点。メッシュ表面のサンプルだと裏面が常に見えないので
/// 「見えない＝死角」の判定にならない。ここでは
///   担当機構の範囲にボールが居た瞬間を母数にして、
///     (a) 画角の外            … 画角不足の死角
///     (b) 画角内だが遮蔽された … 手前の造形に隠れる死角
///     (c) 見えた
/// を数える。(a)+(b) が多い台だけオービットさせればよい。
public class CameraCoverage : MonoBehaviour
{
    public float sampleInterval = 0.25f;
    public string outPath = "Docs/camera_coverage.json";

    /// カメラ名（`Cam_` を除いた部分）-> 担当する Park 直下グループ。ParkBuilder も同じ表を使う
    public static readonly Dictionary<string, string[]> Assign = new()
    {
        { "A_Overview", new[] { "TowerA", "TowerA23" } },
        { "A_GrandRoulette", new[] { "TowerA23" } },
        { "H_Garapon", new[] { "TowerH" } },
        { "F_JPSpinner_S", new[] { "TowerF_S" } },
        { "F_JPSpinner_N", new[] { "TowerF_N" } },
        { "E_PocketDisc_S", new[] { "TowerE_S" } },
        { "E_PocketDisc_N", new[] { "TowerE_N" } },
        { "C_Zigzag_S", new[] { "TowerC_S" } },
        { "C_Zigzag_N", new[] { "TowerC_N" } },
        { "G_Numa_E", new[] { "TowerG_E" } },
        { "G_Numa_W", new[] { "TowerG_W" } },
        { "B_Pachinko_E", new[] { "TowerB_E" } },
        { "B_Pachinko_W", new[] { "TowerB_W" } },
        { "D_Kuruun_E", new[] { "TowerD_E" } },
        { "D_Kuruun_W", new[] { "TowerD_W" } },
        { "DrainStation", new[] { "DrainStation", "Lifts" } },
    };

    /// 抽選機カメラを映しているときにHUDへ出す表示名。
    /// **PenchantManufacture書体はCJK未収録で日本語が豆腐になる**ため英名で持つ（User報告 2026-08-24）。
    /// 日本語で出したくなったらCJK収録フォントを別途HUDへ割り当てること。
    public static readonly Dictionary<string, string> DisplayName = new()
    {
        { "A_Overview", "Tower A - Spiral Overview" },
        { "A_GrandRoulette", "Tower A - Grand Roulette" },
        { "H_Garapon", "Tower H - Garapon" },
        { "F_JPSpinner_S", "Tower F - JP Spinner South" },
        { "F_JPSpinner_N", "Tower F - JP Spinner North" },
        { "E_PocketDisc_S", "Tower E - Pocket Disc South" },
        { "E_PocketDisc_N", "Tower E - Pocket Disc North" },
        { "C_Zigzag_S", "Tower C - Zigzag South" },
        { "C_Zigzag_N", "Tower C - Zigzag North" },
        { "G_Numa_E", "Tower G - Numa Kuruun East" },
        { "G_Numa_W", "Tower G - Numa Kuruun West" },
        { "B_Pachinko_E", "Tower B - Pachinko East" },
        { "B_Pachinko_W", "Tower B - Pachinko West" },
        { "D_Kuruun_E", "Tower D - Kuruun East" },
        { "D_Kuruun_W", "Tower D - Kuruun West" },
        { "DrainStation", "Drain Station / Lifts" },
    };

    /// `Cam_xxx` -> `xxx`
    public static string KeyOf(string cameraName)
    {
        return cameraName.StartsWith("Cam_") ? cameraName.Substring(4) : cameraName;
    }

    /// 「死角」を体積で測るための格子サイズ[m]。ボールが居た区画のうち、
    /// **一度も映らなかった区画**の割合が本当の死角。時間平均の可視率とは別物なので両方出す。
    public float voxel = 0.15f;

    class Cov
    {
        public Camera cam;
        public Vector3 focus;      // 「映すべき」範囲の中心（＝カメラの注視点）
        public float radius;       // 同・半径
        public int inRegion, outOfFrustum, occluded, seen;
        public readonly HashSet<Vector3Int> visited = new(), everSeen = new();
        public readonly Dictionary<string, int> blockers = new();   // 遮蔽したコライダ名 -> 回数
    }

    readonly List<Cov> covs = new();
    float acc;

    void Start()
    {
        var park = GameObject.Find("Park");
        var cams = park.transform.Find("Cameras");
        if (cams == null) { enabled = false; return; }

        foreach (Transform t in cams)
        {
            var cam = t.GetComponent<Camera>();
            var orb = t.GetComponent<OrbitCamera>();
            if (cam == null || orb == null) continue;   // 担当範囲が決まっている定点カメラだけ
            covs.Add(new Cov { cam = cam, focus = orb.pivot, radius = orb.focusRadius });
        }
        Debug.Log($"[Coverage] tracking {covs.Count} fixed cameras");
    }

    void Update()
    {
        acc += Time.deltaTime;
        if (acc < sampleInterval) return;
        acc = 0f;

        var balls = Object.FindObjectsByType<LotteryBall>(FindObjectsSortMode.None);
        foreach (var c in covs)
        {
            var planes = GeometryUtility.CalculateFrustumPlanes(c.cam);
            Vector3 eye = c.cam.transform.position;
            foreach (var ball in balls)
            {
                Vector3 p = ball.transform.position;
                if ((p - c.focus).sqrMagnitude > c.radius * c.radius) continue;
                c.inRegion++;
                var cell = new Vector3Int(Mathf.FloorToInt(p.x / voxel), Mathf.FloorToInt(p.y / voxel), Mathf.FloorToInt(p.z / voxel));
                c.visited.Add(cell);

                bool inFrustum = true;
                foreach (var pl in planes)
                    if (pl.GetDistanceToPoint(p) < -0.05f) { inFrustum = false; break; }
                if (!inFrustum) { c.outOfFrustum++; continue; }

                // 球の手前に何かあるか。球の表面ぶんだけ手前で止めて自分自身を拾わない
                Vector3 d = p - eye;
                float dist = d.magnitude - 0.06f;
                // 透過アクリルのシェル（SeeThroughレイヤ）は視界を塞がないので数えない。
                // 物理コライダとしては生きているのでボールの通り道は変わらない。
                int mask = ~0;
                int st = LayerMask.NameToLayer("SeeThrough");
                if (st >= 0) mask &= ~(1 << st);
                if (dist > 0f && Physics.Raycast(eye, d.normalized, out var hit, dist, mask, QueryTriggerInteraction.Ignore))
                {
                    c.occluded++;
                    // 「何に隠されたか」を数える。これが無いと、どのメッシュを抜けばいいのか当て推量になる
                    string k = hit.collider.name;
                    c.blockers.TryGetValue(k, out int n);
                    c.blockers[k] = n + 1;
                }
                else { c.seen++; c.everSeen.Add(cell); }
            }
        }
    }

    /// 遮蔽回数の多い順に上位5件。「どのメッシュを透かす/抜くか」を決めるための実測値
    static string TopBlockers(Cov c)
    {
        var top = new List<KeyValuePair<string, int>>(c.blockers);
        top.Sort((a, b) => b.Value.CompareTo(a.Value));
        var parts = new List<string>();
        for (int i = 0; i < top.Count && i < 5; i++)
            parts.Add("\"" + top[i].Key + "\":" + top[i].Value);
        return string.Join(",", parts);
    }

    /// SoakRecorder から呼ばれる（ソーク終了時）
    public void Dump()
    {
        var rows = new List<string>();
        foreach (var c in covs)
        {
            float total = Mathf.Max(1, c.inRegion);
            var orb = c.cam.GetComponent<OrbitCamera>();
            float cells = Mathf.Max(1, c.visited.Count);
            rows.Add("  {\"cam\":\"" + c.cam.name + "\""
                   + ",\"samples\":" + c.inRegion
                   + ",\"seen\":" + c.seen
                   + ",\"outOfFrustum\":" + c.outOfFrustum
                   + ",\"occluded\":" + c.occluded
                   + ",\"timeCoverage\":" + (c.seen / total).ToString("F4")
                   + ",\"cellsVisited\":" + c.visited.Count
                   + ",\"cellsSeen\":" + c.everSeen.Count
                   + ",\"blindSpot\":" + (1f - c.everSeen.Count / cells).ToString("F4")
                   + ",\"orbitDps\":" + (orb != null ? orb.degreesPerSecond : 0f).ToString("F1")
                   + ",\"radius\":" + c.radius.ToString("F2")
                   + ",\"blockers\":{" + TopBlockers(c) + "}}");
        }
        var sb = new StringBuilder();
        sb.AppendLine("{ \"cameras\": [");
        sb.AppendLine(string.Join(",\n", rows));
        sb.AppendLine("] }");
        System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "..", outPath), sb.ToString());
        Debug.Log("[Coverage] wrote " + outPath);
    }
}
