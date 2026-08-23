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
    const string ScenePath = "Assets/Scenes/ParkScene_v2.unity"; // User改名（旧: ParkScene.unity）

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
        // レーン対称配置（User案 2026-08-22）: JPスピナー×2=Z軸両端 / 沼×2=X軸両端
        // 建て込みは基準位置で行い、グループごとY回転で配置（鏡像問題を回避）
        BuildTowerG_Numa(root, "TowerG_E", 90f);    // 東(+X)
        BuildTowerG_Numa(root, "TowerG_W", -90f);   // 西(-X)
        BuildTowerF_JPSpinner(root, "TowerF_S", 0f);    // 南(-Z)
        BuildTowerF_JPSpinner(root, "TowerF_N", 180f);  // 北(+Z)
        // フェーズ5（User配置指示 2026-08-23）: ハズレルート接続型サルベージ抽選
        // 対称2基化（User案）: 180°回転でG西/F北のハズレルートにも同型を接続
        // フェーズ6新フロー: F当選→E上段 / Fハズレ→C→E下段、G当選→D上段 / Gハズレ→B→D下段
        BuildTowerC_Zigzag(root, "TowerC_S", -1f);
        BuildTowerC_Zigzag(root, "TowerC_N", +1f);
        BuildTowerE_Wheel(root, "TowerE_S", -1f);
        BuildTowerE_Wheel(root, "TowerE_N", +1f);
        BuildTowerB_Pachinko(root, "TowerB_E", 0f);
        BuildTowerB_Pachinko(root, "TowerB_W", 180f);
        BuildTowerD_Kuruun(root, "TowerD_E", 0f);
        BuildTowerD_Kuruun(root, "TowerD_W", 180f);
        BuildFixedCameras(root);   // 抽選機ごとの定点カメラ（既定オフ）
        BuildTowerH_Garapon(root);                      // 赤: 大型ルーレット直下のガラポン挿入

        // フェーズ1スモーク用スポナー
        var spawner = new GameObject("BallSpawner").AddComponent<BallSpawner>();
        spawner.transform.SetParent(root);
        spawner.transform.position = new Vector3(-1.0f, 1.6f, 1.0f);
        spawner.ballPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/LotteryBall.prefab");
        spawner.count = 36;  // フェーズ6完了(2026-08-23)で36球へ（DESIGN-v2 6章フェーズ7の負荷検証水準）
        spawner.interval = 1f;
        // サンプルキャラスキン常用（User作・CC BY 4.0）
        spawner.characterSkin = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/BallSkins_Sample.png");

        var cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = new Vector3(18f, 12f, 15f);
            cam.transform.LookAt(new Vector3(1f, 4f, 0));
            cam.farClipPlane = 100f;
            if (cam.GetComponent<FollowCamera>() == null) cam.gameObject.AddComponent<FollowCamera>();
            // ボール情報HUD（Penchant書体・追従対象の番号/周回/得点）
            var hud = cam.GetComponent<BallHUD>();
            if (hud == null) hud = cam.gameObject.AddComponent<BallHUD>();
            hud.font = PenchantFont();
            hud.followCam = cam.GetComponent<FollowCamera>();
        }

        // 罠20: 塔グループは生成後に一括Y回転しているので、同期しないとエディタ時のコライダが
        // 回転前の位置に残る（検証レイキャストが別の塔を掴んで誤診の元になる）
        Physics.SyncTransforms();
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
        // 広幅化（罠12対応 2026-08-22）: 喉元を±0.40ポケット化＋ローター1.7倍（r≈0.43）。
        // 先端は常に壁内=密閉掃引を維持（隙間0.5〜1.5dの楔ゾーンを作らない）。レーン/リフトは無変更。
        st.transform.localScale = new Vector3(1.7f, 1f, 1.7f);
        st.AddComponent<Rotator>().degreesPerSecond = 24f; // axis=up 既定（ローカル上軸=エプロン法線）
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
        // フェーズ5(2026-08-23): 旧B予定地(-7,9.8,-4)への空投下を廃止（半数の球が全機構スキップだった）。
        // 両リフトともA分岐盤へ→全球が抽選網に乗る。配点は24球バランスフェーズで再調整。
        BuildLift(g, "LiftS", -0.09f, new Vector3(0, 13.5f, 0), 0.6f);
        // 頂部搬送レール（User要望 2026-08-23）: 見た目専用（ボールはウェイポイント搬送・コライダ無し）。
        // ツインチューブが両レーン(z±0.09)をまたぐ。ボール(y14)の少し下(13.90)に敷設
        var railPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/LiftTopRail.fbx");
        var topRail = (GameObject)Object.Instantiate(railPrefab, new Vector3(0f, 13.90f, 0f), Quaternion.Euler(-90f, 180f, 0), g);
        topRail.name = "LiftTopRail";
        foreach (var r in topRail.GetComponentsInChildren<Renderer>())
            r.sharedMaterial = Mat("LiftGuide", new Color(0.35f, 0.35f, 0.40f));
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
        // 3巻き版（User案 2026-08-23）: 4巻きから中間1巻きを切除（ピッチ0.743・入口/テール無傷）。
        // 排出高が9.55→10.29に上がった分、以降の全機構を+0.74詰めて下部の高さを確保
        var spiralPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/TowerA_Spiral3.fbx");
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
        // 注意: 羽根の面内短縮は不可（0.92でリム停留球=r0.80に届かず滞留。羽根先端0.83はリム球0.815にぎりぎり届く設計）
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
    // スパイラル3巻き化(2026-08-23)に伴い、A23以下の全機構を+Yシフト（LIFT定数）
    // 0.74=3巻き化で浮いた1ピッチ分 + 0.50=User指示の追加詰め（テール→クレーター落差1.10→0.60）
    const float LiftY = 1.24f;

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
            var kuruun = (GameObject)Object.Instantiate(kuruunPrefab, c + new Vector3(0, 8.45f + LiftY, 0), Quaternion.Euler(-90f, 0, 0), sub);
            kuruun.name = "MiniKuruun";
            SetupMesh(kuruun, kuruunMat);
            var ks = new GameObject("KuruunStirrer");
            ks.transform.SetParent(sub);
            ks.transform.position = c + new Vector3(0, 8.54f + LiftY, 0);
            ks.AddComponent<Rotator>().degreesPerSecond = 12f;
            var krb = ks.GetComponent<Rigidbody>();
            krb.isKinematic = true;
            krb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var ksMesh = InstantiateFbx("Assets/Models/DrainStirrer.fbx", "StirrerMesh", ks.transform, Accent, true);
            ksMesh.transform.localPosition = Vector3.zero;
            ksMesh.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            // ③ミニルーレット（ボウル静止＋ホイール回転）
            var bowl = (GameObject)Object.Instantiate(bowlPrefab, c + new Vector3(0, 7.25f + LiftY, 0), Quaternion.Euler(-90f, 0, 0), sub);
            bowl.name = "MiniRouletteBowl";
            SetupMesh(bowl, bowlMat);
            var wheel = (GameObject)Object.Instantiate(wheelPrefab, c + new Vector3(0, 7.25f + LiftY, 0), Quaternion.Euler(-90f, 0, 0), sub);
            wheel.name = "MiniRouletteWheel";
            SetupMesh(wheel, wheelMat);
            var wrot = wheel.AddComponent<Rotator>();
            wrot.axis = Vector3.forward;
            wrot.degreesPerSecond = 18f;
            var wrb = wheel.GetComponent<Rigidbody>();
            wrb.isKinematic = true;
            wrb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            // スコア＋レーン分岐（User案 2026-08-22）:
            // 内向き2口=低得点(X:10/Z:20)→中央集約ファンネルへ落下、
            // 外向き2口=高得点(X:80/Z:120)→HighLane（高レア横ルート）へ
            var lanePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/TowerA_HighLane.fbx");
            var laneMat = Mat("TowerA_HighLane", new Color(0.85f, 0.72f, 0.25f)); // 高レア=金色系
            for (int i = 0; i < 4; i++)
            {
                float taz = i * 90f;
                var dir = new Vector3(Mathf.Cos(taz * Mathf.Deg2Rad), 0, Mathf.Sin(taz * Mathf.Deg2Rad));
                bool centerFacing =
                    (Mathf.Abs(dir.x) > 0.5f && Mathf.Sign(dir.x) != Mathf.Sign(c.x)) ||
                    (Mathf.Abs(dir.z) > 0.5f && Mathf.Sign(dir.z) != Mathf.Sign(c.z));
                bool xAxis = Mathf.Abs(dir.x) > 0.5f;
                // 配点則「点数=C/P」(C=2.5): 各口P≈25% → 全16口一律10pt。
                // ミニルーレットは分配器扱い（当たり演出は衛星側の低確率トリガーへ集約）
                int p = 10;
                if (centerFacing)
                    ScoreGateAt(sub, c + dir * 1.03f + new Vector3(0, 7.12f + LiftY, 0), taz, p, new Color(0.92f, 0.92f, 0.95f));
                else
                    ScoreGateAt(sub, c + dir * 1.35f + new Vector3(0, 7.08f + LiftY, 0), taz, p, new Color(1.0f, 0.83f, 0.25f));
                if (!centerFacing)
                {
                    // 機構系FBXもX-mirror（実測）: blender+Xの向き = world az 180-yaw → yaw = 180-方位角
                    var lane = (GameObject)Object.Instantiate(lanePrefab,
                        c + dir * 1.10f + new Vector3(0, 7.10f + LiftY, 0), Quaternion.Euler(-90f, 180f - taz, 0), sub);
                    lane.name = "HighLane_" + taz;
                    SetupMesh(lane, laneMat);
                }
            }
        }
        // ---- 中央集約: ファンネル(8口の落下環r≈1.6を受ける)→大型ルーレット（Grand版FBX採用） ----
        var funnelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/TowerA_CollectorFunnel.fbx");
        var funnel = (GameObject)Object.Instantiate(funnelPrefab, new Vector3(0, 6.15f + LiftY, 0), Quaternion.Euler(-90f, 0, 0), g);
        funnel.name = "CollectorFunnel";
        SetupMesh(funnel, Mat("TowerA_CollectorFunnel", new Color(0.55f, 0.50f, 0.68f)));
        var fs = new GameObject("FunnelStirrer");
        fs.transform.SetParent(g);
        fs.transform.position = new Vector3(0, 6.24f + LiftY, 0);
        fs.AddComponent<Rotator>().degreesPerSecond = 12f;
        var frb = fs.GetComponent<Rigidbody>();
        frb.isKinematic = true;
        frb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        var fsMesh = InstantiateFbx("Assets/Models/DrainStirrer.fbx", "StirrerMesh", fs.transform, Accent, true);
        fsMesh.transform.localPosition = Vector3.zero;
        fsMesh.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        var gBowlPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/TowerA_RouletteBowl.fbx");
        var gBowl = (GameObject)Object.Instantiate(gBowlPrefab, new Vector3(0, 5.0f + LiftY, 0), Quaternion.Euler(-90f, 0, 0), g);
        gBowl.name = "GrandRouletteBowl";
        SetupMesh(gBowl, Mat("TowerA_GrandRouletteBowl", new Color(0.28f, 0.40f, 0.30f)));
        var gWheelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/TowerA_RouletteWheel.fbx");
        var gWheel = (GameObject)Object.Instantiate(gWheelPrefab, new Vector3(0, 5.0f + LiftY, 0), Quaternion.Euler(-90f, 0, 0), g);
        gWheel.name = "GrandRouletteWheel";
        SetupMesh(gWheel, Mat("TowerA_GrandRouletteWheel", new Color(0.70f, 0.28f, 0.30f)));
        var gwrot = gWheel.AddComponent<Rotator>();
        gwrot.axis = Vector3.forward;
        gwrot.degreesPerSecond = 15f;
        var gwrb = gWheel.GetComponent<Rigidbody>();
        gwrb.isKinematic = true;
        gwrb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        // 配点則: 通常口10×3＋当たり口40×1（西=-X。機内当たり率25%・1巡あたりP=12.5%）
        int[] gpts = { 10, 10, 20, 10 };  // 西当たり口: P=12.5%実測→C/P=20
        for (int i = 0; i < 4; i++)
        {
            float gazDeg = i * 90f;
            float gaz = gazDeg * Mathf.Deg2Rad;
            var pos = new Vector3(1.80f * Mathf.Cos(gaz), 4.90f + LiftY, 1.80f * Mathf.Sin(gaz));
            ScoreGateAt(g, pos, gazDeg, gpts[i], new Color(1.0f, 0.45f, 0.35f));
        }
    }

    // ---- 得点ゲート（メダルゲームのチャッカー風・Blenderメッシュ＋PenchantManufacture書体） ----
    // 表示はTMP SDF（深度テスト＋背面カリング）: 壁の奥・反対向きの得点は自動的に見えない。
    static TMPro.TMP_FontAsset _penchantFont;
    static TMPro.TMP_FontAsset PenchantFont()
    {
        if (_penchantFont != null) return _penchantFont;
        _penchantFont = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Fonts/PenchantManufacture_SDF.asset");
        if (_penchantFont == null)
        {
            if (TMPro.TMP_Settings.instance == null)
            {
                // TMP必須リソース未導入だとCreateFontAssetがNullRefで落ちる
                TMPro.TMP_PackageResourceImporter.ImportResources(true, false, false);
                AssetDatabase.Refresh();
            }
            var src = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/PenchantManufacture.otf");
            _penchantFont = TMPro.TMP_FontAsset.CreateFontAsset(src, 64, 6,
                UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 512, 512);
            _penchantFont.name = "PenchantManufacture_SDF";
            AssetDatabase.CreateAsset(_penchantFont, "Assets/Fonts/PenchantManufacture_SDF.asset");
            _penchantFont.atlasTexture.name = "PenchantAtlas";
            AssetDatabase.AddObjectToAsset(_penchantFont.atlasTexture, _penchantFont);
            _penchantFont.material.name = "PenchantManufacture_SDF_Mat";
            AssetDatabase.AddObjectToAsset(_penchantFont.material, _penchantFont);
            AssetDatabase.SaveAssets();
        }
        return _penchantFont;
    }

    static Material PenchantCullBack()
    {
        const string path = "Assets/Fonts/Penchant_SDF_CullBack.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(PenchantFont().material);
            m.SetFloat("_CullMode", 2f); // Cull Back: 裏側から見えない
            AssetDatabase.CreateAsset(m, path);
        }
        return m;
    }

    /// 得点ゲート一式: ゲートメッシュ＋スコアトリガー＋ボード上のTMP得点表示
    static void ScoreGateAt(Transform parent, Vector3 floorPos, float azDeg, int points, Color textColor)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/TowerA_ScoreGate.fbx");
        var gate = (GameObject)Object.Instantiate(prefab, floorPos, Quaternion.Euler(-90f, 180f - azDeg, 0), parent);
        gate.name = "ScoreGate_" + points;
        SetupMesh(gate, Mat("TowerA_ScoreGate", new Color(0.80f, 0.66f, 0.30f)));
        var dir = new Vector3(Mathf.Cos(azDeg * Mathf.Deg2Rad), 0, Mathf.Sin(azDeg * Mathf.Deg2Rad));
        Trigger(parent, "GateScore_" + points, floorPos + Vector3.up * 0.15f,
            new Vector3(0.26f, 0.28f, 0.26f), points, false);
        var go = new GameObject("GateLabel_" + points);
        go.transform.SetParent(parent);
        go.transform.position = floorPos + Vector3.up * 0.415f - dir * 0.035f;
        go.transform.rotation = Quaternion.LookRotation(dir);
        var tmp = go.AddComponent<TMPro.TextMeshPro>();
        var fa = PenchantFont();
        if (fa != null) tmp.font = fa;
        tmp.text = points.ToString();
        tmp.fontSize = 1.5f;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        tmp.color = textColor;
        tmp.rectTransform.sizeDelta = new Vector2(0.6f, 0.2f);
        tmp.fontSharedMaterial = PenchantCullBack();
    }

    /// 落下系スコア地点: トリガー＋TMP得点表示（ゲート無し。深度テスト＋背面カリング）
    static void ScoreMark(Transform parent, Vector3 pos, int points, Color color, float labelAzDeg, Vector3? triggerSize = null)
    {
        Trigger(parent, "DropScore_" + points, pos, triggerSize ?? new Vector3(0.30f, 0.26f, 0.30f), points, false);
        var dir = new Vector3(Mathf.Cos(labelAzDeg * Mathf.Deg2Rad), 0, Mathf.Sin(labelAzDeg * Mathf.Deg2Rad));
        var go = new GameObject("DropLabel_" + points);
        go.transform.SetParent(parent);
        go.transform.position = pos + Vector3.up * 0.25f + dir * 0.18f;
        go.transform.rotation = Quaternion.LookRotation(-dir); // 外側から読む向き
        var tmp = go.AddComponent<TMPro.TextMeshPro>();
        var fa = PenchantFont();
        if (fa != null) tmp.font = fa;
        tmp.text = points.ToString();
        tmp.fontSize = 1.5f;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        tmp.color = color;
        tmp.rectTransform.sizeDelta = new Vector2(0.6f, 0.2f);
        tmp.fontSharedMaterial = PenchantCullBack();
    }

    // ---- タワーG「沼」(0,5): 釘バラし盤→三連クルーン縦積み（HighLane+Z×2から給球） ----
    // 盤: 前板(低)がA側・後板(高)が北。ギャップ0.15=1.5d・釘4段千鳥・V字ガイドで中央へ集約。
    // クルーンはMiniKuruunのスケール流用(0.8/0.65/0.52)。撹拌なし（低トラフィック前提。
    // ponytail: 2球同時到達でk3穴(1.66d)アーチの可能性→観測されたら小型撹拌追加）
    static void BuildTowerG_Numa(Transform root, string name, float yawDeg)
    {
        var g = Group(root, name);
        // フェーズ6（User指示 2026-08-23）: 釘バラし盤は廃止（釘系はタワーBが担う。TowerG_NumaBoard.fbxは未使用残置）。
        // 給球実測: HighLane+X終端=(4.82, 床8.20, z±1.40..1.65)・v≈1.1-1.4 → 着弾窓 x5.35-5.55/y6.95。
        // 盤が担っていた飛翔捕捉の代替: FeedTrough×2を着弾窓に置き、6°チルトで皿中心側(z±0.6)へ流して投下。
        // 取りこぼし（高速球）はMergeTray東壁(6.45)が受けてハズレ路へ＝フェイルセーフ
        // ローカル系注意: グループyaw回転前。トラフ長手=ローカルx(=ワールドz)・チルト軸=ローカルforward。
        // FeedTroughメッシュは原点=端・−ローカルx方向へ1.0伸びる（実測）。中心側(ローカル|x|小)が低くなる6°チルト
        var ftMat = Mat("TowerF_CatchTray", new Color(0.55f, 0.50f, 0.42f));
        InstantiateMech("Assets/Models/TowerDE_FeedTrough.fbx", "FeedGuide_S", g,
            new Vector3(1.6f, 5.71f + LiftY, 5.46f),
            Quaternion.AngleAxis(6f, Vector3.forward) * Quaternion.Euler(-90f, 0f, 0), ftMat);
        InstantiateMech("Assets/Models/TowerDE_FeedTrough.fbx", "FeedGuide_N", g,
            new Vector3(-0.6f, 5.71f + LiftY, 5.46f),
            Quaternion.AngleAxis(-6f, Vector3.forward) * Quaternion.Euler(-90f, 0f, 0), ftMat);
        // v3「当たり穴＋ハズレ穴」方式（User原案）: 皿=リングトラフ＋中央クレーター縁(高0.055=確率ノブ)。
        // 当たり=縁を越えて中央穴(3.0d)へ→採点、ハズレ=トラフ床穴×2(3.2d, r0.39, 世界±X)から次皿へ素通り落下(採点なし)。
        // すり鉢単穴の「減速球は必ず中央へ」問題を構造で解消（旧壁ノッチ式は勝率75%超で廃止）。
        // 穴・トラフ・縁は3皿とも絶対寸法＝L/M/S別メッシュ（一律スケールだとk3の穴が3d未満に潰れる）
        var kMat = Mat("TowerG_NumaKuruun", new Color(0.45f, 0.55f, 0.40f));
        // 千鳥オフセット（User指摘の同軸素通し防止）は維持: 上の当たり穴の真下=次皿のクレーター斜面
        string[] variants = { "L", "M", "S" };
        float[] roots = { 5.10f + LiftY, 4.30f + LiftY, 3.55f + LiftY };
        float[] xoff = { 0f, 0.25f, -0.20f };
        for (int i = 0; i < 3; i++)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/TowerG_NumaKuruun_" + variants[i] + ".fbx");
            var k = (GameObject)Object.Instantiate(prefab, new Vector3(xoff[i], roots[i], 5.0f), Quaternion.Euler(-90f, 0, 0), g);
            k.name = "NumaKuruun_" + (i + 1);
            SetupMesh(k, kMat);
        }
        ScoreMark(g, new Vector3(0, 4.96f + LiftY, 5.0f), 20, new Color(0.92f, 0.92f, 0.95f), 90f);
        ScoreMark(g, new Vector3(0.25f, 4.16f + LiftY, 5.0f), 60, new Color(0.92f, 0.92f, 0.95f), 90f);
        ScoreMark(g, new Vector3(-0.20f, 3.40f + LiftY, 5.0f), 110, new Color(1.0f, 0.83f, 0.25f), 90f);  // 最終カップ=G当選（JPレール入口）
        // フェーズ6: 高壁マージトレイ（乖離シュートの放出球を全周で受ける・User赤シールド案の発展形）。
        // 円錐床→スパウト(ローカル+z=世界±X外向き)→B受けへ。中央ボア＋襟をK3当選穴（＝JPレール軸）に整列。
        // ボア実測(2026-08-23): 開口は 0.42(x) × 0.22(z) で、トレイ原点から **世界+Z へ0.10** ずれている。
        // 原点をK3穴に合わせるとボアがJPチューブ断面(内径0.31)に片側だけ食い込み、
        // 落下球が襟の棚(y4.24)に乗って停留した（実測 2/12）。ローカルx を +0.10 して
        // ボア中心をチューブ軸(世界 z=0.20)へ寄せ、軸を挟む2.0d(0.20)のスロットを確保する。
        InstantiateMech("Assets/Models/TowerG_MergeTray.fbx", "MergeTray", g,
            new Vector3(-0.10f, 2.91f + LiftY, 5.0f), Quaternion.Euler(-90f, -90f, 0),
            Mat("TowerG_MergeTray", new Color(0.46f, 0.40f, 0.50f)));
        // G当選のJPレール: K3当選穴(底4.56)→ボア貫通→D上段ボウル(2.65)直上まで密閉2段
        var gtubeMat = Mat("TowerG_JPRail", new Color(1.0f, 0.55f, 0.25f));
        foreach (var ty in new[] { 2.92f, 2.12f })
            InstantiateMech("Assets/Models/TowerF_JPTube.fbx", "JPRail_" + ty, g,
                new Vector3(-0.20f, ty + LiftY, 5.0f), Quaternion.Euler(-90f, 0, 0), gtubeMat);
        g.rotation = Quaternion.Euler(0, yawDeg, 0);  // グループ一括回転（原点ピボット）
    }

    // ---- タワーF「JPスピナー」(0,-5): 受けトレイ→回転穴皿→MissTray＋JPTube（フェーズ6・DESIGN F1〜F3） ----
    // 当選=中央JP穴→JPTube×3段スタック（密閉ガイド）でE上段デッキ(3.9)直上まで降ろす。
    // ハズレ=リング穴(r0.32)→MissTray(中央ボアφ0.4をチューブが貫通・排出=側面スパウト)→C受けへ。
    // 旧Separator（リング床+ギャップ）は廃止: 休止帯停留（罠29）を構造で解消。
    static void BuildTowerF_JPSpinner(Transform root, string name, float yawDeg)
    {
        var g = Group(root, name);
        InstantiateMech("Assets/Models/TowerF_CatchTray.fbx", "CatchTray", g,
            new Vector3(0, 6.25f + LiftY, -5.35f), Quaternion.Euler(-90f, 180f, 0),
            Mat("TowerF_CatchTray", new Color(0.55f, 0.50f, 0.42f)));
        var dish = InstantiateMech("Assets/Models/TowerF_SpinnerDish.fbx", "SpinnerDish", g,
            new Vector3(0, 5.55f + LiftY, -5.0f), Quaternion.Euler(-90f, 0, 0),
            Mat("TowerF_SpinnerDish", new Color(0.75f, 0.55f, 0.20f)));
        var rot = dish.AddComponent<Rotator>();
        rot.axis = Vector3.forward;
        // 22°/s: 休止帯r≈0.18の多球アーチ対策の中速（罠4の遠心軌道50°/s級には未達）
        rot.degreesPerSecond = 22f;
        var drb = dish.GetComponent<Rigidbody>();
        drb.isKinematic = true;
        drb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        // ハズレ受けトレイ: 中央ボアφ0.4・排出スパウト=mesh−X面（Euler(-90,0,0)実測でスパウトは世界az180=西）。
        // C受けは世界東（+X）なので tray yaw=180−グループyaw: F_S(0)→180, F_N(180)→0（グループ回転後に両方とも世界東）
        var trayMat = Mat("TowerF_MissTray", new Color(0.40f, 0.32f, 0.45f));
        // 床=スパウトへ向かう円錐勾配（メッシュ側 k0.05≈2.9°）だが、36球ソークで
        // トレイ床上に2球が数十秒止まる停留を実測（User報告 2026-08-23）。2.9°は
        // ボール径0.1・低摩擦でも「他球に当たって止まった球」を再始動できない浅さ。
        // → 設置側で世界+X（スパウト側）へ4°足して実効≈7°にする。スパウト床は0.07下がるだけで、
        //    Cジグザグ入口天面(5.49)とのクリアランスは保たれる。
        float trayTilt = -4f * Mathf.Cos(yawDeg * Mathf.Deg2Rad);   // 世界+X低で固定（F_N はグループ鏡映ぶん反転）
        InstantiateMech("Assets/Models/TowerF_MissTray.fbx", "MissTray", g,
            new Vector3(0, 4.40f + LiftY, -5.0f),
            Quaternion.AngleAxis(trayTilt, Vector3.forward) * Quaternion.Euler(-90f, 180f - yawDeg, 0), trayMat);
        // JPチューブ×3段（φ0.37外径・各0.8高。天端=皿底6.79の0.02下=密閉。ボアとの隙間0.015<0.5d）
        var tubeMat = Mat("TowerF_JPTube", new Color(1.0f, 0.55f, 0.25f));
        float[] tubeY = { 5.13f, 4.33f, 3.53f };
        for (int i = 0; i < tubeY.Length; i++)
            InstantiateMech("Assets/Models/TowerF_JPTube.fbx", "JPTube_" + i, g,
                new Vector3(0, tubeY[i] + LiftY, -5.0f), Quaternion.Euler(-90f, 0, 0), tubeMat);
        // JP=チューブ内通過（薄型トリガー・二重加算なし）。通過15はトレイ排出スパウト（世界東側=グループyawで反転）
        float sx = Mathf.Cos(yawDeg * Mathf.Deg2Rad);  // F_S:+1 / F_N:-1 → 回転後に世界+Xで一致
        ScoreMark(g, new Vector3(0, 3.42f + LiftY, -5.0f), 150, new Color(1.0f, 0.35f, 0.30f), 270f,
            new Vector3(0.20f, 0.14f, 0.20f));
        ScoreMark(g, new Vector3(1.05f * sx, 4.50f + LiftY, -5.0f), 15, new Color(0.92f, 0.92f, 0.95f), yawDeg,
            new Vector3(0.26f, 0.14f, 0.34f));
        g.rotation = Quaternion.Euler(0, yawDeg, 0);  // グループ一括回転（原点ピボット）
    }

    // ---- タワーB「パチンコ」フェーズ6版: Gマージトレイ・スパウト(6.78, 4.2, z0.2)の排出を受ける ----
    // スパウト球(東向き~1m/s)の飛翔→ピン盤天面フレア(4.8d・中心7.15)へ直接落とし込み（受けトレイ省略）→
    // 2枚板ギャップ+釘拡散→ステップチャッカー(yaw0=西下り反転)→西端排出(6.24, 1.47)→西行きトラフ→D下段(リム1.44想定)。
    // 東端こぼれ・フレア外れ=盆地（フェイルセーフ）
    static void BuildTowerB_Pachinko(Transform root, string name, float yawDeg)
    {
        var g = Group(root, name);
        // 高さ配分（実測ベース）: Gスパウト4.15の直下に盤天面4.10を置くのが上限。
        // 盤1.74＋チャッカー0.55＝2.29を積むと排出は1.81。ここからD下段リム(1.55)へ渡す
        var pboard = InstantiateMech("Assets/Models/TowerB_PachiBoard.fbx", "PachiBoard", g,
            new Vector3(7.15f, 1.12f + LiftY, 0.20f), Quaternion.Euler(-90f, 180f, 0),
            Mat("TowerB_PachiBoard", new Color(0.30f, 0.35f, 0.50f)));
        // 釘上バランス静止対策: 盤ごと微振動（±0.4°・1.3s）
        var posc = pboard.AddComponent<Oscillator>();
        posc.axis = Vector3.up;   // Euler(-90,180,0)配置のためローカルup=世界Z軸（盤面内の傾き揺れ）
        posc.angleA = -0.4f; posc.angleB = 0.4f; posc.period = 1.3f;
        var prb = pboard.GetComponent<Rigidbody>();
        prb.isKinematic = true;  // 非凸MeshCollider+非キネマRBはコライダ無効化される（エディタ時対策）
        prb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        // チャッカー yaw0=西下り（排出をD側へ）。天面ステップ(+0.55)=盤底(2.36)と面一
        InstantiateMech("Assets/Models/TowerB_StepChucker.fbx", "StepChucker", g,
            new Vector3(7.15f, 0.57f + LiftY, 0.20f), Quaternion.Euler(-90f, 0, 0),
            Mat("TowerB_StepChucker", new Color(0.55f, 0.35f, 0.40f)));
        // 穴スコア（yaw0反転後の実穴位置: 西6.70=最頻35 / 中7.18=65 / 東7.70=稀90）
        var thinTrig = new Vector3(0.26f, 0.12f, 0.26f);
        ScoreMark(g, new Vector3(6.70f, 0.92f + LiftY, 0.20f), 35, new Color(1.0f, 0.83f, 0.25f), 0f, thinTrig);
        ScoreMark(g, new Vector3(7.18f, 0.92f + LiftY, 0.20f), 65, new Color(0.92f, 0.92f, 0.95f), 0f, thinTrig);
        ScoreMark(g, new Vector3(7.70f, 0.92f + LiftY, 0.20f), 90, new Color(0.92f, 0.92f, 0.95f), 0f, thinTrig);
        // 採点後の回収: チャッカーの穴を抜けた球は真下へ落ちる（実測13/16が穴通過）。
        // 直下に西下りの連絡トラフ2本を並べて全穴を受け、D下段リム(1.55)へ渡す。
        // 東端こぼれ・トラフ外れは盆地（フェイルセーフ）
        // 専用シュート（新造）: 全穴を覆う2.3m・側壁4.6d・東端バックストップ付き。
        // 細いFeedTrough2本では落下球が横に弾け出て回収率25%だった実測を受けて置換
        InstantiateMech("Assets/Models/TowerB_CatchChute.fbx", "CatchChute", g,
            new Vector3(7.15f, 0.35f + LiftY, 0.20f), Quaternion.Euler(-90f, 0f, 0),
            Mat("TowerB_CatchTray", new Color(0.60f, 0.45f, 0.35f)));
        g.rotation = Quaternion.Euler(0, yawDeg, 0);  // 180°=G西ミラー（原点ピボット）
    }

    // ---- タワーD「クルーンボウル」フェーズ6版: G系の終端（DESIGN G7/G8） ----
    // 上段=高得点チャレンジ（G当選のみ。JPレール下端 world 2.96 からの落下を受ける）
    // 下段=通常抽選（B_CatchChute 西端排出 world ~1.60 ＋ 上段5穴の落下が合流）
    // メッシュ実測（TowerD_Kuruun.fbx / Euler(-90,0,0)基準・原点=トラフ床）:
    //   外径1.72(r0.86) / 底 -0.12 / リム上端 +0.423 / 中央ドーム頂 +0.130
    //   トラフ床= r0.15〜0.30 の平坦環(高さ0) / 穴5個 d0.18@r0.238 = az 342.5,54.5,126.5,198.5,270.5
    // 配置の根拠:
    //   ・上段中心を投下点(5.00,0.20)から +Z 0.50 ずらす → ドーム頂への無摂動投下を回避（罠15）
    //     かつ 東リム 5.86 < CatchChute 西端 6.00 で非干渉
    //   ・下段中心(5.30,0.25)・リム上端1.46 < シュート床1.53 → 潜り込ませてシュート排出を直受け
    //   ・下段は yaw36（穴を上段と半ピッチずらす。同軸素通し防止＝G沼の千鳥則と同じ）
    //   ・上段5穴の落下点は下段中心から最大0.77＝リム0.86の内側（全穴が下段に入る＝合流）
    //   ・こぼれ（リム越え・シュート外れ）は盆地へ直落ち＝フェイルセーフ
    static void BuildTowerD_Kuruun(Transform root, string name, float yawDeg)
    {
        var g = Group(root, name);
        var dMat = Mat("TowerD_Kuruun", new Color(0.50f, 0.42f, 0.62f));
        // 上段ボウル: 床 world 2.40 / リム上端 2.82（JPレール下端2.96の直下ぎりぎりまで持ち上げ＝塔を伸ばす）
        InstantiateMech("Assets/Models/TowerD_Kuruun.fbx", "BowlUpper", g,
            new Vector3(5.00f, 1.16f + LiftY, 0.70f), Quaternion.Euler(-90f, 0, 0), dMat);
        // 下段ボウル: 床 world 1.13 / リム上端は東(B側)1.51・西1.60。
        // ここは上下から挟まれた最難所——上は B_CatchChute 床(1.53)、下は盆地床(3°で+X低)。
        // 水平に置くと西塔(x-5.3)ではスカート下端(root-0.12)と床の隙間が0.07しか無く、
        // 穴から落ちた球がスカート下に潜り込んで抜けられなくなる（36球ソークで6球が滞留・実測）。
        // → **盆地と同じ3°で傾けて隙間を均一化**する（+X低＝床と平行）。これで
        //    ・スカート下の隙間はどのx位置でも約0.13（1.3d）で一定＝楔ゾーンが消える
        //    ・B側（東）のリムは0.045下がるのでシュート床1.53の下に収まったまま
        // 傾きは世界+X低（床と平行）で固定する。グループyaw180のD_WはZ軸チルトも鏡映されるので符号を反転。
        float tiltSign = Mathf.Cos(yawDeg * Mathf.Deg2Rad);   // D_E:+1 / D_W:-1
        InstantiateMech("Assets/Models/TowerD_Kuruun.fbx", "BowlLower", g,
            new Vector3(5.30f, -0.11f + LiftY, 0.25f),
            Quaternion.AngleAxis(-3f * tiltSign, Vector3.forward) * Quaternion.Euler(-90f, 36f, 0), dMat);
        // 撹拌ローター（罠4の標準手当て）: トラフ床は穴と穴の間が1.1dの平坦地で、
        // 静止ボウルだと ①球が平坦地に乗って停留 ②毎回同じ穴に入る（実測16/16が西穴＝罠23の再来）。
        // 低速の掃引アームで球を穴リング上に送り続ける＝停留解消＋入る穴がランダム化（≈1/5）。
        // 盤ごと微振動（Oscillator）は「振動軸=±X」が優先方位になり西穴に集中したため不採用。
        Stirrer(g, "StirrerUpper", new Vector3(5.00f, 1.22f + LiftY, 0.70f));
        Stirrer(g, "StirrerLower", new Vector3(5.30f, -0.03f + LiftY, 0.25f));  // 傾けたぶん+0.02逃がす
        // 当たり穴の真下に薄型トリガー（穴0.18に対し0.20角。隣穴は0.28離れているので誤検出なし）。
        // 当たり穴の選び方は実測ベース: 給球は毎回ほぼ同じ点(az180付近)に落ちるので、
        // 撹拌アームの掃引方向に沿って穴の当選率が偏る。24球実測の分布
        //   上段 az342:4 / 126:3 / 198:3 / 54:2 / 270:0   下段 az18:8 / 90:8 / 162:4 / 306:3 / 234:0
        // から、目標の「5穴中1（≈20%）」に最も近い穴を当たりに割り当てる。
        var thin = new Vector3(0.20f, 0.10f, 0.20f);
        // 上段 az54.5（実測 2/12 ≈17%）= (5.00,0.70) + (0.194, 0.138)
        ScoreMark(g, new Vector3(5.194f, 0.96f + LiftY, 0.838f), 200, new Color(1.0f, 0.35f, 0.30f), 0f, thin);
        // 下段 az162.5（yaw36後・実測 4/23 ≈17%）= (5.30,0.25) + (0.072, -0.227)
        ScoreMark(g, new Vector3(5.372f, -0.36f + LiftY, 0.023f), 45, new Color(0.92f, 0.92f, 0.95f), 0f, thin);
        g.rotation = Quaternion.Euler(0, yawDeg, 0);  // 180°=D西ミラー（原点ピボット）
    }

    // ---- タワーC「ジグザグ」フェーズ6版: FハズレのMissTrayスパウト(東, x1.05, 床5.61)を受ける ----
    // 主面 z=±5.0: スパウト直下→ZigzagShort4段(yaw180: 入口=西端天面/出口=東端下段)→シーソー→
    // FeedTrough(z向き・6°チルト)で z=±5.62 の戻りレーンへ乗り換え→CatchTurn(5.77m西行き)→
    // 西端排出(x≈-1.0)からE車輪西側のピックアップへ落下（E未設置の間は盆地へ=フェイルセーフ）。
    // 同一面に縦積みするとJPチューブ・E車輪・E上段デッキと干渉するため、戻りだけ別レーン（phase5のz-6.05と同じ発想）。
    // Z対称ミラー配置（User美観指示）: 北側は zSign 反転のみ・グループ回転なし。
    static void BuildTowerC_Zigzag(Transform root, string name, float zSign)
    {
        float cz = 5.0f * zSign;      // 主面（F塔と同じ鉛直面・罠11。戻りトラフも同面でジグザグ下をくぐる）
        var g = Group(root, name);
        // ジグザグ: 入口天面(top5.49)はスパウト床(5.61)より低く（縁への横衝突防止）
        InstantiateMech("Assets/Models/TowerC_ZigzagShort.fbx", "Zigzag", g,
            new Vector3(2.20f, 2.71f + LiftY, cz), Quaternion.Euler(-90f, 180f, 0),
            Mat("TowerC_Zigzag", new Color(0.30f, 0.45f, 0.55f)));
        var saw = InstantiateMech("Assets/Models/TowerC_Seesaw.fbx", "Seesaw", g,
            new Vector3(3.65f, 3.09f + LiftY, cz), Quaternion.Euler(-90f, 180f, 0),
            Mat("TowerC_Seesaw", new Color(0.75f, 0.60f, 0.30f)));
        var osc = saw.AddComponent<Oscillator>();
        osc.axis = Vector3.up;      // Euler(-90,180,0)配置のためローカルup=世界Z軸
        osc.angleA = -11f; osc.angleB = 11f; osc.period = 4.5f;
        var srb = saw.GetComponent<Rigidbody>();
        srb.isKinematic = true;
        srb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        // 通過25: シーソー東側の飛翔弧全体を覆う大型トリガー（罠31）
        ScoreMark(g, new Vector3(4.15f, 2.95f + LiftY, cz), 25, new Color(1.0f, 0.83f, 0.25f), 180f,
            new Vector3(0.60f, 0.30f, 0.30f));
        // 戻りトラフ: 主面 z=±5 のまま西行き5.77m（yaw0=西端が低い実測プロファイル）。
        // フェーズ6改訂: 西端 x0.59 で止める＝E車輪(頂3.19・|x|<0.51が3.03超)と非干渉。
        // 排出球は車輪の東上面(≈(0.4,3.09))へ投げ込まれ、カップ捕捉=当たり／弾かれ=下段デッキの通常抽選へ。
        // メッシュ原点は東端寄り（バウンズ中心=原点-2.51実測）→ 補正込みでバウンズ 0.59..6.35
        // 注: カウンターチルトは西部が持ち上がりジグザグ底(3.95)と干渉するため不採用。
        // リコシェット対策は E 側のデッキ2枚直列で受けスパンを延ばす方式（BuildTowerE_Wheel参照）。
        // 西端0.80: E車輪の「カップ掃引円 r1.02」（円盤0.88でなく！）がトラフ底3.03を切らない位置（x0.80で2.94）
        InstantiateMech("Assets/Models/TowerC_CatchTurn.fbx", "ReturnTrough", g,
            new Vector3(6.19f, 1.85f + LiftY, cz), Quaternion.Euler(-90f, 0, 0),
            Mat("TowerC_CatchTurn", new Color(0.42f, 0.52f, 0.48f)));
    }

    // ---- タワーH「ガラポン」(赤・中央): 大型ルーレット直下＝中央チェーン3段目 ----
    // 大型ルーレット(5.74)の4方位排出（デッキ縁r≈2.0-2.4の落下環）をリムr2.6のキャッチファンネル(4.20)で
    // 全受け→スロート(3.2d)→横軸ドラム(r0.45・壁穴3=入口兼出口のビンゴケージ方式・25°/s)→2択スイング→盆地。
    // 待機球はスロート管内でドラム外皮に保持され、壁穴が真上に来たとき1球ずつ吸込→穴が真下で排出。
    // リムの±Zノッチはオーバーフロー用（満杯時は盆地へ直落ち＝フェイルセーフ）。
    // P(通過)=中央系≈50%/巡・スイング当たり≈45%→15pt暫定（24球フェーズでC/P再調整）
    static void BuildTowerH_Garapon(Transform root)
    {
        var g = Group(root, "TowerH");
        // カラコロッタ皿（User案 2026-08-23）: 単穴スロート（アーチ・煙突化で不通）を廃し、
        // 穴リング8(3d・45°)＋中央キャップの高回転皿へ。当たり=東1穴のみ→直下のドラムへ、
        // 他7穴=素通りで盆地落下（フェイルセーフ・背圧なし）。yaw180で当たり穴を+Xへ（X-mirror則）
        InstantiateMech("Assets/Models/TowerH_KarakoDish.fbx", "KarakoDish", g,
            new Vector3(0f, 3.46f + LiftY, 0f), Quaternion.Euler(-90f, 180f, 0),
            Mat("TowerH_KarakoDish", new Color(0.55f, 0.40f, 0.55f)));
        // 当たり穴通過スコア（チューブ内薄型トリガー。45=集計識別のため一意値）
        ScoreMark(g, new Vector3(0.575f, 3.42f + LiftY, 0f), 75, new Color(1.0f, 0.83f, 0.25f), 0f,
            new Vector3(0.20f, 0.14f, 0.20f));
        var drum = InstantiateMech("Assets/Models/TowerH_Drum.fbx", "Drum", g,
            new Vector3(0.575f, 2.89f + LiftY, 0f), Quaternion.Euler(-90f, 180f, 0),  // 頂=当たり穴チューブ下端-0.02密閉
            Mat("TowerH_Drum", new Color(0.80f, 0.55f, 0.25f)));
        var drot = drum.AddComponent<Rotator>();
        drot.axis = Vector3.up;      // Euler(-90,180,0)配置のためローカルup=世界Z（横軸回転）
        drot.degreesPerSecond = 25f;
        var drb = drum.GetComponent<Rigidbody>();
        drb.isKinematic = true;
        drb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        // 2択スイング（TowerC_Seesawメッシュ流用）
        var swing = InstantiateMech("Assets/Models/TowerC_Seesaw.fbx", "Swing", g,
            new Vector3(0.575f, 2.06f + LiftY, 0f), Quaternion.Euler(-90f, 180f, 0),
            Mat("TowerH_Swing", new Color(0.60f, 0.70f, 0.45f)));
        var sosc = swing.AddComponent<Oscillator>();
        sosc.axis = Vector3.up;
        sosc.angleA = -11f; sosc.angleB = 11f; sosc.period = 3.8f; sosc.phase = 0.5f;
        var srb = swing.GetComponent<Rigidbody>();
        srb.isKinematic = true;
        srb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        // 2択スイング当たり=東落ち（薄型トリガー）
        ScoreMark(g, new Vector3(1.075f, 1.76f + LiftY, 0f), 100, new Color(1.0f, 0.83f, 0.25f), 0f,
            new Vector3(0.26f, 0.12f, 0.30f));
    }

    // ---- タワーE「縦回転ポケット車輪」(0,±5): F系終端（フェーズ6・DESIGN F5〜F7） ----
    // 車輪=2枚円盤+ポケット仕切り(φ1.76・隙間0.15にボール0.1)。западピックアップ: C戻りトラフ排出(-1.15,3.1)を
    // 受けトレイ+ブリッジトラフで車輪西下(リム密閉0.05)へ送り、西側上昇→頂点越えの東こぼれ→下段デッキ。
    // 上段デッキ(3.94)=JPチューブ直下の高得点チャレンジ(穴180)。ハズレは東端開放（TopSplit東壁は除去済み）→下段へ。
    // 下段デッキ(2.0)=通常抽選(穴55)。通過=東端→盆地（フェイルセーフ）。
    // 注意: 車輪底~1.43は盆地床(非LiftY系)との相対。LiftY変更時はここを再実測すること。
    static void BuildTowerE_Wheel(Transform root, string name, float zSign)
    {
        float cz = 5.0f * zSign;
        var g = Group(root, name);
        var wheel = InstantiateMech("Assets/Models/TowerDE_BigWheel.fbx", "Wheel", g,
            new Vector3(0, 1.07f + LiftY, cz), Quaternion.Euler(-90f, 0, 0),
            Mat("TowerE_Wheel", new Color(0.30f, 0.50f, 0.62f)));
        var wrot = wheel.AddComponent<Rotator>();
        wrot.axis = Vector3.forward;
        wrot.degreesPerSecond = -20f;  // 負=+Z視でCW=西側上昇（シミュ実測で確定した向き）
        var wrb = wheel.GetComponent<Rigidbody>();
        wrb.isKinematic = true;
        wrb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        // フェーズ6改訂: 底面掬い機構は廃止（C排出3.1 > 下段デッキ2.0でリフト不要と判明）。
        // 車輪は「投げ込み式の抽選機構」: Cトラフ排出球が東上面に着弾→カップ捕捉(≈22%)=当たり／弾かれ=下段デッキへ。
        // 捕捉球は東下降でカップ口が下を向き、車輪際に低速リリース→Winトレイ(55)。弾かれ球は飛翔弧でトレイを飛び越す（罠31の逆用）。
        var trayMechMat = Mat("TowerE_Pickup", new Color(0.55f, 0.45f, 0.35f));
        InstantiateMech("Assets/Models/TowerDE_PickupTray.fbx", "WinTray", g,
            new Vector3(0.88f, 1.06f + LiftY, cz),
            Quaternion.AngleAxis(4f * zSign, Vector3.right) * Quaternion.Euler(-90f, zSign < 0 ? 0f : 180f, 0),
            trayMechMat);
        ScoreMark(g, new Vector3(0.88f, 1.16f + LiftY, cz), 55, new Color(1.0f, 0.83f, 0.25f), 270f,
            new Vector3(0.24f, 0.12f, 0.40f));
        // 上段デッキ（高得点チャレンジ・JPチューブ(底4.26)直下）: 東下り3°・穴=origin-0.30
        var deckMat = Mat("TowerE_Deck", new Color(0.62f, 0.55f, 0.30f));
        InstantiateMech("Assets/Models/TowerE_TopSplit.fbx", "UpperDeck", g,
            new Vector3(0.62f, 2.66f + LiftY, cz),
            Quaternion.AngleAxis(-3f, Vector3.forward) * Quaternion.Euler(-90f, 0, 0), deckMat);
        ScoreMark(g, new Vector3(0.32f, 2.56f + LiftY, cz), 180, new Color(1.0f, 0.35f, 0.30f), 270f,
            new Vector3(0.18f, 0.12f, 0.18f));
        // 下段デッキ2枚直列（通常抽選・車輪で弾かれた球+上段ハズレを受ける）:
        // 高速リコシェット球の着地分布(実測1.2〜2.8m)をカバー。A穴=45／B穴=40、通過=東端→盆地
        InstantiateMech("Assets/Models/TowerE_TopSplit.fbx", "LowerDeckA", g,
            new Vector3(1.80f, 0.72f + LiftY, cz),
            Quaternion.AngleAxis(-3f, Vector3.forward) * Quaternion.Euler(-90f, 0, 0), deckMat);
        ScoreMark(g, new Vector3(1.50f, 0.62f + LiftY, cz), 45, new Color(1.0f, 0.83f, 0.25f), 270f,
            new Vector3(0.18f, 0.12f, 0.18f));
        InstantiateMech("Assets/Models/TowerE_TopSplit.fbx", "LowerDeckB", g,
            new Vector3(2.90f, 0.60f + LiftY, cz),
            Quaternion.AngleAxis(-3f, Vector3.forward) * Quaternion.Euler(-90f, 0, 0), deckMat);
        ScoreMark(g, new Vector3(2.60f, 0.50f + LiftY, cz), 40, new Color(1.0f, 0.83f, 0.25f), 270f,
            new Vector3(0.18f, 0.12f, 0.18f));
    }

    // ---- 抽選機ごとの定点カメラ（User指示 2026-08-23・前セッションからの継続要望） ----
    // 各機構を正面から捉える固定カメラを Cameras/ 配下に生成。既定は全て無効（enabled=false）で、
    // 観賞時は Inspector か FollowCamera 側から任意の1台を有効化して切り替える。
    // 位置は各塔の実測バウンズに基づく（塔ごとに z or x 方向へ引き、中心をやや上から見下ろす）。
    static void BuildFixedCameras(Transform root)
    {
        var g = Group(root, "Cameras");
        void Cam(string n, Vector3 pos, Vector3 look, float fov)
        {
            var go = new GameObject("Cam_" + n);
            go.transform.SetParent(g);
            go.transform.position = pos;
            go.transform.LookAt(look);
            var c = go.AddComponent<Camera>();
            c.fieldOfView = fov;
            c.farClipPlane = 60f;
            c.depth = -10f;      // MainCameraより後ろ（有効化しても既定表示を奪わない）
            c.enabled = false;   // 既定オフ
        }
        // タワーA（全景・看板）
        Cam("A_Overview", new Vector3(0f, 10.6f, -9.5f), new Vector3(0f, 8.6f, 0f), 50f);
        Cam("A_GrandRoulette", new Vector3(0f, 6.9f, -4.4f), new Vector3(0f, 5.9f, 0f), 45f);
        // タワーH（中央・カラコロッタ＋ガラポン）
        Cam("H_Garapon", new Vector3(0f, 4.9f, -4.2f), new Vector3(0f, 4.0f, 0f), 45f);
        // F/E系（南北）: 南は-Z側から、北は+Z側から見る
        for (int s = 0; s < 2; s++)
        {
            float sg = s == 0 ? -1f : 1f;
            string sfx = s == 0 ? "S" : "N";
            Cam("F_JPSpinner_" + sfx, new Vector3(0f, 7.2f, sg * 8.6f), new Vector3(0f, 6.5f, sg * 5f), 42f);
            Cam("E_Wheel_" + sfx, new Vector3(1.4f, 3.0f, sg * 8.5f), new Vector3(1.4f, 2.5f, sg * 5f), 55f);
            Cam("C_Zigzag_" + sfx, new Vector3(2.6f, 4.4f, sg * 8.4f), new Vector3(2.6f, 3.6f, sg * 5f), 55f);
        }
        // G/B/D系（東西）: 東は+X側から、西は-X側から見る
        for (int s = 0; s < 2; s++)
        {
            float sg = s == 0 ? 1f : -1f;
            string sfx = s == 0 ? "E" : "W";
            Cam("G_Numa_" + sfx, new Vector3(sg * 5f, 6.4f, -4.8f), new Vector3(sg * 5f, 5.4f, 0f), 50f);
            Cam("B_Pachinko_" + sfx, new Vector3(sg * 7.15f, 3.4f, -3.6f), new Vector3(sg * 7.15f, 2.9f, 0.2f), 50f);
            Cam("D_Kuruun_" + sfx, new Vector3(sg * 5f, 2.6f, -3.4f), new Vector3(sg * 5f, 1.8f, 0f), 50f);
        }
        // 排水・リフト（循環の要）
        Cam("DrainStation", new Vector3(11.2f, 1.5f, -3.2f), new Vector3(11.2f, 0.3f, 0f), 50f);
    }

    /// 機構系FBXの配置ヘルパ（回転指定つき）
    /// 皿・ボウル用の低速撹拌ローター（DrainStirrer流用。罠4の標準手当て）。
    /// 親=回転体（軸=世界Y）／子=メッシュ（Z-up規約補正）。XZのみ拡大して高さプロファイルは実測のまま使う。
    static void Stirrer(Transform parent, string name, Vector3 pos, float xzScale = 1.25f, float degPerSec = 35f)
    {
        var st = new GameObject(name);
        st.transform.SetParent(parent);
        st.transform.position = pos;
        st.transform.localScale = new Vector3(xzScale, 1f, xzScale);
        st.AddComponent<Rotator>().degreesPerSecond = degPerSec;   // axis=up 既定
        var rb = st.GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;  // 罠14
        var mesh = InstantiateFbx("Assets/Models/DrainStirrer.fbx", "StirrerMesh", st.transform, Accent, true);
        mesh.transform.localPosition = Vector3.zero;
        mesh.transform.localRotation = Quaternion.Euler(90f, 0, 0);
    }

    static GameObject InstantiateMech(string path, string name, Transform parent, Vector3 pos, Quaternion rot, Material mat)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var go = (GameObject)Object.Instantiate(prefab, pos, rot, parent);
        go.name = name;
        SetupMesh(go, mat);
        return go;
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
        lift.speed = 1.7f; // 半速化(User要望 2026-08-23): 視線が追える速度に。BallLiftは球ごと並行搬送なので処理能力は不変
        lift.releaseJitter = releaseJitter;
        lift.waypoints = new Transform[]
        {
            Waypoint(liftGO.transform, "W0", new Vector3(12.33f, 14f, laneZ)),
            Waypoint(liftGO.transform, "W1", new Vector3(dropPoint.x, 14f, dropPoint.z)),
            Waypoint(liftGO.transform, "W2", dropPoint),
        };
        // ガイドレール（Blenderメッシュ・見た目専用）。片面シェルの裏面が透けるため両面描画（法線は健全と実測）
        var guideMat = Mat("LiftGuide", new Color(0.35f, 0.35f, 0.40f));
        guideMat.SetFloat("_Cull", 0f);  // Both faces
        var guide = InstantiateFbx("Assets/Models/LiftGuide.fbx", name + "_Guide", parent, guideMat, false);
        guide.transform.position = new Vector3(12.33f, 0, laneZ);
    }
}
