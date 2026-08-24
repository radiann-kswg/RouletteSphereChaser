using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// レースゲームのデモプレイ風にショットを切り替える演出ディレクタ。
/// 手持ちのショットは3種類:
///   ・メインの追従カメラ（`FollowCamera`。Tab/0 の手動操作はそのまま生きる）
///   ・ランダム追従カメラ（`RandomFollowCamera`。数十秒ごとに別のボールへ乗り換える）
///   ・抽選機ごとの定点カメラ（`Park/Cameras/Cam_*`。死角のある台は `OrbitCamera` で周回する）
///
/// デモ中は**同時に1台だけ**を有効にして Display 1 に出す。HUDは映っている球に追従させる。
/// 何も操作されない時間が `idleToDemo` を超えたら自動でデモへ入る（アトラクトモード）。
///   C … デモON/OFF   V … 次のショットへ   Tab/0 … 手動操作（デモを抜ける）
public class CameraDirector : MonoBehaviour
{
    public bool demoMode = true;
    /// 手動操作からこの秒数放置されたらデモへ戻る
    public float idleToDemo = 45f;

    Camera mainCam;
    BallHUD hud;
    /// Display 1 に出す全カメラ対応のミックスカメラ（抽選機4＋ボール4から自動で選ぶ）
    RandomMixCamera mix;
    Camera mixCam;
    /// 演出の実体側。ミックスが姿勢を借りるだけで、これ自体は画面に出さない
    readonly List<Camera> channels = new();
    readonly List<Camera> fixedCams = new();

    Camera live;
    float lastManual;

    /// 現在映しているカメラ（ソークのスクリーンショット用）
    public Camera Live => live != null ? live : mainCam;

    void Start()
    {
        mainCam = Camera.main;
        if (mainCam == null) { enabled = false; return; }
        hud = mainCam.GetComponent<BallHUD>();

        var park = GameObject.Find("Park");
        var cams = park != null ? park.transform.Find("Cameras") : null;
        if (cams != null)
            foreach (Transform t in cams)
            {
                var c = t.GetComponent<Camera>();
                if (c == null) continue;
                var m = t.GetComponent<RandomMixCamera>();
                if (m != null) { mix = m; mixCam = c; }
                else if (t.GetComponent<RandomFixedCamera>() != null || t.GetComponent<RandomFollowCamera>() != null) channels.Add(c);
                else fixedCams.Add(c);
            }

        Cut(mainCam);
        Debug.Log($"[Director] mix={(mix != null)} channels={channels.Count} fixed={fixedCams.Count} demo={demoMode}");
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.cKey.wasPressedThisFrame) { demoMode = !demoMode; Cut(demoMode ? mixCam : mainCam); }
            if (kb.vKey.wasPressedThisFrame && demoMode && mix != null) mix.Pick();
            // 手動でボールを選んだらデモを抜けてメインカメラへ戻す
            if (kb.tabKey.wasPressedThisFrame || kb.digit0Key.wasPressedThisFrame)
            {
                lastManual = Time.time;
                demoMode = false;
                Cut(mainCam);
            }
        }

        if (!demoMode)
        {
            if (idleToDemo > 0f && Time.time - lastManual > idleToDemo) { demoMode = true; Cut(mixCam); }
            return;
        }

        if (live != mixCam) Cut(mixCam);
        UpdateHud();
    }

    /// HUDの中身を「いま映しているもの」に合わせる。
    /// 抽選機チャンネル: 機構名＋その機構の通過スクロールログ／ボールチャンネル: 従来のボール情報
    void UpdateHud()
    {
        if (hud == null) return;
        // ミックス経由なので、いま実際に絵を作っているチャンネルまで辿る
        Camera shown = live;
        if (mix != null && live == mixCam && mix.Source != null) shown = mix.Source;

        var mech = shown != null ? shown.GetComponent<RandomFixedCamera>() : null;
        if (mech != null && mech.Source != null)
        {
            string key = CameraCoverage.KeyOf(mech.Source.name);
            hud.mechTitle = CameraCoverage.DisplayName.TryGetValue(key, out var dn) ? dn : key;
            hud.mechGroups = CameraCoverage.Assign.TryGetValue(key, out var gs) ? gs : null;
            hud.externalTarget = null;
            return;
        }
        hud.mechTitle = null;
        hud.mechGroups = null;
        var r = shown != null ? shown.GetComponent<RandomFollowCamera>() : null;
        hud.externalTarget = r != null ? r.Target : null;
    }

    void Cut(Camera c)
    {
        if (c == null) c = mainCam;
        live = c;
        mainCam.enabled = (c == mainCam);
        // 実体側の定点カメラとチャンネルは姿勢の供給元。画面に出すのはミックスかメインだけ
        foreach (var f in fixedCams) f.enabled = false;
        foreach (var ch in channels) ch.enabled = false;
        if (mixCam != null) mixCam.enabled = (c == mixCam);
        if (hud != null && c == mainCam) { hud.externalTarget = null; hud.mechTitle = null; }
    }
}
