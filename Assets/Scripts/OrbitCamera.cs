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

    /// 方位の振り幅[deg]。**0 なら360°周回**、>0 なら配置時の方位を中心に ±この角度で往復する。
    /// **平らな盤面の機構（パチンコ盤など）は正面からしか中身が見えない**ので、
    /// 回してしまうと一周のほとんどが裏側＝死角になる（実測: 周回のままだと死角0.88）。
    /// 盤に貼り付いた機構はこれを使って正面寄りに振ること。
    public float azimuthAmplitude = 0f;

    float radius, height, angle, baseAngle;

    void Start()
    {
        var d = transform.position - pivot;
        height = d.y;
        d.y = 0f;
        radius = Mathf.Max(0.3f, d.magnitude);
        angle = baseAngle = Mathf.Atan2(d.z, d.x) * Mathf.Rad2Deg;
    }

    void LateUpdate()
    {
        if (degreesPerSecond == 0f && elevationAmplitude == 0f && azimuthAmplitude == 0f) return;
        if (azimuthAmplitude > 0f)
        {
            // 振り子。周期は「振り幅を degreesPerSecond で往復する時間」に合わせるので、
            // 速度指定の意味が360°周回と揃う（dpsを上げれば速く振れる）
            float period = Mathf.Max(0.1f, 4f * azimuthAmplitude / Mathf.Max(0.1f, Mathf.Abs(degreesPerSecond)));
            angle = baseAngle + azimuthAmplitude * Mathf.Sin(Time.time / period * 2f * Mathf.PI);
        }
        else
        {
            angle += degreesPerSecond * Time.deltaTime;
        }
        float y = height + (elevationAmplitude > 0f
            ? elevationAmplitude * Mathf.Sin(Time.time / Mathf.Max(0.1f, elevationPeriod) * 2f * Mathf.PI)
            : 0f);
        Pose(angle, y);
    }

    void Pose(float deg, float y)
    {
        float r = deg * Mathf.Deg2Rad;
        transform.position = pivot + new Vector3(Mathf.Cos(r) * radius, y, Mathf.Sin(r) * radius);
        transform.LookAt(pivot + lookOffset);
    }

    /// 撮影用: 配置時の方位・高さに戻す。**README用スクショの構図を毎回そろえる**ためのもの
    /// （周回中の任意フレームで撮ると画角がぶれて、前後の差分が読めなくなる）。
    public void SnapToBase()
    {
        if (radius <= 0f) Start();
        Pose(baseAngle, height);
    }
}
