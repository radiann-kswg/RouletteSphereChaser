using System.Collections;
using UnityEngine;

/// 回収部に落ちたボールをウェイポイント沿いに頂上へ運ぶリフト。循環の要。
[RequireComponent(typeof(Collider))]
public class BallLift : MonoBehaviour
{
    public Transform[] waypoints;
    public float speed = 0.6f; // m/s

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
        foreach (var w in waypoints)
        {
            while ((rb.position - w.position).sqrMagnitude > 0.0001f)
            {
                rb.MovePosition(Vector3.MoveTowards(rb.position, w.position, speed * Time.fixedDeltaTime));
                yield return new WaitForFixedUpdate();
            }
        }
        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
    }
}
