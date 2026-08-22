using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// 追従中ボールの情報HUD（番号/周回/獲得中/累計）。PenchantManufacture書体。
/// 全景時はパーク集計へ切替。Canvasは実行時に自前生成（ビルダーはフォントと参照の配線のみ）。
public class BallHUD : MonoBehaviour
{
    public TMP_FontAsset font;
    public FollowCamera followCam;

    TextMeshProUGUI title, body;

    void Awake()
    {
        var canvasGO = new GameObject("HUDCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        title = MakeText(canvasGO.transform, new Vector2(36, -30), 64, new Color(1f, 0.85f, 0.30f));
        body = MakeText(canvasGO.transform, new Vector2(38, -108), 40, new Color(0.95f, 0.95f, 1f));
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
        var b = followCam != null ? followCam.Target : null;
        if (b != null)
        {
            title.text = $"BALL {b.number:00}";
            body.text = $"LAP {b.laps}\nSCORE {b.pendingPoints}\nTOTAL {b.totalScore}";
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
    }
}
