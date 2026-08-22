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
        BuildTowerA_Tier1(root);
        BuildTowerA_Tier23(root);

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

    // ---- 地面＋矩形回収フロア（Blender一体メッシュ。寸法はBlenderSources/ParkBase.blend側が正） ----
    static void BuildGroundAndBasin(Transform root)
    {
        var g = Group(root, "Basin");
        InstantiateFbx("Assets/Models/ParkBase.fbx", "ParkBase", g,
            Mat("ParkBase", new Color(0.55f, 0.55f, 0.58f)), true);
    }

    // ---- 排水ステーション（Blender一体メッシュ＋撹拌/トリガーはUnity側） ----
    static void BuildDrainStation(Transform root)
    {
        var g = Group(root, "DrainStation");
        InstantiateFbx("Assets/Models/DrainStation.fbx", "DrainStation", g,
            Mat("DrainStation", new Color(0.42f, 0.45f, 0.52f)), true);
        // 撹拌ローター（テーパー喉元。アーチ崩し標準装備。腕はBlenderメッシュ＝十字バー＋ハブキャップ）
        var st = new GameObject("DrainStirrer");
        st.transform.SetParent(g);
        st.transform.position = new Vector3(11.6f, 0.09f, 0);
        st.transform.rotation = Quaternion.Euler(0, 0, -1.4f);
        st.AddComponent<Rotator>().degreesPerSecond = 20f; // axis=up 既定（ローカル上軸=エプロン法線）
        var srb = st.GetComponent<Rigidbody>();
        srb.isKinematic = true;
        srb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        var stMesh = InstantiateFbx("Assets/Models/DrainStirrer.fbx", "StirrerMesh", st.transform, Accent, true);
        stMesh.transform.localPosition = Vector3.zero;
        stMesh.transform.localRotation = Quaternion.Euler(90f, 0, 0); // Z-up規約→Unity補正（InstantiateFbxと同一）
        // 周回確定（両レーン共通、レーン入口手前）
        var lap = Trigger(g, "LapGate", new Vector3(11.87f, 0.10f, 0), new Vector3(0.10f, 0.16f, 0.38f), 0);
        Object.DestroyImmediate(lap.GetComponent<ScoreZone>());
        lap.AddComponent<LapGate>();
    }

    // ベース系FBXはBlender Z-up規約（blender X=Unity X / Z=Unity Y / Y=-Unity Z）で
    // 作成し、インポータのbakeAxisConversion=ON。Euler(90,0,0)で world=(x, y, -z) になる（実測）。
    // 残るZ反転はベースメッシュが全てZ対称なため無害。Z非対称の新メッシュを作る場合は要再実測。
    static GameObject InstantiateFbx(string path, string name, Transform parent, Material mat, bool collide)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var go = (GameObject)Object.Instantiate(prefab, Vector3.zero, Quaternion.Euler(90f, 0, 0), parent);
        go.name = name;
        if (collide) SetupMesh(go, mat);
        else foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
        return go;
    }

    // ---- リフト2基（各レーン終端から別々の投下点へ） ----
    static void BuildLifts(Transform root)
    {
        var g = Group(root, "Lifts");
        // 高さ4倍方針（User 2026-08-22）: 塔は最大13m級の縦積み。リフト頂部14m
        // タワーA分岐盤へ投下。※静止コーン真頂点への無摂動投下は垂直バウンド＋すり抜けの罠
        // だったが、現在は回転キャップ＋解放ジッタで対称性が崩れるため中心投下でよい。
        // 位置オフセットは方位バイアスになる（西側スパイラルが0件になった実測）ので入れない。
        BuildLift(g, "LiftN", 0.09f, new Vector3(0, 13.5f, 0), 0.6f);
        BuildLift(g, "LiftS", -0.09f, new Vector3(-7f, 9.8f, -4f));    // 将来: タワーB頂上(-7,-4)へ
    }

    // ---- タワーA ① 分岐盤＋大スパイラル×4（水平分散配置, 全メッシュBlender製） ----
    // FBXは軸素通し（Blender座標=Unity座標）。分岐盤は静止（回すとキネマティック体との
    // CCDペア不成立でトンネリングした）。撹拌腕(12°/s)が滞留ボールをノッチへ送る。
    // ノッチ4箇所→radialスナウト→対角配置の4台のスパイラルへ落下投入。
    // 着地点=各スパイラル開始30°先の高い床（yaw=-(az+150)で開始方位=ノッチ30°手前）。
    // 各スパイラル終端は内向きテールで自分の中央シャフトへ排出→盆地へ自由落下（フェイルセーフ）。
    static void BuildTowerA_Tier1(Transform root)
    {
        var g = Group(root, "TowerA");
        var spiralPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/TowerA_Spiral.fbx");
        var spiralMat = Mat("TowerA_Spiral", new Color(0.78f, 0.64f, 0.45f)); // テクスチャ差し替え用
        foreach (float az in new[] { 45f, 135f, 225f, 315f })
        {
            float rad = az * Mathf.Deg2Rad;
            var pos = new Vector3(2.15f * Mathf.Cos(rad), 12.45f, 2.15f * Mathf.Sin(rad));
            // yaw+180（User指摘）: 開始デッドエンドを着地方位の反対側へ置き、
            // 着地帯の前後両方に樋が続く向きにする（取りこぼし→1段落ち防止）
            var spiral = (GameObject)Object.Instantiate(spiralPrefab, pos, Quaternion.Euler(-90f, 30f - az, 0), g);
            spiral.name = "SpiralA_" + az;
            SetupMesh(spiral, spiralMat);
        }
        // 分岐盤（静止）: yaw-45でノッチ/スナウトを対角方位へ
        var dishPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/TowerA_Distributor.fbx");
        var dish = (GameObject)Object.Instantiate(dishPrefab, new Vector3(0, 12.85f, 0), Quaternion.Euler(-90f, -45f, 0), g);
        dish.name = "DistributorA";
        SetupMesh(dish, Mat("TowerA_Distributor", new Color(0.55f, 0.68f, 0.75f)));
        // 撹拌腕（回転・キネマティック・Speculative CCD）
        var agitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/TowerA_Agitator.fbx");
        var agit = (GameObject)Object.Instantiate(agitPrefab, new Vector3(0, 12.85f, 0), Quaternion.Euler(-90f, 0, 0), g);
        agit.name = "AgitatorA";
        SetupMesh(agit, Accent);
        var rot = agit.AddComponent<Rotator>();
        rot.axis = Vector3.forward;   // ルートX-90回転のためローカルz=鉛直軸
        rot.degreesPerSecond = 18f;   // 4枚羽根×18°/s: 羽根遭遇位相で排出方位をばらす
        var arb = agit.GetComponent<Rigidbody>();
        arb.isKinematic = true;
        arb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    // ---- タワーA ②③（フェーズ3改・4分岐版）: 各スパイラル直下に専用チェーン ----
    // 4本の中央シャフト排出(y9.55, スパイラル中心からr≈0.4)は合流させず（User指示）、
    // 各スパイラル中心(±1.52,±1.52)に ②ミニクルーン(r1.34, 穴縁8.45/縁床8.75/壁9.35)
    // → ③ミニルーレット(ボウルr0.87@7.25＋フレット6ホイール18°/s) を1系統ずつ＝計4系統。
    // 撹拌クロスはDrainStirrer.fbx流用（罠10）。スコアは静止側トリガー（罠6）。
    // 排出スナウトは軸方位→自由落下柱で盆地へ（フェイルセーフ維持）。
    static void BuildTowerA_Tier23(Transform root)
    {
        var g = Group(root, "TowerA23");
        var kuruunPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/TowerA_MiniKuruun.fbx");
        var bowlPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/TowerA_MiniRouletteBowl.fbx");
        var wheelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/TowerA_MiniRouletteWheel.fbx");
        var kuruunMat = Mat("TowerA_MiniKuruun", new Color(0.72f, 0.55f, 0.62f));
        var bowlMat = Mat("TowerA_MiniRouletteBowl", new Color(0.30f, 0.45f, 0.30f));
        var wheelMat = Mat("TowerA_MiniRouletteWheel", new Color(0.75f, 0.30f, 0.28f));
        int[] pts = { 40, 20, 100, 60 };  // 軸方位 +X/+Z/-X/-Z
        foreach (float az in new[] { 45f, 135f, 225f, 315f })
        {
            float rad = az * Mathf.Deg2Rad;
            var c = new Vector3(2.15f * Mathf.Cos(rad), 0, 2.15f * Mathf.Sin(rad));
            var sub = Group(g, "Chain_" + az);
            // ②ミニクルーン＋撹拌クロス
            var kuruun = (GameObject)Object.Instantiate(kuruunPrefab, c + new Vector3(0, 8.45f, 0), Quaternion.Euler(-90f, 0, 0), sub);
            kuruun.name = "MiniKuruun";
            SetupMesh(kuruun, kuruunMat);
            var ks = new GameObject("KuruunStirrer");
            ks.transform.SetParent(sub);
            ks.transform.position = c + new Vector3(0, 8.54f, 0);
            ks.AddComponent<Rotator>().degreesPerSecond = 12f;
            var krb = ks.GetComponent<Rigidbody>();
            krb.isKinematic = true;
            krb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var ksMesh = InstantiateFbx("Assets/Models/DrainStirrer.fbx", "StirrerMesh", ks.transform, Accent, true);
            ksMesh.transform.localPosition = Vector3.zero;
            ksMesh.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            // ③ミニルーレット（ボウル静止＋ホイール回転）
            var bowl = (GameObject)Object.Instantiate(bowlPrefab, c + new Vector3(0, 7.25f, 0), Quaternion.Euler(-90f, 0, 0), sub);
            bowl.name = "MiniRouletteBowl";
            SetupMesh(bowl, bowlMat);
            var wheel = (GameObject)Object.Instantiate(wheelPrefab, c + new Vector3(0, 7.25f, 0), Quaternion.Euler(-90f, 0, 0), sub);
            wheel.name = "MiniRouletteWheel";
            SetupMesh(wheel, wheelMat);
            var wrot = wheel.AddComponent<Rotator>();
            wrot.axis = Vector3.forward;
            wrot.degreesPerSecond = 18f;
            var wrb = wheel.GetComponent<Rigidbody>();
            wrb.isKinematic = true;
            wrb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            // スコアトリガー（スナウト出口・静止側）
            for (int i = 0; i < 4; i++)
            {
                float taz = i * 90f * Mathf.Deg2Rad;
                var pos = c + new Vector3(1.06f * Mathf.Cos(taz), 7.27f, 1.06f * Mathf.Sin(taz));
                Trigger(sub, "RouletteScore_" + pts[i], pos, new Vector3(0.24f, 0.28f, 0.24f), pts[i]);
            }
        }
    }

    /// BlenderメッシュFBX共通セットアップ: 単一マテリアル＋非凸MeshCollider＋低摩擦
    static void SetupMesh(GameObject go, Material mat)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
        {
            var mc = mf.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            mc.material = RailPM;
        }
    }

    static void BuildLift(Transform parent, string name, float laneZ, Vector3 dropPoint, float releaseJitter = 0f)
    {
        var liftGO = Trigger(parent, name, new Vector3(12.33f, 0.08f, laneZ), new Vector3(0.16f, 0.12f, 0.15f), 0);
        Object.DestroyImmediate(liftGO.GetComponent<ScoreZone>());
        var lift = liftGO.AddComponent<BallLift>();
        lift.speed = 3.5f; // 14m級に合わせて増速
        lift.releaseJitter = releaseJitter;
        lift.waypoints = new Transform[]
        {
            Waypoint(liftGO.transform, "W0", new Vector3(12.33f, 14f, laneZ)),
            Waypoint(liftGO.transform, "W1", new Vector3(dropPoint.x, 14f, dropPoint.z)),
            Waypoint(liftGO.transform, "W2", dropPoint),
        };
        // ガイドレール（Blenderメッシュ・見た目専用）
        var guide = InstantiateFbx("Assets/Models/LiftGuide.fbx", name + "_Guide", parent,
            Mat("LiftGuide", new Color(0.35f, 0.35f, 0.40f)), false);
        guide.transform.position = new Vector3(12.33f, 0, laneZ);
    }
}
