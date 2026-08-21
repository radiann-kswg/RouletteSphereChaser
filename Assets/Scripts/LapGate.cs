using UnityEngine;

/// 周回チェックポイント。通過で1巡確定（pending→total）。回収部の1箇所に置く。
[RequireComponent(typeof(Collider))]
public class LapGate : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        var rb = other.attachedRigidbody;
        if (rb == null) return;
        var ball = rb.GetComponent<LotteryBall>();
        if (ball != null) ball.CommitLap();
    }
}
