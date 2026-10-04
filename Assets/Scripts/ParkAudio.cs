using System.Collections.Generic;
using UnityEngine;

/// 観賞アプリの音（美化その4-2）。シーンにもプレハブにも配線しない＝ParkBuilder の作り直しや下流の球差し替えに影響されない。
///   ・物理音: 球ごとに BallAudio（衝突の「コトッ」＋転がりはその小さな刻み）を後付け、リフトに低いモーター音
///   ・BGM: Resources/BGM_Custom（git 管轄外の差し替えスロット）が空なら Resources/BGM（PD 曲の自前オルゴール版）を順繰り
///   ・M / パッド Y: All → SFX only → Mute（PlayerPrefs に保存）
/// 方針は「静かな部屋でずっと流しておけるか」。既定値は小さめ。音色は Docs/gen_sfx.py / gen_bgm.py、音量はここのノブ。
public class ParkAudio : MonoBehaviour
{
    public enum Mode { All, SfxOnly, Mute }

    [Range(0, 1)] public float master = 0.65f;
    [Range(0, 1)] public float bgmVolume = 0.15f;
    [Range(0, 1)] public float liftVolume = 0.10f;
    public float bgmGap = 6f;   // 曲間の無音[s]
    // 既定値は User が Play 中に耳で合わせた値（2026-09-20）。変えるときも耳で合わせてから書き戻す。
    // 球の音のノブ。実物の音は測れないので耳で合わせる（Play 中に Hierarchy の ParkAudio を Inspector で動かし、決まった値をこの既定値へ）
    [Range(0, 1)] public float rollVolume = 0.035f;  // 転がりの刻み（衝突と同じ「コトッ」）の最大音量。衝突より十分小さく
    public float rollFullSpeed = 2.0f;               // この速さ[m/s]で最大音量
    public float rollMinSpeed = 0.25f;               // これ未満（渋滞待ち・ほぼ静止）は刻まない
    public float rollTickDistance = 0.30f;           // 何m転がるごとに1回刻むか（球径0.1＝約1回転）
    [Range(0, 1)] public float hitVolume = 0.35f;
    public float hitMinSpeed = 0.35f;                // これ未満の接触は鳴らさない（レール上の細かい跳ねを拾わない）
    public float hitFullSpeed = 4.0f;

    public static Mode Current { get; private set; }

    AudioSource bgm;
    AudioClip[] playlist;
    int track = -1;
    float nextTrackAt, nextScan;
    AudioClip[] hits;
    readonly HashSet<LotteryBall> wired = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindAnyObjectByType<CameraDirector>() == null) return;   // パーク以外のシーンでは何もしない
        new GameObject("ParkAudio").AddComponent<ParkAudio>();
    }

    void Awake()
    {
        Current = (Mode)PlayerPrefs.GetInt("ParkAudio.Mode", 0);

        // 耳は「いま映しているカメラ」に置く（メインカメラはデモ中も1番ボールを追っていて、絵と音がずれる）
        foreach (var l in FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) l.enabled = false;
        gameObject.AddComponent<AudioListener>();

        hits = Resources.LoadAll<AudioClip>("SFX/Hits");
        var lift = Resources.Load<AudioClip>("SFX/Lift_Loop");
        foreach (var bl in FindObjectsByType<BallLift>(FindObjectsSortMode.None))
        {
            var s = Make3D(bl.gameObject, lift, true);
            s.volume = liftVolume;
            s.maxDistance = 60f;
            s.Play();
        }

        playlist = Resources.LoadAll<AudioClip>("BGM_Custom");
        if (playlist.Length == 0) playlist = Resources.LoadAll<AudioClip>("BGM");
        bgm = gameObject.AddComponent<AudioSource>();
        bgm.spatialBlend = 0f;
        bgm.priority = 0;
        nextTrackAt = Time.time + 4f;
        Apply();
    }

    public static AudioSource Make3D(GameObject go, AudioClip clip, bool loop)
    {
        var s = go.AddComponent<AudioSource>();
        s.clip = clip;
        s.loop = loop;
        s.playOnAwake = false;
        s.spatialBlend = 1f;
        s.dopplerLevel = 0f;
        s.rolloffMode = AudioRolloffMode.Logarithmic;   // 全景（20m級）でも遠くのざわめきとして残す
        s.minDistance = 1f;
        s.maxDistance = 40f;
        s.priority = 200;   // 同時発音の上限を超えたら遠い球から間引かれる。BGM(0) は落とさない
        return s;
    }

    void Apply()
    {
        AudioListener.volume = Current == Mode.Mute ? 0f : master;
        if (bgm != null) bgm.mute = Current != Mode.All;
    }

    void Update()
    {
        if (ParkInput.Audio)
        {
            Current = (Mode)(((int)Current + 1) % 3);
            PlayerPrefs.SetInt("ParkAudio.Mode", (int)Current);
            Apply();
        }

        if (playlist.Length > 0 && !bgm.isPlaying && Time.time >= nextTrackAt)
        {
            track = (track + 1) % playlist.Length;
            bgm.clip = playlist[track];
            bgm.Play();
        }
        else if (bgm.isPlaying) nextTrackAt = Time.time + bgmGap;
        bgm.volume = bgmVolume;

        // ponytail: 1秒ごとの全探索で新しい球に BallAudio を付ける（球は数十個・生成は起動直後だけ）。
        // 球の生成元が RSC と下流で別なので、生成側にフックを足すよりこちらが疎結合
        if (Time.time >= nextScan && hits.Length > 0)
        {
            nextScan = Time.time + 1f;
            foreach (var b in FindObjectsByType<LotteryBall>(FindObjectsSortMode.None))
                if (wired.Add(b)) b.gameObject.AddComponent<BallAudio>().Init(this, hits);
        }
    }

    void LateUpdate()
    {
        var cam = CameraDirector.Active;
        if (cam != null) transform.SetPositionAndRotation(cam.transform.position, cam.transform.rotation);
    }
}
