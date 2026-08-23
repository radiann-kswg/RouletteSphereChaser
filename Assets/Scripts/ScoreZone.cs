using UnityEngine;

/// 抽選機構の入賞穴・通過点に置くトリガー。通過したボールの今巡ポイントに加算する。
[RequireComponent(typeof(Collider))]
public class ScoreZone : MonoBehaviour
{
    public int points = 10;

    /// 到達回数（フェーズ7の「点数 = C / P」再計算用。プレイ中のみ加算・保存しない）
    [System.NonSerialized] public int hits;

    void OnTriggerEnter(Collider other)
    {
        var rb = other.attachedRigidbody;
        if (rb == null) return;
        var ball = rb.GetComponent<LotteryBall>();
        if (ball == null) return;
        ball.pendingPoints += points;
        hits++;
        Debug.Log($"[Score] {name} +{points} -> {ball.name} (pend={ball.pendingPoints})");
    }
}
