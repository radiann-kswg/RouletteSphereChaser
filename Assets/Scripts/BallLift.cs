using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// 回収部に落ちたボールをウェイポイント沿いに頂上へ運ぶリフト。循環の要。
[RequireComponent(typeof(Collider))]
public class BallLift : MonoBehaviour
{
    public Transform[] waypoints;
    public float speed = 0.6f; // m/s
    public float releaseJitter = 0f; // 解放時の水平ランダム速度[m/s]。分岐盤への投下方位を一様化する

    readonly List<Rigidbody> riders = new(); // 搬送中ボール台帳（凍結ボール回収用）

    void OnTriggerEnter(Collider other)
    {
        var rb = other.attachedRigidbody;
        if (rb == null || rb.isKinematic) return;
        if (rb.GetComponent<LotteryBall>() == null) return;
        StartCoroutine(Ride(rb));
    }

    IEnumerator Ride(Rigidbody rb)
    {
        rb.isKinematic = true;
        riders.Add(rb);
        foreach (var w in waypoints)
        {
            if (w == null) continue; // waypoint欠損でも凍結させず先へ進む
            while (rb != null && (rb.position - w.position).sqrMagnitude > 0.0001f)
            {
                rb.MovePosition(Vector3.MoveTowards(rb.position, w.position, speed * Time.fixedDeltaTime));
                yield return new WaitForFixedUpdate();
            }
        }
        if (rb != null) Release(rb);
        riders.Remove(rb);
    }

    void Release(Rigidbody rb)
    {
        rb.isKinematic = false;
        rb.linearVelocity = releaseJitter > 0f
            ? Quaternion.Euler(0, Random.Range(0f, 360f), 0) * new Vector3(releaseJitter, 0, 0)
            : Vector3.zero;
    }

    /// リフト無効化・破棄でコルーチンが止まっても、搬送中ボールをキネマティック凍結のまま残さない
    void OnDisable()
    {
        foreach (var rb in riders)
            if (rb != null) Release(rb);
        riders.Clear();
    }
}
