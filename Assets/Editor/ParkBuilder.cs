using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static GreyboxKit;

/// Tools > Build RouletteSphere Park (v2)
/// 多塔パーク型ボールコースター（Docs/DESIGN-v2.md）のビルダー。冪等。
/// フェーズ1: パーク基盤 = 広幅回収盆地 + テーパー排水路 + 2レーン×リフト2基 + 撹拌ローター
public static class ParkBuilder
{
    // ---- 調整ノブ（DESIGN-v2 4章のクリアランス基準: d=0.1） ----
    // 水平拡張（User 2026-08-22）: 回収フロアは矩形20×13m・+X側へ3°傾斜。
    // 東側はV字漏斗壁で排水口(x=10.5)へ集約
    const float FloorHalfX = 10f, FloorHalfZ = 6.5f;
    const float FloorTiltDeg = 3f;
    const string ScenePath = "Assets/Scenes/ParkScene.unity";

    [MenuItem("Tools/Build RouletteSphere Park (v2)")]
    public static void Build()
    {
        // ---- シーン準備（v1のSampleSceneは温存） ----
        if (SceneManager.GetActiveScene().path != ScenePath)
        {
            EditorSceneManager.SaveOpenScenes();
            if (System.IO.File.Exists(ScenePath))
                EditorSceneManager.OpenScene(ScenePath);
            else
            {
                var s = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                EditorSceneManager.SaveScene(s, ScenePath);
            }
        }

        Init();
        var old = GameObject.Find("Park");
        if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject("Park").transform;

        BuildGroundAndBasin(root);
        BuildDrainStation(root);
        BuildLifts(root);

        // フェーズ1スモーク用スポナー
        var spawner = new GameObject("BallSpawner").AddComponent<BallSpawner>();
        spawner.transform.SetParent(root);
        spawner.transform.position = new Vector3(-1.0f, 1.6f, 1.0f);
        spawner.ballPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/LotteryBall.prefab");
        spawner.count = 8;
        spawner.interval = 1f;

        var cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = new Vector3(18f, 12f, 15f);
            cam.transform.LookAt(new Vector3(1f, 4f, 0));
            cam.farClipPlane = 100f;
            if (cam.GetComponent<FollowCamera>() == null) cam.gameObject.AddComponent<FollowCamera>();
        }

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[ParkBuilder] phase1 build complete");
    }

    // ---- 地面＋矩形回収フロア ----
    static void BuildGroundAndBasin(Transform root)
    {
        var g = Group(root, "Basin");
        Prim(PrimitiveType.Cube, g, "GroundPlate", new Vector3(0.8f, -0.02f, 0), Vector3.zero,
            new Vector3(26f, 0.04f, 16f), Track);
        // 傾斜フロア（+X側が低い）。西端(x-10)y≈1.14 → 東端(x10.6)y≈0.06
        // 東端はV字漏斗壁の合流点(10.5)より先まで延長（端で平地に落ちると停止する）
        Prim(PrimitiveType.Cube, g, "FloorPlate", new Vector3(0.3f, 0.58f, 0),
            new Vector3(0, 0, -FloorTiltDeg), new Vector3(20.6f, 0.04f, FloorHalfZ * 2f), Track);
        // 周囲壁（高所投下のバウンドを収める高壁）
        foreach (float s in new[] { -1f, 1f })
            Prim(PrimitiveType.Cube, g, s < 0 ? "WallS" : "WallN", new Vector3(0.3f, 1.1f, s * (FloorHalfZ + 0.05f)),
                Vector3.zero, new Vector3(21f, 2.2f, 0.06f), Rail);
        Prim(PrimitiveType.Cube, g, "WallW", new Vector3(-(FloorHalfX + 0.05f), 1.4f, 0), Vector3.zero,
            new Vector3(0.06f, 2.6f, FloorHalfZ * 2f + 0.2f), Rail);
        // 東側V字漏斗壁: 東端に達したボールを壁沿いに排水口(z=0)へ滑らせる
        foreach (float s in new[] { -1f, 1f })
        {
            Vector3 p1 = new Vector3(8.8f, 0.7f, s * (FloorHalfZ + 0.05f)), p2 = new Vector3(10.5f, 0.7f, s * 0.53f);
            Prim(PrimitiveType.Cube, g, s < 0 ? "FunnelWallS" : "FunnelWallN", (p1 + p2) * 0.5f,
                Quaternion.LookRotation(p2 - p1).eulerAngles, new Vector3(0.06f, 1.4f, Vector3.Distance(p1, p2) + 0.1f), Rail);
        }
    }

    // ---- 排水ステーション（広幅→浅テーパー→2レーン） ----
    static void BuildDrainStation(Transform root)
    {
        var g = Group(root, "DrainStation");
        // エプロン床（フロア東端の高さから緩やかに下る）
        Prim(PrimitiveType.Cube, g, "Apron", new Vector3(11.25f, 0.015f, 0),
            new Vector3(0, 0, -1.4f), new Vector3(2.6f, 0.04f, 1.3f), Track);
        // 浅テーパー壁（片側約17°。急絞りはアーチする）
        foreach (float s in new[] { -1f, 1f })
        {
            Vector3 p1 = new Vector3(10.5f, 0.18f, s * 0.56f), p2 = new Vector3(11.75f, 0.16f, s * 0.17f);
            Prim(PrimitiveType.Cube, g, s < 0 ? "TaperL" : "TaperR", (p1 + p2) * 0.5f,
                Quaternion.LookRotation(p2 - p1).eulerAngles, new Vector3(0.03f, 0.34f, 1.36f), Rail);
            // レーン平行壁
            Prim(PrimitiveType.Cube, g, s < 0 ? "LaneWallL" : "LaneWallR", new Vector3(12.1f, 0.16f, s * 0.175f),
                Vector3.zero, new Vector3(0.75f, 0.32f, 0.03f), Rail);
        }
        // 2レーン分割ノーズ（幅0.16=1.6dずつ）
        Prim(PrimitiveType.Cube, g, "LaneNose", new Vector3(12.1f, 0.10f, 0), Vector3.zero,
            new Vector3(0.72f, 0.20f, 0.02f), Rail);
        // レーン終端壁
        Prim(PrimitiveType.Cube, g, "LaneEnd", new Vector3(12.49f, 0.16f, 0), Vector3.zero,
            new Vector3(0.03f, 0.32f, 0.40f), Rail);
        // 撹拌ローター（テーパー喉元。アーチ崩し標準装備）
        Stirrer(g, "DrainStirrer", new Vector3(11.6f, 0.09f, 0), new Vector3(0, 0, -1.4f), 2, 0.5f, 20f);
        // 周回確定（両レーン共通、レーン入口手前）
        var lap = Trigger(g, "LapGate", new Vector3(11.87f, 0.10f, 0), new Vector3(0.10f, 0.16f, 0.38f), 0);
        Object.DestroyImmediate(lap.GetComponent<ScoreZone>());
        lap.AddComponent<LapGate>();
    }

    // ---- リフト2基（各レーン終端から別々の投下点へ） ----
    static void BuildLifts(Transform root)
    {
        var g = Group(root, "Lifts");
        // 高さ4倍方針（User 2026-08-22）: 塔は最大13m級の縦積み。リフト頂部14m
        BuildLift(g, "LiftN", 0.09f, new Vector3(0, 12.8f, 0.3f));     // 将来: タワーA頂上(0,0)へ
        BuildLift(g, "LiftS", -0.09f, new Vector3(-7f, 9.8f, -4f));    // 将来: タワーB頂上(-7,-4)へ
    }

    static void BuildLift(Transform parent, string name, float laneZ, Vector3 dropPoint)
    {
        var liftGO = Trigger(parent, name, new Vector3(12.33f, 0.08f, laneZ), new Vector3(0.16f, 0.12f, 0.15f), 0);
        Object.DestroyImmediate(liftGO.GetComponent<ScoreZone>());
        var lift = liftGO.AddComponent<BallLift>();
        lift.speed = 3.5f; // 14m級に合わせて増速
        lift.waypoints = new Transform[]
        {
            Waypoint(liftGO.transform, "W0", new Vector3(12.33f, 14f, laneZ)),
            Waypoint(liftGO.transform, "W1", new Vector3(dropPoint.x, 14f, dropPoint.z)),
            Waypoint(liftGO.transform, "W2", dropPoint),
        };
        // ガイドレール（見た目）
        VisualBar(parent, name + "_GuideA", new Vector3(12.33f + 0.09f, 7f, laneZ), Vector3.zero, new Vector3(0.03f, 14f, 0.03f));
        VisualBar(parent, name + "_GuideB", new Vector3(12.33f - 0.09f, 7f, laneZ), Vector3.zero, new Vector3(0.03f, 14f, 0.03f));
    }
}
