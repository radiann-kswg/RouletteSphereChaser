using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// 36球ソークの記録係（Tools > Run Soak から一時的に挿入される）。
/// 目的は2つ:
///  1. 各スコアトリガーの到達回数を採る（配点再計算＝DESIGN-v2 フェーズ7の素材、かつ移行の回帰検出）
///  2. **想定レーンから脱線した球を捕まえる** — 盆地の外へ出た球・床下へ抜けた球・
///     どこにも到達しないまま長時間さまよう球・止まったまま動かない球。
/// 結果は Docs/soak_result.json に書き、`duration` 秒でプレイモードを抜ける。
public class SoakRecorder : MonoBehaviour
{
    public float duration = 180f;
    public string outPath = "Docs/soak_result.json";
    public string label = "run";

    // パーク境界（ParkAssembly の実測AABB X[-12.2,13.8] Y[-0.08,14] Z[-8,8] に余裕を持たせた枠）
    public Vector3 boundsMin = new Vector3(-13.5f, -0.6f, -9.5f);
    public Vector3 boundsMax = new Vector3(15.0f, 16.0f, 9.5f);

    public float stuckSpeed = 0.02f;     // これ未満が
    public float stuckSeconds = 25f;     // この秒数続いたら「停止」
    public float strandedSeconds = 90f;  // 一度も得点/周回せずこの秒数さまよったら「迷子」

    class Track
    {
        public float stillFor, sinceProgress;
        public int lastLaps, lastPending;
        public bool reportedStuck, reportedStranded, reportedEscape;
    }

    /// 同じシーンを比べるときに再現性を持たせる（BallLift.releaseJitter 等が Random を引くため）。
    /// 別シーン同士は接触処理順が変わるので、種を揃えても軌道は一致しない点に注意。
    public int seed = 12345;

    readonly Dictionary<LotteryBall, Track> tracks = new();
    readonly List<string> escapes = new(), stucks = new(), strandeds = new();
    float t;

    void Awake() { Random.InitState(seed); }

    void FixedUpdate()
    {
        t += Time.fixedDeltaTime;

        foreach (var ball in Object.FindObjectsByType<LotteryBall>(FindObjectsSortMode.None))
        {
            if (!tracks.TryGetValue(ball, out var tr)) tracks[ball] = tr = new Track();
            var rb = ball.GetComponent<Rigidbody>();
            var p = ball.transform.position;

            // 1) 枠外＝コースアウト
            bool outside = p.x < boundsMin.x || p.x > boundsMax.x
                        || p.y < boundsMin.y || p.y > boundsMax.y
                        || p.z < boundsMin.z || p.z > boundsMax.z;
            if (outside && !tr.reportedEscape)
            {
                tr.reportedEscape = true;
                escapes.Add(Row(ball, p, "outOfBounds"));
                Debug.LogWarning($"[Soak] ESCAPE {ball.name} at {p} t={t:F1}");
            }

            // 2) 止まったまま（リフト搬送中の isKinematic は除外）
            if (rb != null && !rb.isKinematic && rb.linearVelocity.magnitude < stuckSpeed) tr.stillFor += Time.fixedDeltaTime;
            else tr.stillFor = 0f;
            if (tr.stillFor > stuckSeconds && !tr.reportedStuck)
            {
                tr.reportedStuck = true;
                stucks.Add(Row(ball, p, "stuck"));
                Debug.LogWarning($"[Soak] STUCK {ball.name} at {p} t={t:F1}");
            }

            // 3) 得点も周回もしないまま長時間＝どこかで脱線して滞留している
            if (ball.laps != tr.lastLaps || ball.pendingPoints != tr.lastPending)
            {
                tr.lastLaps = ball.laps; tr.lastPending = ball.pendingPoints; tr.sinceProgress = 0f;
            }
            else tr.sinceProgress += Time.fixedDeltaTime;
            if (tr.sinceProgress > strandedSeconds && !tr.reportedStranded)
            {
                tr.reportedStranded = true;
                strandeds.Add(Row(ball, p, "stranded"));
                Debug.LogWarning($"[Soak] STRANDED {ball.name} at {p} t={t:F1}");
            }
        }

        if (t >= duration) Finish();
    }

    string Row(LotteryBall b, Vector3 p, string why)
    {
        return string.Format("{{\"ball\":\"{0}\",\"why\":\"{1}\",\"t\":{2:F1},\"pos\":[{3:F3},{4:F3},{5:F3}],\"laps\":{6},\"score\":{7}}}",
            b.name, why, t, p.x, p.y, p.z, b.laps, b.totalScore);
    }

    void Finish()
    {
        var zones = Object.FindObjectsByType<ScoreZone>(FindObjectsSortMode.None);
        var balls = Object.FindObjectsByType<LotteryBall>(FindObjectsSortMode.None);

        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($" \"label\": \"{label}\", \"duration\": {t:F1}, \"balls\": {balls.Length},");

        int totalHits = 0, totalLaps = 0, totalScore = 0;
        foreach (var z in zones) totalHits += z.hits;
        foreach (var b in balls) { totalLaps += b.laps; totalScore += b.totalScore + b.pendingPoints; }
        sb.AppendLine($" \"totalHits\": {totalHits}, \"totalLaps\": {totalLaps}, \"totalScore\": {totalScore},");

        sb.AppendLine($" \"escapes\": [{string.Join(",", escapes)}],");
        sb.AppendLine($" \"stuck\": [{string.Join(",", stucks)}],");
        sb.AppendLine($" \"stranded\": [{string.Join(",", strandeds)}],");

        // 同名の兄弟トリガーが多数あるので、キーにワールド座標を混ぜて一意かつ実行間で安定にする
        var rows = new List<string>();
        foreach (var z in zones)
        {
            var p = z.transform.position;
            rows.Add($"  \"{FullPath(z.transform)}@{p.x:F2},{p.y:F2},{p.z:F2}\": {z.hits}");
        }
        rows.Sort();
        sb.AppendLine(" \"zoneHits\": {");
        sb.AppendLine(string.Join(",\n", rows));
        sb.AppendLine(" }");
        sb.AppendLine("}");

        System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "..", outPath), sb.ToString());
        Debug.Log($"[Soak] done label={label} hits={totalHits} laps={totalLaps} escapes={escapes.Count} stuck={stucks.Count} stranded={strandeds.Count}");
        enabled = false;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    static string FullPath(Transform t)
    {
        var s = t.name;
        while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
        return s;
    }
}
