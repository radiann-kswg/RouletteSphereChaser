using System.Linq;
using UnityEngine;

/// Tab(RB)/Shift+Tab(LB)でボールを順に追従、0(LT)で全景に戻る観賞用カメラ。バインドは ParkInput。
/// 無指示のときは1番ボールへ自動追従（User方針 2026-08-22）。0で全景にするとオート解除、Tabで復帰。
public class FollowCamera : MonoBehaviour
{
    public float distance = 0.5f;
    public float height = 0.25f;
    public float smooth = 4f;
    /// 追従の応答（小さいほど機敏）。速度先読みで遅れは消えるので、滑らかさ寄りの値でよい
    public float smoothTime = 0.22f;

    Vector3 overviewPos;
    Quaternion overviewRot;
    LotteryBall target;
    int index = -1;
    bool manualOverview;
    readonly BallCamRig rig = new();

    /// HUD等が現在の追従対象を参照するためのアクセサ
    public LotteryBall Target => target;

    void Start()
    {
        overviewPos = transform.position;
        overviewRot = transform.rotation;
    }

    void Update()
    {
        int step = ParkInput.BallStep;
        if (step != 0)
        {
            var balls = FindObjectsByType<LotteryBall>().OrderBy(b => b.number).ToArray();
            if (balls.Length > 0)
            {
                index = ((index < 0 && step < 0 ? 0 : index) + step + balls.Length) % balls.Length;
                target = balls[index];
                manualOverview = false;
            }
        }
        if (ParkInput.Overview)
        {
            target = null;
            index = -1;
            manualOverview = true;
        }
        // デフォルト: 1番ボールに追従（スポーン待ちの間は毎フレーム探す）
        if (target == null && !manualOverview)
        {
            foreach (var b in FindObjectsByType<LotteryBall>())
                if (b.number == 1) { target = b; break; }
        }
    }

    void LateUpdate()
    {
        if (target != null)
        {
            // 定常遅れを残さない追従（リフト上昇に追いつくため。詳細は BallCamRig）
            rig.distance = distance;
            rig.height = height;
            rig.smoothTime = smoothTime;
            rig.Track(transform, target.transform.position, Time.deltaTime, target.GetComponent<Rigidbody>());
            return;
        }
        rig.Reset();
        transform.position = Vector3.Lerp(transform.position, overviewPos, smooth * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, overviewRot, smooth * Time.deltaTime);
    }
}
