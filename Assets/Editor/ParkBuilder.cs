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

        // フェーズ1スモーク用スポナー
        var spawner = new GameObject("BallSpawner").AddComponent<BallSpawner>();
        spawner.transform.SetParent(root);
        spawner.transform.position = new Vector3(-1.0f, 1.6f, 1.0f);
        spawner.ballPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/LotteryBall.prefab");
        spawner.count = 12;  // 12球テスト（16球化は排水路広幅化=罠12対応後）
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
                    ScoreGateAt(sub, c + dir * 1.03f + new Vector3(0, 7.12f, 0), taz, p, new Color(0.92f, 0.92f, 0.95f));
                else
                    ScoreGateAt(sub, c + dir * 1.35f + new Vector3(0, 7.08f, 0), taz, p, new Color(1.0f, 0.83f, 0.25f));
                if (!centerFacing)
                {
                    // 機構系FBXもX-mirror（実測）: blender+Xの向き = world az 180-yaw → yaw = 180-方位角
                    var lane = (GameObject)Object.Instantiate(lanePrefab,
                        c + dir * 1.10f + new Vector3(0, 7.10f, 0), Quaternion.Euler(-90f, 180f - taz, 0), sub);
                    lane.name = "HighLane_" + taz;
                    SetupMesh(lane, laneMat);
                }
            }
        }
        // ---- 中央集約: ファンネル(8口の落下環r≈1.6を受ける)→大型ルーレット（Grand版FBX採用） ----
        var funnelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/TowerA_CollectorFunnel.fbx");
        var funnel = (GameObject)Object.Instantiate(funnelPrefab, new Vector3(0, 6.15f, 0), Quaternion.Euler(-90f, 0, 0), g);
        funnel.name = "CollectorFunnel";
        SetupMesh(funnel, Mat("TowerA_CollectorFunnel", new Color(0.55f, 0.50f, 0.68f)));
        var fs = new GameObject("FunnelStirrer");
        fs.transform.SetParent(g);
        fs.transform.position = new Vector3(0, 6.24f, 0);
        fs.AddComponent<Rotator>().degreesPerSecond = 12f;
        var frb = fs.GetComponent<Rigidbody>();
        frb.isKinematic = true;
        frb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        var fsMesh = InstantiateFbx("Assets/Models/DrainStirrer.fbx", "StirrerMesh", fs.transform, Accent, true);
        fsMesh.transform.localPosition = Vector3.zero;
        fsMesh.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        var gBowlPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/TowerA_RouletteBowl.fbx");
        var gBowl = (GameObject)Object.Instantiate(gBowlPrefab, new Vector3(0, 5.0f, 0), Quaternion.Euler(-90f, 0, 0), g);
        gBowl.name = "GrandRouletteBowl";
        SetupMesh(gBowl, Mat("TowerA_GrandRouletteBowl", new Color(0.28f, 0.40f, 0.30f)));
        var gWheelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/TowerA_RouletteWheel.fbx");
        var gWheel = (GameObject)Object.Instantiate(gWheelPrefab, new Vector3(0, 5.0f, 0), Quaternion.Euler(-90f, 0, 0), g);
        gWheel.name = "GrandRouletteWheel";
        SetupMesh(gWheel, Mat("TowerA_GrandRouletteWheel", new Color(0.70f, 0.28f, 0.30f)));
        var gwrot = gWheel.AddComponent<Rotator>();
        gwrot.axis = Vector3.forward;
        gwrot.degreesPerSecond = 15f;
        var gwrb = gWheel.GetComponent<Rigidbody>();
        gwrb.isKinematic = true;
        gwrb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        // 配点則: 通常口10×3＋当たり口40×1（西=-X。機内当たり率25%・1巡あたりP=12.5%）
        int[] gpts = { 10, 10, 40, 10 };
        for (int i = 0; i < 4; i++)
        {
            float gazDeg = i * 90f;
            float gaz = gazDeg * Mathf.Deg2Rad;
            var pos = new Vector3(1.80f * Mathf.Cos(gaz), 4.90f, 1.80f * Mathf.Sin(gaz));
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
    static void ScoreMark(Transform parent, Vector3 pos, int points, Color color, float labelAzDeg)
    {
        Trigger(parent, "DropScore_" + points, pos, new Vector3(0.30f, 0.26f, 0.30f), points, false);
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
        var board = InstantiateMech("Assets/Models/TowerG_NumaBoard.fbx", "NumaBoard", g,
            new Vector3(0, 5.85f, 5.0f), Quaternion.Euler(-90f, 180f, 0),
            Mat("TowerG_NumaBoard", new Color(0.35f, 0.42f, 0.55f)));
        // v3「当たり穴＋ハズレ穴」方式（User原案）: 皿=リングトラフ＋中央クレーター縁(高0.055=確率ノブ)。
        // 当たり=縁を越えて中央穴(3.0d)へ→採点、ハズレ=トラフ床穴×2(3.2d, r0.39, 世界±X)から次皿へ素通り落下(採点なし)。
        // すり鉢単穴の「減速球は必ず中央へ」問題を構造で解消（旧壁ノッチ式は勝率75%超で廃止）。
        // 穴・トラフ・縁は3皿とも絶対寸法＝L/M/S別メッシュ（一律スケールだとk3の穴が3d未満に潰れる）
        var kMat = Mat("TowerG_NumaKuruun", new Color(0.45f, 0.55f, 0.40f));
        // 千鳥オフセット（User指摘の同軸素通し防止）は維持: 上の当たり穴の真下=次皿のクレーター斜面
        string[] variants = { "L", "M", "S" };
        float[] roots = { 5.10f, 4.30f, 3.55f };
        float[] xoff = { 0f, 0.25f, -0.20f };
        for (int i = 0; i < 3; i++)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/TowerG_NumaKuruun_" + variants[i] + ".fbx");
            var k = (GameObject)Object.Instantiate(prefab, new Vector3(xoff[i], roots[i], 5.0f), Quaternion.Euler(-90f, 0, 0), g);
            k.name = "NumaKuruun_" + (i + 1);
            SetupMesh(k, kMat);
        }
        ScoreMark(g, new Vector3(0, 4.96f, 5.0f), 20, new Color(0.92f, 0.92f, 0.95f), 90f);
        ScoreMark(g, new Vector3(0.25f, 4.16f, 5.0f), 40, new Color(0.92f, 0.92f, 0.95f), 90f);
        ScoreMark(g, new Vector3(-0.20f, 3.40f, 5.0f), 150, new Color(1.0f, 0.83f, 0.25f), 90f);  // 最終カップ（機内P≈6%）
        g.rotation = Quaternion.Euler(0, yawDeg, 0);  // グループ一括回転（原点ピボット）
    }

    // ---- タワーF「JPスピナー」(0,-5): 受けトレイ→回転穴皿→セパレータ（HighLane-Z×2から給球） ----
    // 皿: 中央JP穴(カラー付)＋通過穴3(r0.45リング)・15°/s回転。下段は半径分離:
    // 中央落下→チューブ→JP150、リング落下→セクター欠きリング床→南ギャップ→30。
    static void BuildTowerF_JPSpinner(Transform root, string name, float yawDeg)
    {
        var g = Group(root, name);
        InstantiateMech("Assets/Models/TowerF_CatchTray.fbx", "CatchTray", g,
            new Vector3(0, 6.25f, -5.35f), Quaternion.Euler(-90f, 180f, 0),
            Mat("TowerF_CatchTray", new Color(0.55f, 0.50f, 0.42f)));
        var dish = InstantiateMech("Assets/Models/TowerF_SpinnerDish.fbx", "SpinnerDish", g,
            new Vector3(0, 5.55f, -5.0f), Quaternion.Euler(-90f, 0, 0),
            Mat("TowerF_SpinnerDish", new Color(0.75f, 0.55f, 0.20f)));
        var rot = dish.AddComponent<Rotator>();
        rot.axis = Vector3.forward;
        rot.degreesPerSecond = 15f;
        var drb = dish.GetComponent<Rigidbody>();
        drb.isKinematic = true;
        drb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        // セクターギャップを南へ（鏡像則: world方位=180-yaw+blender方位。blender270°→yaw180で南270°）
        // ＋南下がり4°チルト: リング床は回転対称で接線力ゼロ→チューブ際で静止する（実測）ため、
        // 最低点=ギャップ方位に傾けてどの着地方位からも南へ転がす
        InstantiateMech("Assets/Models/TowerF_Separator.fbx", "Separator", g,
            new Vector3(0, 4.65f, -5.0f),
            Quaternion.AngleAxis(-4f, Vector3.right) * Quaternion.Euler(-90f, 180f, 0),
            Mat("TowerF_Separator", new Color(0.40f, 0.32f, 0.45f)));
        ScoreMark(g, new Vector3(0, 4.50f, -5.0f), 100, new Color(1.0f, 0.35f, 0.30f), 270f);  // JP（機内P≈10%目標・カラー低背化）
        ScoreMark(g, new Vector3(0, 4.72f, -5.55f), 15, new Color(0.92f, 0.92f, 0.95f), 270f); // 通過
        g.rotation = Quaternion.Euler(0, yawDeg, 0);  // グループ一括回転（原点ピボット）
    }

    /// 機構系FBXの配置ヘルパ（回転指定つき）
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
