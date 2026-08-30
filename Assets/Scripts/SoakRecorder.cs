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
        public bool inWallBand;   // 外周壁帯に高所進入中（脱線イベントの立ち上がり検出用）
    }

    /// 同じシーンを比べるときに再現性を持たせる（BallLift.releaseJitter 等が Random を引くため）。
    /// 別シーン同士は接触処理順が変わるので、種を揃えても軌道は一致しない点に注意。
    public int seed = 12345;

    [Header("カメラ撮影")]
    public float shotInterval = 15f;      // デモ演出のいまのショットを定期撮影
    public bool shotOnAnomaly = true;     // コースアウト/停止/迷子を見つけたら集中撮影
    public string shotDir = "Docs/soak_shots";
    public int shotWidth = 960, shotHeight = 540;

    CameraDirector director;
    CameraCoverage coverage;
    float shotAcc;
    int shotNo;

    // 追従カメラが対象を画角内に保てているかの実測（リフト上昇に追いつくかの検証。User報告 2026-08-24）
    int framedFree, offFree, framedLift, offLift;
    float worstOffsetFree, worstOffsetLift;
    float frameAcc;

    readonly Dictionary<LotteryBall, Track> tracks = new();
    readonly List<string> escapes = new(), stucks = new(), strandeds = new(), derails = new();
    float t;

    void Awake() { Random.InitState(seed); }

    void Start()
    {
        director = Object.FindFirstObjectByType<CameraDirector>();
        coverage = gameObject.AddComponent<CameraCoverage>();
        System.IO.Directory.CreateDirectory(ShotPath(""));
    }

    string ShotPath(string file)
    {
        return System.IO.Path.Combine(Application.dataPath, "..", shotDir, file);
    }

    /// 指定カメラの絵をPNGで保存する。カメラの enabled 状態に関係なく描ける
    void Shot(Camera cam, string tag)
    {
        if (cam == null) return;
        var rt = new RenderTexture(shotWidth, shotHeight, 24);
        var prev = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = prev;

        var prevActive = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(shotWidth, shotHeight, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, shotWidth, shotHeight), 0, 0);
        tex.Apply();
        RenderTexture.active = prevActive;

        string name = string.Format("{0}_{1:D3}_{2:F0}s_{3}_{4}.png", label, ++shotNo, t, tag, cam.name);
        System.IO.File.WriteAllBytes(ShotPath(name), tex.EncodeToPNG());
        Destroy(tex);
        rt.Release();
        Destroy(rt);
    }

    /// 異常が起きた球を、一番よく映せるカメラで押さえる（近い定点カメラ＋メインの追従）
    void ShotAnomaly(LotteryBall b, string why)
    {
        if (!shotOnAnomaly) return;
        Camera best = null;
        float bestScore = float.MaxValue;
        var park = GameObject.Find("Park");
        var cams = park != null ? park.transform.Find("Cameras") : null;
        if (cams != null)
            foreach (Transform ct in cams)
            {
                var c = ct.GetComponent<Camera>();
                if (c == null || c.GetComponent<RandomFollowCamera>() != null) continue;
                var vp = c.WorldToViewportPoint(b.transform.position);
                if (vp.z <= 0f) continue;                       // カメラの後ろ
                if (vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f) continue;  // 画角外
                float d = Vector3.Distance(c.transform.position, b.transform.position);
                if (d < bestScore) { bestScore = d; best = c; }
            }
        Shot(best != null ? best : Camera.main, why + "_" + b.name);
    }

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
                ShotAnomaly(ball, "ESCAPE");
            }

            // 1.5) 外周壁マージン帯への高所進入＝機構から飛び出したオーバーシュート（脱線）。
            //      枠内に収まっていても、壁に当たって盆地へ落ちる球はコース設計上の取りこぼし（User要望 2026-08-30）。
            //      帯: 南北壁際 |z|>6.2 ／ 西壁際 x<-9.8 ／ 東は排水路(y<0.6)を除く x>10.2。y>0.6=床バウンドより上。
            //      リフト回廊(x>11.8)と搬送中(isKinematic)は正規ルートなので除外する。
            //      盆地床は3°で西高東低（西端y≈1.1）なので、しきい値は局所床高からの相対で見る。
            float localFloor = 0.62f - 0.05f * p.x;
            bool wallBand = p.y > localFloor + 0.45f && p.x < 11.8f && !(rb != null && rb.isKinematic)
                            && (Mathf.Abs(p.z) > 6.2f || p.x < -9.8f || p.x > 10.2f);
            if (wallBand && !tr.inWallBand)
            {
                tr.inWallBand = true;
                derails.Add(Row(ball, p, "derail"));
                Debug.LogWarning($"[Soak] DERAIL {ball.name} at {p} t={t:F1}");
            }
            else if (!wallBand) tr.inWallBand = false;

            // 2) 止まったまま（リフト搬送中の isKinematic は除外）
            if (rb != null && !rb.isKinematic && rb.linearVelocity.magnitude < stuckSpeed) tr.stillFor += Time.fixedDeltaTime;
            else tr.stillFor = 0f;
            if (tr.stillFor > stuckSeconds && !tr.reportedStuck)
            {
                tr.reportedStuck = true;
                stucks.Add(Row(ball, p, "stuck"));
                Debug.LogWarning($"[Soak] STUCK {ball.name} at {p} t={t:F1}");
                ShotAnomaly(ball, "STUCK");
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
                ShotAnomaly(ball, "STRANDED");
            }
        }

        // 追従カメラの追随性: いま映しているのがボール追従なら、対象が画角のどこに居るか測る
        frameAcc += Time.fixedDeltaTime;
        if (frameAcc >= 0.25f)
        {
            frameAcc = 0f;
            var liveCam = director != null ? director.Live : Camera.main;
            var shown = liveCam;
            var mixc = liveCam != null ? liveCam.GetComponent<RandomMixCamera>() : null;
            if (mixc != null && mixc.Source != null) shown = mixc.Source;
            LotteryBall tgt = null;
            var rf = shown != null ? shown.GetComponent<RandomFollowCamera>() : null;
            if (rf != null) tgt = rf.Target;
            else if (shown != null)
            {
                var fc = shown.GetComponent<FollowCamera>();
                if (fc != null) tgt = fc.Target;
            }
            if (tgt != null && liveCam != null)
            {
                var vp = liveCam.WorldToViewportPoint(tgt.transform.position);
                bool on = vp.z > 0f && vp.x > 0.02f && vp.x < 0.98f && vp.y > 0.02f && vp.y < 0.98f;
                float off = Mathf.Max(Mathf.Abs(vp.x - 0.5f), Mathf.Abs(vp.y - 0.5f));
                var trb = tgt.GetComponent<Rigidbody>();
                bool onLift = trb != null && trb.isKinematic;
                if (onLift) { if (on) framedLift++; else offLift++; worstOffsetLift = Mathf.Max(worstOffsetLift, off); }
                else { if (on) framedFree++; else offFree++; worstOffsetFree = Mathf.Max(worstOffsetFree, off); }
            }
        }

        // デモ演出がいま映しているショットを定期撮影（＝実際の見え方の記録にもなる）
        if (shotInterval > 0f)
        {
            shotAcc += Time.fixedDeltaTime;
            if (shotAcc >= shotInterval)
            {
                shotAcc = 0f;
                Shot(director != null ? director.Live : Camera.main, "live");
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

        float fFree = framedFree + offFree, fLift = framedLift + offLift;
        // ponytail: string.Format の後ろの方の書式指定が環境依存で落ちたので素直に連結する
        sb.AppendLine(" \"followFraming\": {\"freeSamples\":" + (int)fFree
            + ",\"freeInFrame\":" + (framedFree / Mathf.Max(1f, fFree)).ToString("F3")
            + ",\"freeWorstOffset\":" + worstOffsetFree.ToString("F3")
            + ",\"liftSamples\":" + (int)fLift
            + ",\"liftInFrame\":" + (framedLift / Mathf.Max(1f, fLift)).ToString("F3")
            + ",\"liftWorstOffset\":" + worstOffsetLift.ToString("F3") + "},");
        sb.AppendLine($" \"escapes\": [{string.Join(",", escapes)}],");
        sb.AppendLine($" \"stuck\": [{string.Join(",", stucks)}],");
        sb.AppendLine($" \"stranded\": [{string.Join(",", strandeds)}],");
        sb.AppendLine($" \"derails\": [{string.Join(",", derails)}],");

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
        if (coverage != null) coverage.Dump();
        Debug.Log($"[Soak] done label={label} hits={totalHits} laps={totalLaps} escapes={escapes.Count} derails={derails.Count} stuck={stucks.Count} stranded={strandeds.Count} shots={shotNo}");
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
