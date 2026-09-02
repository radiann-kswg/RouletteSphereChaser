using UnityEngine;

/// 一定時間ごとにランダムなボールへ乗り換える追従カメラ。
/// レースゲームのデモプレイのように「勝手に見どころを見せ続ける」ための演出用で、
/// `CameraDirector` が定点カメラと混ぜて切り替える。ソーク検証では脱線した球を拾う目にもなる。
public class RandomFollowCamera : MonoBehaviour
{
    public float minHold = 20f, maxHold = 40f;
    public float distance = 0.55f, height = 0.28f;
    /// 追従の応答（小さいほど機敏）。速度先読みで遅れは消えるので、滑らかさ寄りの値でよい
    public float smoothTime = 0.22f;
    /// 起動直後に全台がいっせいに切り替わらないよう、台ごとに初回だけずらす
    public float startOffset = 0f;

    LotteryBall target;
    float nextSwitch;

    public LotteryBall Target => target;

    void Start() { nextSwitch = Time.time + startOffset; }

    void Update()
    {
        if (target == null || Time.time >= nextSwitch) Pick();
    }

    void Pick()
    {
        var balls = Object.FindObjectsByType<LotteryBall>();
        if (balls.Length == 0) { nextSwitch = Time.time + 1f; return; }

        // 搬送中（キネマティック＝リフトで運ばれている）の球は絵が動かないので、8回まで引き直す
        LotteryBall pick = null;
        for (int i = 0; i < 8; i++)
        {
            var b = balls[Random.Range(0, balls.Length)];
            var rb = b.GetComponent<Rigidbody>();
            if (rb == null || !rb.isKinematic) { pick = b; break; }
            pick = pick ?? b;
        }
        if (pick != target)
        {
            rig.Reset();                    // 乗り換え直後の速度推定を汚さない
            SnapBehind(pick);               // パーク横断のホイップパンを避けて、その場でカットする
        }
        target = pick;
        nextSwitch = Time.time + Random.Range(minHold, maxHold);
    }

    /// 乗り換え先の後方へ瞬間移動する。ショットの切り替わりは「カット」であって「パン」ではない
    void SnapBehind(LotteryBall b)
    {
        if (b == null) return;
        Vector3 p = b.transform.position;
        Vector3 back = Vector3.back;
        var rb = b.GetComponent<Rigidbody>();
        if (rb != null && rb.linearVelocity.sqrMagnitude > 0.01f)
        {
            var v = rb.linearVelocity; v.y = 0f;
            if (v.sqrMagnitude > 0.01f) back = -v.normalized;   // 進行方向の後ろから見る
        }
        transform.position = p + back * distance + Vector3.up * height;
        transform.rotation = Quaternion.LookRotation(p - transform.position, Vector3.up);
    }

    readonly BallCamRig rig = new();

    void LateUpdate()
    {
        if (target == null) return;
        rig.distance = distance;
        rig.height = height;
        rig.smoothTime = smoothTime;
        rig.Track(transform, target.transform.position, Time.deltaTime, target.GetComponent<Rigidbody>());
    }
}
