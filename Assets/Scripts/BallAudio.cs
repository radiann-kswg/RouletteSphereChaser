using UnityEngine;

/// 球1個ぶんの物理音。ParkAudio が実行時に後付けする（プレハブには入れない）。
/// 転がり＝接地中の速さで音量とピッチ、衝突＝相対速度がしきい値を超えたときだけ「コトッ」。
public class BallAudio : MonoBehaviour
{
    ParkAudio k;   // ノブは ParkAudio に1か所（全球共通）
    AudioSource rollSrc, hitSrc;
    AudioClip[] hits;
    Rigidbody rb;
    float lastTouch = -1f, lastHit = -1f;

    public void Init(ParkAudio knobs, AudioClip roll, AudioClip[] hitClips)
    {
        k = knobs;
        rb = GetComponent<Rigidbody>();
        hits = hitClips;
        rollSrc = ParkAudio.Make3D(gameObject, roll, true);
        rollSrc.volume = 0f;
        rollSrc.time = Random.value * roll.length;   // 36球のループ位相をばらす（揃うとうなる）
        rollSrc.Play();
        hitSrc = ParkAudio.Make3D(gameObject, null, false);
        hitSrc.priority = 180;
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
        hitSrc.pitch = Random.Range(0.92f, 1.08f);
        hitSrc.PlayOneShot(hits[Random.Range(0, hits.Length)], k.hitVolume * Mathf.InverseLerp(k.hitMinSpeed, k.hitFullSpeed, v));
    }

    void Update()
    {
        if (rollSrc == null) return;
        // リフト搬送中（キネマティック）と空中は無音
        bool rolling = rb != null && !rb.isKinematic && Time.time - lastTouch < 0.1f;
        float speed = rolling ? rb.linearVelocity.magnitude : 0f;
        float target = k.rollVolume * Mathf.Clamp01(speed / k.rollFullSpeed);
        rollSrc.volume = Mathf.MoveTowards(rollSrc.volume, target, Time.deltaTime * 2f);   // プツッと切らない
        rollSrc.pitch = 0.85f + 0.35f * Mathf.Clamp01(speed / k.rollFullSpeed);
    }
}
