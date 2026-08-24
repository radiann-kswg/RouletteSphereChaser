using System.Collections.Generic;
using UnityEngine;

/// 抽選機の定点カメラを数十秒ごとに乗り換える「機構チャンネル」。
/// ボール追従の `RandomFollowCamera` と対になる演出ソースで、
/// 台ごとに別の抽選機を担当するので同時に複数の機構を追える。
///
/// 実体は `Park/Cameras/Cam_*`（`OrbitCamera` を持つ定点カメラ）の姿勢をそのまま写すだけ。
/// 死角つぶしのオービットも自動でついてくる。
public class RandomFixedCamera : MonoBehaviour
{
    public float minHold = 20f, maxHold = 40f;
    public float startOffset = 0f;

    readonly List<Camera> sources = new();
    Camera src;
    Camera self;
    float nextSwitch;

    /// いま映している定点カメラ（HUDが機構名と通過ログを引くのに使う）
    public Camera Source => src;

    void Start()
    {
        self = GetComponent<Camera>();
        var park = GameObject.Find("Park");
        var cams = park != null ? park.transform.Find("Cameras") : null;
        if (cams != null)
            foreach (Transform t in cams)
            {
                var c = t.GetComponent<Camera>();
                // 抽選機の定点カメラ＝OrbitCamera を持つもの。ローミング系は除く
                if (c != null && t.GetComponent<OrbitCamera>() != null) sources.Add(c);
            }
        nextSwitch = Time.time + startOffset;
    }

    void Update()
    {
        if (src == null || Time.time >= nextSwitch) Pick();
    }

    void Pick()
    {
        if (sources.Count == 0) { nextSwitch = Time.time + 1f; return; }
        // 球が映っている機構を優先（誰も居ない機構を延々映さない）。8回引いて駄目なら諦める
        var balls = Object.FindObjectsByType<LotteryBall>(FindObjectsSortMode.None);
        Camera pick = sources[Random.Range(0, sources.Count)];
        for (int i = 0; i < 8; i++)
        {
            var c = sources[Random.Range(0, sources.Count)];
            var planes = GeometryUtility.CalculateFrustumPlanes(c);
            foreach (var b in balls)
            {
                bool inside = true;
                foreach (var pl in planes)
                    if (pl.GetDistanceToPoint(b.transform.position) < 0f) { inside = false; break; }
                if (inside) { pick = c; i = 99; break; }
            }
        }
        src = pick;
        nextSwitch = Time.time + Random.Range(minHold, maxHold);
    }

    void LateUpdate()
    {
        if (src == null) return;
        transform.SetPositionAndRotation(src.transform.position, src.transform.rotation);
        if (self != null) self.fieldOfView = src.fieldOfView;
    }
}
