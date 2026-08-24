using UnityEngine;

/// 抽選機構の入賞穴・通過点に置くトリガー。通過したボールの今巡ポイントに加算する。
/// `grantMultiplier > 0` のときは加点せず、**次の抽選で得る点数の倍率**を与える
/// （タワーD/Eの高得点チャレンジ段。User仕様 2026-08-24）。
[RequireComponent(typeof(Collider))]
public class ScoreZone : MonoBehaviour
{
    public int points = 10;

    /// >0 なら「加点しないで倍率を与える」モード。直後の通常抽選段が消費する。
    public int grantMultiplier = 0;

    /// 到達回数（フェーズ7の「点数 = C / P」再計算用。プレイ中のみ加算・保存しない）
    [System.NonSerialized] public int hits;

    void OnTriggerEnter(Collider other)
    {
        var rb = other.attachedRigidbody;
        if (rb == null) return;
        var ball = rb.GetComponent<LotteryBall>();
        if (ball == null) return;
        hits++;

        if (grantMultiplier > 0)
        {
            ball.nextMultiplier = grantMultiplier;
            Debug.Log($"[Mult] {name} x{grantMultiplier} -> {ball.name}");
            return;
        }

        int m = Mathf.Max(1, ball.nextMultiplier);
        int gained = points * m;
        ball.pendingPoints += gained;
        if (m > 1) Debug.Log($"[Score] {name} +{points}x{m}={gained} -> {ball.name} (pend={ball.pendingPoints})");
        else       Debug.Log($"[Score] {name} +{points} -> {ball.name} (pend={ball.pendingPoints})");
        ball.nextMultiplier = 1;   // 倍率は1回で消費
    }
}
