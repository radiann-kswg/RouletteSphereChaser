using UnityEngine;
using UnityEngine.InputSystem;

/// Tabでボールを順に追従、0キーで全景に戻る観賞用カメラ。
/// 無指示のときは1番ボールへ自動追従（User方針 2026-08-22）。0で全景にするとオート解除、Tabで復帰。
public class FollowCamera : MonoBehaviour
{
    public float distance = 0.5f;
    public float height = 0.25f;
    public float smooth = 4f;

    Vector3 overviewPos;
    Quaternion overviewRot;
    LotteryBall target;
    int index = -1;
    bool manualOverview;

    /// HUD等が現在の追従対象を参照するためのアクセサ
    public LotteryBall Target => target;

    void Start()
    {
        overviewPos = transform.position;
        overviewRot = transform.rotation;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.tabKey.wasPressedThisFrame)
            {
                var balls = FindObjectsByType<LotteryBall>(FindObjectsSortMode.InstanceID);
                if (balls.Length > 0)
                {
                    index = (index + 1) % balls.Length;
                    target = balls[index];
                    manualOverview = false;
                }
            }
            if (kb.digit0Key.wasPressedThisFrame)
            {
                target = null;
                index = -1;
                manualOverview = true;
            }
        }
        // デフォルト: 1番ボールに追従（スポーン待ちの間は毎フレーム探す）
        if (target == null && !manualOverview)
        {
            foreach (var b in FindObjectsByType<LotteryBall>(FindObjectsSortMode.None))
                if (b.number == 1) { target = b; break; }
        }
    }

    void LateUpdate()
    {
        Vector3 wantPos;
        Quaternion wantRot;
        if (target != null)
        {
            Vector3 p = target.transform.position;
            // ボールから見て外周側に引いた位置（水平方向は現在位置から補間で滑らかに回り込む）
            Vector3 back = (transform.position - p);
            back.y = 0f;
            back = back.sqrMagnitude < 0.001f ? Vector3.back : back.normalized;
            wantPos = p + back * distance + Vector3.up * height;
            wantRot = Quaternion.LookRotation(p - wantPos);
        }
        else
        {
            wantPos = overviewPos;
            wantRot = overviewRot;
        }
        transform.position = Vector3.Lerp(transform.position, wantPos, smooth * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, wantRot, smooth * Time.deltaTime);
    }
}
