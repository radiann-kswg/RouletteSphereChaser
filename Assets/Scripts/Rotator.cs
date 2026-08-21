using UnityEngine;

/// 定速回転（風車・回転皿など）。キネマティックRigidbody必須—ボールを正しく弾くため。
[RequireComponent(typeof(Rigidbody))]
public class Rotator : MonoBehaviour
{
    public Vector3 axis = Vector3.up;
    public float degreesPerSecond = 60f;

    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
    }

    void FixedUpdate()
    {
        rb.MoveRotation(rb.rotation * Quaternion.AngleAxis(degreesPerSecond * Time.fixedDeltaTime, axis));
    }
}
