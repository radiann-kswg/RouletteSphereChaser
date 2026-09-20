using UnityEngine;

/// 球1個ぶんの物理音。ParkAudio が実行時に後付けする（プレハブには入れない）。
/// 衝突＝相対速度がしきい値を超えたときだけ「コトッ」。転がり＝同じ「コトッ」をごく小さく、進んだ距離に応じて刻む。
/// 連続ノイズの転がりループは使わない（User 判断 2026-09-20: 衝突音と重なると主張が強い。刻みだけのほうが自然でおとなしい）。
public class BallAudio : MonoBehaviour
{
    ParkAudio k;   // ノブは ParkAudio に1か所（全球共通）
    AudioSource hitSrc;
    float travelled;
    AudioClip[] hits;
    Rigidbody rb;
    float lastTouch = -1f, lastHit = -1f;

    public void Init(ParkAudio knobs, AudioClip[] hitClips)
    {
        k = knobs;
        rb = GetComponent<Rigidbody>();
        hits = hitClips;
        hitSrc = ParkAudio.Make3D(gameObject, null, false);
        travelled = Random.value * k.rollTickDistance;   // 36球の刻みを揃えない
    }

    void Tock(float volume)
    {
        hitSrc.pitch = Random.Range(0.92f, 1.08f);
        hitSrc.PlayOneShot(hits[Random.Range(0, hits.Length)], volume);
    }

    void OnCollisionStay(Collision c) { lastTouch = Time.time; }

    void OnCollisionEnter(Collision c)
    {
        lastTouch = Time.time;
        if (hitSrc == null || hits == null || hits.Length == 0 || Time.time - lastHit < 0.06f) return;
        // 球どうしは両方に届くので、片方だけが鳴らす
        var other = c.rigidbody != null ? c.rigidbody.GetComponent<BallAudio>() : null;
        if (other != null && other.GetHashCode() < GetHashCode()) return;
        float v = c.relativeVelocity.magnitude;
        if (v < k.hitMinSpeed) return;
        lastHit = Time.time;
        travelled = 0f;   // 当たった直後に刻みを重ねない
        Tock(k.hitVolume * Mathf.InverseLerp(k.hitMinSpeed, k.hitFullSpeed, v));
    }

    void Update()
    {
        if (hitSrc == null || hits == null || hits.Length == 0) return;
        // リフト搬送中（キネマティック）と空中は無音
        if (rb == null || rb.isKinematic || Time.time - lastTouch > 0.1f) return;
        float speed = rb.linearVelocity.magnitude;
        if (speed < k.rollMinSpeed) return;
        travelled += speed * Time.deltaTime;
        if (travelled < k.rollTickDistance) return;
        travelled = Random.Range(0f, k.rollTickDistance * 0.4f);   // 等間隔だと機械的に聞こえるので揺らす
        Tock(k.rollVolume * Mathf.Clamp01(speed / k.rollFullSpeed));
    }
}
