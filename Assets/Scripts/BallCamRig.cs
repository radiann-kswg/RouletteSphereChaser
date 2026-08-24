using UnityEngine;

/// ボール追従カメラの共通の足回り。`FollowCamera` と `RandomFollowCamera` が使う。
///
/// **リフト搬送に追従できなかった原因は Lerp の定常遅れ**（User報告 2026-08-24）。
/// `Lerp(pos, want, k*dt)` は等速で動く相手に対して **v/k の遅れが残り続ける**ので、
/// 1.5m/s で上昇する球は毎フレーム画面外へ逃げていく。ここでは
///   ・臨界制動（`SmoothDamp`）でフレームレートに依らない追従にする
///   ・**相手の速度 × smoothTime を先読み**して定常誤差そのものを打ち消す
///   ・速いときは少し引いて画角に余裕を作る
/// の3点で対処する。リフト搬送は `MovePosition` なので `Rigidbody.linearVelocity` は0のまま。
/// 速度は**位置差分から実測**すること。
public class BallCamRig
{
    public float distance = 0.55f;
    public float height = 0.28f;
    /// 小さいほど機敏。0.18〜0.28 くらいが「滑らかで、かつ遅れない」
    public float smoothTime = 0.22f;
    /// 速度に応じてどれだけ引くか（0で引かない）
    public float pullBackPerSpeed = 0.12f;
    /// 速度推定の平滑化の速さ[1/s]。物理は50Hz・描画はそれ以上なので、生の位置差分はガタつく
    public float velocityFilter = 10f;

    Vector3 lastTargetPos, camVel, smoothVel;
    bool hasLast;

    public void Reset() { hasLast = false; camVel = Vector3.zero; smoothVel = Vector3.zero; }

    /// `rb` を渡せる場合は渡すこと。非キネマティックなら物理の速度をそのまま使えて推定ノイズが出ない
    public void Track(Transform cam, Vector3 p, float dt, Rigidbody rb = null)
    {
        if (dt <= 0f) return;

        // 速度の取り方（ガタつき対策・User報告 2026-08-24）:
        //  ・自由落下中は Rigidbody の速度をそのまま使う（既に滑らか）
        //  ・リフト搬送中は MovePosition なので速度が0のまま。位置差分で拾い、指数フィルタで均す
        Vector3 raw;
        if (rb != null && !rb.isKinematic) raw = rb.linearVelocity;
        else raw = hasLast ? (p - lastTargetPos) / dt : Vector3.zero;
        smoothVel = hasLast
            ? Vector3.Lerp(smoothVel, raw, 1f - Mathf.Exp(-velocityFilter * dt))
            : raw;
        Vector3 vel = smoothVel;
        lastTargetPos = p;
        hasLast = true;

        Vector3 back = cam.position - p;
        back.y = 0f;
        back = back.sqrMagnitude < 1e-4f ? Vector3.back : back.normalized;

        float d = distance * Mathf.Clamp(1f + vel.magnitude * pullBackPerSpeed, 1f, 1.8f);
        // 先読み分を足すことで、等速移動中も球が画角中央に留まる
        Vector3 want = p + back * d + Vector3.up * height + vel * smoothTime;

        cam.position = Vector3.SmoothDamp(cam.position, want, ref camVel, smoothTime, Mathf.Infinity, dt);
        // 向きも滑らかに。位置が臨界制動なので、こちらは軽く均すだけで十分中央に収まる
        var look = Quaternion.LookRotation(p - cam.position, Vector3.up);
        cam.rotation = Quaternion.Slerp(cam.rotation, look, 1f - Mathf.Exp(-14f * dt));
    }
}
