using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// `Tools > Capture Showcase Shots` … README 用のスクリーンショットを `Docs/screenshots/` に書き出す。
///
/// **見た目を変えたら必ず撮り直す**（配色・ライティング・透過シェル・カメラ配置を触ったとき）。
/// 手で撮ると画角がぶれて差分が読めなくなるので、構図はここに固定してある。
/// プレイ中でも実行できるが、球が入った絵が欲しいなら **`Tools > Run Soak` の実行中に**叩くこと。
public static class ShowcaseCapture
{
    const string OutDir = "Docs/screenshots";
    const int W = 1600, H = 900;

    /// 撮る定点カメラ（`Cam_` を除いた名前）-> 出力ファイル名
    static readonly (string cam, string file)[] Shots =
    {
        ("A_Overview", "tower-a-overview"),
        ("A_GrandRoulette", "grand-roulette"),
        ("B_Pachinko_E", "pachinko"),
        ("G_Numa_E", "numa-kuruun"),
        ("H_Garapon", "garapon"),
        ("E_PocketDisc_N", "pocket-disc"),
    };

    [MenuItem("Tools/Capture Showcase Shots")]
    public static void Capture()
    {
        Directory.CreateDirectory(OutDir);

        // 1) パーク全景。**構図は固定値**（毎回同じ位置から撮らないと差分が比較できない）
        var go = new GameObject("__showcaseCam");
        var cam = go.AddComponent<Camera>();
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 200f;
        cam.clearFlags = CameraClearFlags.Skybox;
        Shoot(cam, "park-wide", new Vector3(18f, 13f, 16f), new Vector3(0f, 6.5f, 0f), 42f);
        Shoot(cam, "park-front", new Vector3(0f, 9.5f, -21f), new Vector3(0f, 7f, 0f), 42f);
        Object.DestroyImmediate(go);

        // 2) 抽選機ごとの定点カメラ。実際にデモで映る絵をそのまま出す
        var cams = Object.FindObjectsByType<Camera>();
        int n = 2;
        foreach (var (name, file) in Shots)
        {
            var c = cams.FirstOrDefault(x => x.name == "Cam_" + name);
            if (c == null) { Debug.LogWarning("[Showcase] カメラが無い: Cam_" + name); continue; }
            // 周回・振り子の途中で撮ると毎回画角が変わる。配置時の姿勢に戻してから撮る
            var orb = c.GetComponent<OrbitCamera>();
            if (orb != null) orb.SnapToBase();
            Render(c, file);
            n++;
        }
        AssetDatabase.Refresh();
        Debug.Log($"[Showcase] {n} 枚を {OutDir}/ に書き出した");
    }

    static void Shoot(Camera cam, string file, Vector3 pos, Vector3 look, float fov)
    {
        cam.transform.position = pos;
        cam.transform.rotation = Quaternion.LookRotation((look - pos).normalized, Vector3.up);
        cam.fieldOfView = fov;
        Render(cam, file);
    }

    static void Render(Camera cam, string file)
    {
        var prev = cam.targetTexture;
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        cam.targetTexture = prev;
        File.WriteAllBytes(Path.Combine(OutDir, file + ".png"), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        rt.Release();
        Object.DestroyImmediate(rt);
    }
}
