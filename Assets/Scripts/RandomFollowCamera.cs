using UnityEngine;

/// 一定時間ごとにランダムなボールへ乗り換える追従カメラ。
/// レースゲームのデモプレイのように「勝手に見どころを見せ続ける」ための演出用で、
/// `CameraDirector` が定点カメラと混ぜて切り替える。ソーク検証では脱線した球を拾う目にもなる。
public class RandomFollowCamera : MonoBehaviour
{
    public float minHold = 20f, maxHold = 40f;
    public float distance = 0.55f, height = 0.28f, smooth = 3.5f;
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
        var balls = Object.FindObjectsByType<LotteryBall>(FindObjectsSortMode.None);
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
        target = pick;
        nextSwitch = Time.time + Random.Range(minHold, maxHold);
    }

    void LateUpdate()
    {
        if (target == null) return;
        Vector3 p = target.transform.position;
        Vector3 back = transform.position - p;
        back.y = 0f;
        back = back.sqrMagnitude < 0.001f ? Vector3.back : back.normalized;
        Vector3 wantPos = p + back * distance + Vector3.up * height;
        transform.position = Vector3.Lerp(transform.position, wantPos, smooth * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(p - wantPos), smooth * Time.deltaTime);
    }
}
