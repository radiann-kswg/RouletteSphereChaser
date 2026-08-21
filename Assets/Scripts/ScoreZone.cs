using UnityEngine;

/// 抽選機構の入賞穴・通過点に置くトリガー。通過したボールの今巡ポイントに加算する。
[RequireComponent(typeof(Collider))]
public class ScoreZone : MonoBehaviour
{
    public int points = 10;

    void OnTriggerEnter(Collider other)
    {
        var rb = other.attachedRigidbody;
        if (rb == null) return;
        var ball = rb.GetComponent<LotteryBall>();
        if (ball == null) return;
        ball.pendingPoints += points;
        Debug.Log($"[Score] {name} +{points} -> {ball.name} (pend={ball.pendingPoints})");
    }
}
