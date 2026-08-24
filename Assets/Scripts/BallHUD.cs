using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// 追従中ボールの情報HUD（番号/周回/獲得中/累計）。PenchantManufacture書体。
/// 全景時はパーク集計へ切替。Canvasは実行時に自前生成（ビルダーはフォントと参照の配線のみ）。
public class BallHUD : MonoBehaviour
{
    public TMP_FontAsset font;
    public FollowCamera followCam;

    /// デモ演出中に別カメラの映している球へHUDを合わせるための差し込み口（CameraDirectorが毎フレーム設定する）。
    /// null なら followCam の対象、それも null なら全景集計。
    [System.NonSerialized] public LotteryBall externalTarget;

    /// 抽選機の定点カメラを映している間に出す機構名。null 以外なら**ボール情報ではなく機構モード**になり、
    /// タイトル＝機構名／本文＝その機構の通過スクロールログ になる（User要望 2026-08-24）。
    [System.NonSerialized] public string mechTitle;
    /// 通過ログを絞り込む Park 直下グループ名（例 "TowerG_E"）
    [System.NonSerialized] public string[] mechGroups;

    public int logLines = 6;

    struct Pass { public string group, text; public float time; }
    readonly System.Collections.Generic.List<Pass> passes = new();

    void OnEnable() { ScoreZone.OnPassed += OnPassed; }
    void OnDisable() { ScoreZone.OnPassed -= OnPassed; }

    void OnPassed(ScoreZone z, LotteryBall b, int gained, int mult)
    {
        // Park/<グループ>/... の <グループ> で分類する
        var t = z.transform;
        string group = t.name;
        while (t.parent != null && t.parent.name != "Park") { t = t.parent; group = t.name; }

        string text = gained > 0
            ? $"BALL {b.number:00}   {z.name}   +{gained}" + (mult > 1 ? $"  (x{mult})" : "")
            : $"BALL {b.number:00}   {z.name}   NEXT x{mult}";
        passes.Add(new Pass { group = group, text = text, time = Time.time });
        if (passes.Count > 200) passes.RemoveRange(0, 100);   // ponytail: 単純なリングでよい
    }

    TextMeshProUGUI title, body;
    RectTransform panel;   // 明るい機構を映すと白文字が飛ぶので、TMPの裏に半透明の黒板を敷く

    void Awake()
    {
        var canvasGO = new GameObject("HUDCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // 先に敷く＝ヒエラルキー順で文字より奥に描かれる
        var panelGO = new GameObject("HUDPanel");
        panelGO.transform.SetParent(canvasGO.transform, false);
        var img = panelGO.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.7f);
        img.raycastTarget = false;
        panel = img.rectTransform;
        panel.anchorMin = panel.anchorMax = new Vector2(0, 1);
        panel.pivot = new Vector2(0, 1);
        panel.anchoredPosition = new Vector2(20, -18);

        title = MakeText(canvasGO.transform, new Vector2(36, -30), 64, new Color(1f, 0.85f, 0.30f));
        body = MakeText(canvasGO.transform, new Vector2(38, -108), 40, new Color(0.95f, 0.95f, 1f));
    }

    /// 文字の実サイズに合わせて黒板を伸縮させる（機構モードはログ行数で高さが変わる）
    void FitPanel()
    {
        if (panel == null) return;
        title.ForceMeshUpdate();
        body.ForceMeshUpdate();
        float w = Mathf.Max(title.preferredWidth, body.preferredWidth) + 40f;
        float h = title.preferredHeight + body.preferredHeight + 46f;
        panel.sizeDelta = new Vector2(Mathf.Clamp(w, 260f, 900f), Mathf.Clamp(h, 120f, 560f));
    }

    TextMeshProUGUI MakeText(Transform parent, Vector2 pos, float size, Color col)
    {
        var go = new GameObject("HUDText");
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.color = col;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.alignment = TextAlignmentOptions.TopLeft;
        t.lineSpacing = 8f;
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(800, 500);
        return t;
    }

    void LateUpdate()
    {
        // 抽選機モード: 機構名＋その機構での通過スクロールログ
        if (mechTitle != null)
        {
            title.text = mechTitle;
            var sb = new System.Text.StringBuilder();
            int shown = 0;
            for (int i = passes.Count - 1; i >= 0 && shown < logLines; i--)
            {
                if (mechGroups != null && System.Array.IndexOf(mechGroups, passes[i].group) < 0) continue;
                sb.AppendLine(passes[i].text);
                shown++;
            }
            body.text = shown > 0 ? sb.ToString().TrimEnd() : "...";
            FitPanel();
            return;
        }

        var b = externalTarget != null ? externalTarget : (followCam != null ? followCam.Target : null);
        if (b != null)
        {
            title.text = $"BALL {b.number:00}";
            var mult = b.nextMultiplier > 1 ? $"  x{b.nextMultiplier}" : "";
            body.text = $"LAP {b.laps}\nSCORE {b.pendingPoints}{mult}\nTOTAL {b.totalScore}";
        }
        else
        {
            // ponytail: 全景表示のみ毎フレーム集計（8〜32球なら無視できる負荷）
            int total = 0, laps = 0, n = 0;
            foreach (var lb in FindObjectsByType<LotteryBall>(FindObjectsSortMode.None))
            {
                total += lb.totalScore;
                laps += lb.laps;
                n++;
            }
            title.text = "OVERVIEW";
            body.text = $"BALLS {n}\nLAPS {laps}\nTOTAL {total}";
        }
        FitPanel();
    }
}
