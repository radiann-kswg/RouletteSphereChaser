using UnityEditor;
using UnityEngine;

/// Tools > Run Soak … シーンに SoakRecorder を差してプレイモードに入るだけの起動係。
/// SoakRecorder が duration 秒後に Docs/soak_result.json を書いてプレイを抜ける。
public static class SoakRunner
{
    const string LabelKey = "Soak.Label";
    const string OutKey = "Soak.Out";
    const string DurKey = "Soak.Duration";

    [MenuItem("Tools/Run Soak (36 balls)")]
    public static void Run()
    {
        var old = GameObject.Find("SoakRecorder");
        if (old != null) Object.DestroyImmediate(old);

        var go = new GameObject("SoakRecorder");
        var rec = go.AddComponent<SoakRecorder>();
        rec.label = EditorPrefs.GetString(LabelKey, "run");
        rec.outPath = EditorPrefs.GetString(OutKey, "Docs/soak_result.json");
        rec.duration = EditorPrefs.GetFloat(DurKey, 180f);

        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        EditorApplication.isPlaying = true;
    }

    /// プレイを抜けた後の後始末（SoakRecorder をシーンから外す）
    [MenuItem("Tools/Clear Soak Recorder")]
    public static void Clear()
    {
        var go = GameObject.Find("SoakRecorder");
        if (go != null) Object.DestroyImmediate(go);
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
    }
}
