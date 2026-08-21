using UnityEngine;

/// 角度A⇔Bを正弦波で往復（シーソー・開閉ゲート）。キネマティックRigidbody必須。
[RequireComponent(typeof(Rigidbody))]
public class Oscillator : MonoBehaviour
{
    public Vector3 axis = Vector3.right;
    public float angleA = -20f;
    public float angleB = 20f;
    public float period = 3f;
    public float phase; // 複数ゲートの位相ずらし用

    Rigidbody rb;
    Quaternion baseRot;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        baseRot = rb.rotation;
    }

    void FixedUpdate()
    {
        float t = 0.5f + 0.5f * Mathf.Sin((Time.time / period + phase) * 2f * Mathf.PI);
        float ang = Mathf.Lerp(angleA, angleB, t);
        rb.MoveRotation(baseRot * Quaternion.AngleAxis(ang, axis));
    }
}
