using UnityEngine;

/// 定点カメラを機構のまわりに等速で周回させて死角を消す。
/// 半径・高さ・開始角は**配置時のカメラ位置から自動で取る**ので、ビルダー側は pivot と速度を与えるだけでよい。
/// 死角が無いカメラには付けない（`CameraCoverage` の実測で判断する）。
public class OrbitCamera : MonoBehaviour
{
    public Vector3 pivot;
    /// 0 なら回さない（配置のまま固定）。`CameraCoverage` の実測で死角が無い台は0にする
    public float degreesPerSecond = 10f;
    /// 見上げ／見下ろしの目標点を pivot から少しずらしたいとき用
    public Vector3 lookOffset = Vector3.zero;
    /// このカメラが「映すべき」範囲の半径。死角の実測（CameraCoverage）で母数を決めるのに使う
    public float focusRadius = 2f;

    /// 上下の振り幅[m]と周期[s]。方位を回すだけでは**すり鉢やトラフの中は永久に見えない**ので、
    /// 見下ろし角も一緒に振って内側を舐める（実測: 方位だけだと死角70〜90%が残った）
    public float elevationAmplitude = 0f;
    public float elevationPeriod = 17f;

    float radius, height, angle;

    void Start()
    {
        var d = transform.position - pivot;
        height = d.y;
        d.y = 0f;
        radius = Mathf.Max(0.3f, d.magnitude);
        angle = Mathf.Atan2(d.z, d.x) * Mathf.Rad2Deg;
    }

    void LateUpdate()
    {
        if (degreesPerSecond == 0f && elevationAmplitude == 0f) return;
        angle += degreesPerSecond * Time.deltaTime;
        float r = angle * Mathf.Deg2Rad;
        float y = height + (elevationAmplitude > 0f
            ? elevationAmplitude * Mathf.Sin(Time.time / Mathf.Max(0.1f, elevationPeriod) * 2f * Mathf.PI)
            : 0f);
        transform.position = pivot + new Vector3(Mathf.Cos(r) * radius, y, Mathf.Sin(r) * radius);
        transform.LookAt(pivot + lookOffset);
    }
}
