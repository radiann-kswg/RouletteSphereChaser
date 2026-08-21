using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Tools > Build RouletteSphere Greybox で観賞用ボールマシンの仮組みシーン一式を生成する。
/// 再実行時は既存の "BallMachine" を作り直す（冪等）。
public static class GreyboxBuilder
{
    // ---- 調整ノブ ----
    const float BallR = 0.05f;
    const float SpiralR = 0.45f, SpiralTop = 1.45f, SpiralBottom = 0.90f;
    const float SpiralTurns = 2.5f;
    const int SegsPerTurn = 24;
    const float KuruunOuterR = 0.55f, KuruunY = 0.42f, KuruunDrop = 0.10f;
    const float BasinOuterR = 0.70f, BasinY = 0.26f, BasinDrop = 0.12f; // 内縁y=0.14—下に回収空間を確保

    static Material trackMat, railMat, accentMat;
    static PhysicsMaterial railPM;

    [MenuItem("Tools/Build RouletteSphere Greybox")]
    public static void Build()
    {
        AssetDatabase.Refresh();
        foreach (string dir in new[] { "Assets/Materials", "Assets/Prefabs" })
            if (!AssetDatabase.IsValidFolder(dir))
                AssetDatabase.CreateFolder("Assets", dir.Substring("Assets/".Length));

        trackMat = MakeMat("Greybox_Track", new Color(0.55f, 0.55f, 0.58f));
        railMat = MakeMat("Greybox_Rail", new Color(0.35f, 0.35f, 0.40f));
        accentMat = MakeMat("Greybox_Accent", new Color(0.95f, 0.55f, 0.15f));

        railPM = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/Materials/RailPM.asset");
        if (railPM == null)
        {
            railPM = new PhysicsMaterial("Rail") { dynamicFriction = 0.05f, staticFriction = 0.05f, bounciness = 0.1f };
            AssetDatabase.CreateAsset(railPM, "Assets/Materials/RailPM.asset");
        }

        GameObject prefab = BuildBallPrefab();

        var old = GameObject.Find("BallMachine");
        if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject("BallMachine").transform;

        BuildColumnAndSpiral(root);
        BuildZigzag(root);
        BuildWindmillPlatform(root);
        BuildChutes(root);
        BuildKuruun(root);
        BuildRouletteDisc(root);
        BuildBasinAndLift(root);

        // スポナー
        var spawner = new GameObject("BallSpawner").AddComponent<BallSpawner>();
        spawner.transform.SetParent(root);
        spawner.transform.position = new Vector3(SpiralR, SpiralTop + 0.13f, 0);
        spawner.ballPrefab = prefab;
        spawner.count = 8;

        // 全体スケール: ボール(径0.1)は等倍のまま機体だけ1.5倍 → 相対的に全クリアランスが広がり拡張余地を確保
        root.localScale = Vector3.one * 1.5f;

        // カメラ
        var cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = new Vector3(2.6f, 2.0f, 2.6f);
            cam.transform.LookAt(new Vector3(0, 1.05f, 0));
            if (cam.GetComponent<FollowCamera>() == null) cam.gameObject.AddComponent<FollowCamera>();
        }

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[GreyboxBuilder] build complete");
    }

    // ---- ボールプレハブ ----
    static GameObject BuildBallPrefab()
    {
        var body = MakeMat("BallBody", Color.white);
        var numberMat = MakeMat("BallNumber", Color.white);
        var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/NumberAtlas.png");
        numberMat.SetTexture("_BaseMap", atlas);

        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/LotteryBall.fbx");
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        var rend = inst.GetComponentInChildren<Renderer>();
        rend.sharedMaterials = new[] { body, numberMat };
        var col = inst.AddComponent<SphereCollider>();
        col.radius = BallR;
        col.material = new PhysicsMaterial("Ball") { dynamicFriction = 0.25f, staticFriction = 0.25f, bounciness = 0.2f };
        var rb = inst.AddComponent<Rigidbody>();
        rb.mass = 0.2f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.sleepThreshold = 0f; // 渋滞待機中にスリープして坂で固まるのを防ぐ
        inst.AddComponent<LotteryBall>();

        var prefab = PrefabUtility.SaveAsPrefabAsset(inst, "Assets/Prefabs/LotteryBall.prefab");
        Object.DestroyImmediate(inst);
        return prefab;
    }

    // ---- 中央柱＋スパイラル ----
    static void BuildColumnAndSpiral(Transform root)
    {
        var g = Group(root, "Spiral");
        var col = Prim(PrimitiveType.Cylinder, g, "Column", new Vector3(0, 1.30f, 0), Vector3.zero,
            new Vector3(0.72f, 0.30f, 0.72f), railMat); // 下端y=1.0—デッキ/出口シュートの頭上を確保

        int total = Mathf.RoundToInt(SegsPerTurn * SpiralTurns);
        float pitchDeg = Mathf.Atan((SpiralTop - SpiralBottom) / (SpiralTurns * 2f * Mathf.PI * SpiralR)) * Mathf.Rad2Deg;
        for (int i = 0; i <= total; i++)
        {
            float a = i / (float)SegsPerTurn * 2f * Mathf.PI;
            float y = Mathf.Lerp(SpiralTop, SpiralBottom, i / (float)total);
            Vector3 pos = new Vector3(SpiralR * Mathf.Cos(a), y, SpiralR * Mathf.Sin(a));
            Vector3 tan = new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a));
            Quaternion rot = Quaternion.LookRotation(tan) * Quaternion.Euler(pitchDeg, 0, 0);
            Prim(PrimitiveType.Cube, g, $"Floor_{i}", pos, rot.eulerAngles, new Vector3(0.14f, 0.02f, 0.14f), trackMat);
            Vector3 railPos = pos + rot * new Vector3(0.075f, 0.06f, 0); // 外周レール
            Prim(PrimitiveType.Cube, g, $"Rail_{i}", railPos, rot.eulerAngles, new Vector3(0.02f, 0.12f, 0.16f), railMat);
        }
        // 出口: スパイラルの勢い(-Z)のままカーブシュートへ。外周レールで+Xへ曲げ、
        // デッキ縁(215°付近)の一段高い床から流し込む（段差で逆流防止）
        {
            Vector3 p1 = new Vector3(-0.45f, 0.895f, 0), p2 = new Vector3(-0.43f, 0.865f, -0.13f);
            Vector3 mid = (p1 + p2) * 0.5f;
            Quaternion rot = Quaternion.LookRotation(p2 - p1);
            Prim(PrimitiveType.Cube, g, "ExitChute1", mid, rot.eulerAngles, new Vector3(0.16f, 0.02f, 0.20f), trackMat);
            Prim(PrimitiveType.Cube, g, "ExitRail1", mid + rot * new Vector3(0.09f, 0.05f, 0),
                rot.eulerAngles, new Vector3(0.02f, 0.10f, 0.22f), railMat);

            Vector3 p3 = new Vector3(-0.24f, 0.84f, -0.17f);
            Vector3 mid2 = (p2 + p3) * 0.5f;
            Quaternion rot2 = Quaternion.LookRotation(p3 - p2);
            Prim(PrimitiveType.Cube, g, "ExitChute2", mid2, rot2.eulerAngles, new Vector3(0.16f, 0.02f, 0.26f), trackMat);
            Prim(PrimitiveType.Cube, g, "ExitRail2", mid2 + rot2 * new Vector3(0.09f, 0.05f, 0),
                rot2.eulerAngles, new Vector3(0.02f, 0.10f, 0.28f), railMat);
        }
    }

    // ---- ジグザグ・スイッチバックレーン（マーブルラン式チャッカー） ----
    // デッキ第3ゲート(+X)から折り返しレーンで降下。折り返しの跳ね方で15/35ptチャッカーを
    // 通るかが決まる受動抽選。終端は回収床の上に開放（フェイルセーフ循環）
    static void BuildZigzag(Transform root)
    {
        var g = Group(root, "Zigzag");
        // 全レーンを同一鉛直面(z=0)内の純粋±X折り返しに: 落下時の横ズレが構造的に発生しない
        // 2段折り返し→2段目左端から開放空中落下でクルーンの鉢へ合流（多段抽選）。
        // 落下先が広い鉢なのでシャフトアーチも壁衝突も構造的に起きない
        Ramp(g, "Zig1", new Vector3(0.23f, 0.80f, 0), new Vector3(0.48f, 0.74f, 0), 0.16f);
        Ramp(g, "Zig2", new Vector3(0.68f, 0.66f, 0), new Vector3(0.30f, 0.575f, 0), 0.16f);
        // 折り返しの受け壁（右のみ。左端は開放でクルーンへ落ちる）
        Prim(PrimitiveType.Cube, g, "ZigWallR", new Vector3(0.70f, 0.65f, 0), Vector3.zero,
            new Vector3(0.02f, 0.50f, 0.18f), railMat);
        Trigger(g, "Chakker_15", new Vector3(0.52f, 0.67f, 0), new Vector3(0.10f, 0.10f, 0.14f), 15);
        Trigger(g, "Chakker_35", new Vector3(0.24f, 0.50f, 0), new Vector3(0.12f, 0.14f, 0.14f), 35);
    }

    // ---- 風車プラットフォーム（分岐点） ----
    static void BuildWindmillPlatform(Transform root)
    {
        var g = Group(root, "WindmillPlatform");
        Prim(PrimitiveType.Cylinder, g, "Deck", new Vector3(0, 0.81f, 0), Vector3.zero,
            new Vector3(0.6f, 0.01f, 0.6f), trackMat);

        // リム壁（±Zの出口2箇所を開ける）
        const int n = 16;
        for (int i = 0; i < n; i++)
        {
            float a = i / (float)n * 360f;
            if (Mathf.Abs(Mathf.DeltaAngle(a, 90f)) < 25f || Mathf.Abs(Mathf.DeltaAngle(a, 270f)) < 25f) continue; // ゲートL/R
            if (Mathf.Abs(Mathf.DeltaAngle(a, 0f)) < 15f) continue; // ゲートB（ジグザグレーンへ）
            if (Mathf.Abs(Mathf.DeltaAngle(a, 214f)) < 26f) continue; // スパイラル入口（シュート床の段差で一方通行）
            float rad = a * Mathf.Deg2Rad;
            Prim(PrimitiveType.Cube, g, $"Rim_{i}", new Vector3(0.3f * Mathf.Cos(rad), 0.87f, 0.3f * Mathf.Sin(rad)),
                new Vector3(0, -a + 90f, 0), new Vector3(0.13f, 0.12f, 0.02f), railMat);
        }

        // 風車（4枚羽根・回転）
        var mill = new GameObject("Windmill");
        mill.transform.SetParent(g);
        mill.transform.position = new Vector3(0, 0.86f, 0);
        var rot = mill.AddComponent<Rotator>();
        rot.degreesPerSecond = 90f;
        for (int i = 0; i < 4; i++)
            Prim(PrimitiveType.Cube, mill.transform, $"Paddle_{i}", new Vector3(0, 0.86f, 0),
                new Vector3(0, i * 90f, 0), new Vector3(0.03f, 0.08f, 0.6f), accentMat); // デッキ全域を掃く

        // 出口フラップゲート（交互開閉）＋通過スコア
        MakeGate(g, "GateL", new Vector3(0, 0.82f, 0.3f), 0f, 20);
        MakeGate(g, "GateR", new Vector3(0, 0.82f, -0.3f), 0.5f, 10);
        // 第3ゲート(+X): ジグザグレーンへ。内側(-X)へ倒れるスロープ開放
        var flapB = Prim(PrimitiveType.Cube, g, "GateB", new Vector3(0.3f, 0.82f, 0),
            new Vector3(0, 90f, 0), new Vector3(0.24f, 0.16f, 0.02f), accentMat);
        var oscB = flapB.AddComponent<Oscillator>();
        oscB.axis = Vector3.right; // ローカルX=ワールド-Z → -85で内側倒し
        oscB.angleA = 0f;
        oscB.angleB = -85f;
        oscB.period = 3f;
        oscB.phase = 0.25f;
        Trigger(g, "GateB_Score", new Vector3(0.40f, 0.78f, 0), new Vector3(0.06f, 0.14f, 0.20f), 5);
    }

    static void MakeGate(Transform parent, string name, Vector3 pos, float phase, int points)
    {
        // 中心をデッキ面に置く: 閉=上半分が壁、開(85°)=床と面一の橋
        var flap = Prim(PrimitiveType.Cube, parent, name, pos, Vector3.zero, new Vector3(0.24f, 0.16f, 0.02f), accentMat);
        var osc = flap.AddComponent<Oscillator>();
        osc.axis = Vector3.right;
        osc.angleA = 0f;
        osc.angleB = -Mathf.Sign(pos.z) * 85f; // 内側に倒す=半開でも登れるスロープになり通過可能
        osc.period = 3f;
        osc.phase = phase;
        // ゲートを実際に通過した先（シュート上）で加点。閉ゲート待ちの再トリガー防止
        Trigger(parent, name + "_Score", new Vector3(pos.x, 0.77f, Mathf.Sign(pos.z) * 0.45f),
            new Vector3(0.2f, 0.14f, 0.06f), points);
    }

    // ---- シュート（+Z: シーソー振り分け / -Z: クルーン直行） ----
    static void BuildChutes(Transform root)
    {
        var g = Group(root, "Chutes");
        // +Z: ランプ→シーソーへ落下。シーソーの傾き次第でクルーン行き(-Z側)か回収直行(+Z側)
        Ramp(g, "L1", new Vector3(0, 0.80f, 0.32f), new Vector3(0, 0.70f, 0.46f), 0.18f);
        var seesaw = Prim(PrimitiveType.Cube, g, "Seesaw", new Vector3(0, 0.62f, 0.45f),
            Vector3.zero, new Vector3(0.24f, 0.02f, 0.3f), accentMat);
        var osc = seesaw.AddComponent<Oscillator>();
        osc.axis = Vector3.right;
        osc.angleA = -14f;
        osc.angleB = 14f;
        osc.period = 5f;
        // 側面ガード（シーソーと一緒に動く）: 横こぼれ→場外を防ぐ
        foreach (float s in new[] { -1f, 1f })
            Prim(PrimitiveType.Cube, seesaw.transform, s < 0 ? "GuardL" : "GuardR",
                new Vector3(s * 0.13f, 0.66f, 0.45f), Vector3.zero, new Vector3(0.02f, 0.08f, 0.3f), accentMat);
        // +Z側バックボード: シーソー/閉じ際フラップに弾かれたボールの場外飛び出しを回収槽へ落とす
        Prim(PrimitiveType.Cube, g, "SeesawBackboard", new Vector3(0, 0.85f, 0.72f), Vector3.zero,
            new Vector3(0.5f, 0.5f, 0.02f), railMat);

        // -Z: ランプ終端からクルーンすり鉢へ真上から落とし込む
        Ramp(g, "R1", new Vector3(0, 0.80f, -0.32f), new Vector3(0, 0.68f, -0.45f), 0.18f);
    }

    // ---- クルーン（すり鉢） ----
    static void BuildKuruun(Transform root)
    {
        var g = Group(root, "Kuruun");
        FunnelMesh(g, "KuruunFunnel", new Vector3(0, KuruunY - KuruunDrop, 0)); // Blender製の滑らかなすり鉢
        // 外周壁（シュートは真上から落とすので壁越え不要）
        WallRing(g, KuruunOuterR + 0.02f, KuruunY + 0.06f, 0.15f, 24);
        // 中央かき混ぜ棒: 穴の縁で組んだアーチを崩す（Din Don式）
        var stirrer = new GameObject("Stirrer");
        stirrer.transform.SetParent(g);
        stirrer.transform.position = new Vector3(0, 0.36f, 0);
        stirrer.AddComponent<Rotator>().degreesPerSecond = 12f; // 速いとボールを遠心軌道に乗せてしまう
        for (int i = 0; i < 3; i++)
        {
            // 中心の穴を塞がない放射状アーム（r0.14〜0.34、斜面に沿って15°傾け）
            float a = i * 120f * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            Quaternion rot = Quaternion.LookRotation(dir) * Quaternion.Euler(-15f, 0, 0);
            Prim(PrimitiveType.Cube, stirrer.transform, $"StirArm_{i}", dir * 0.26f + Vector3.up * 0.37f,
                rot.eulerAngles, new Vector3(0.02f, 0.06f, 0.16f), accentMat); // 穴の縁より外側のみ
        }
    }

    // ---- ルーレット盤（回転皿＋スコアセクター） ----
    static void BuildRouletteDisc(Transform root)
    {
        var g = Group(root, "RouletteDisc");
        var disc = Prim(PrimitiveType.Cylinder, g, "Disc", new Vector3(0, 0.16f, 0), Vector3.zero,
            new Vector3(0.48f, 0.01f, 0.48f), accentMat); // すり鉢内縁の下端(0.30)との隙間>ボール径を確保
        var rot = disc.AddComponent<Rotator>();
        rot.degreesPerSecond = 45f;
        int[] pts = { 100, 50, 30 };
        for (int i = 0; i < 3; i++)
        {
            float a = i * 120f * Mathf.Deg2Rad;
            Vector3 p = new Vector3(0.1f * Mathf.Cos(a), 0.20f, 0.1f * Mathf.Sin(a));
            Prim(PrimitiveType.Cylinder, disc.transform, $"Peg_{i}", p, Vector3.zero,
                new Vector3(0.03f, 0.03f, 0.03f), railMat);
            // 判定は固定側・外周寄りに離して互いに重ねない（中央で3つ同時ヒットすると抽選にならない）
            Vector3 sp = new Vector3(0.13f * Mathf.Cos(a), 0.22f, 0.13f * Mathf.Sin(a));
            Trigger(g, $"Sector_{pts[i]}", sp, new Vector3(0.12f, 0.12f, 0.12f), pts[i]);
        }
    }

    // ---- 回収すり鉢＋リフト ----
    static void BuildBasinAndLift(Transform root)
    {
        var g = Group(root, "Basin");
        Prim(PrimitiveType.Cube, g, "GroundPlate", new Vector3(0, -0.01f, 0), Vector3.zero,
            new Vector3(2f, 0.02f, 2f), trackMat);
        // 回収床: +X側へ8°傾いた平床。V字ガイドで排出口(+X)へ自然に集める（中央穴方式は
        // ルーレット盤の下空間と両立しないため廃止。実機同様ボールは物理で転がって回収される）
        Prim(PrimitiveType.Cylinder, g, "BasinPlate", new Vector3(0, 0.10f, 0),
            new Vector3(0, 0, -8f), new Vector3(1.44f, 0.01f, 1.44f), trackMat); // -8°=+X側が低い
        // ガイド壁は不要: 傾いた床上では円形の外周壁自体が「谷」になり、
        // ボールは壁沿いに最低点(+X)の排出口へ自然に滑る
        WallRing(g, BasinOuterR + 0.03f, 0.32f, 0.6f, 24, 0f, 7f); // 排出口（通過ボール中心は|z|<0.04に絞られる）
        // 排出口前の撹拌ローター: 出口に向かって両側から絞られたボールが組むアーチを崩し続ける
        var ds = new GameObject("DrainStirrer");
        ds.transform.SetParent(g);
        ds.transform.position = new Vector3(0.56f, 0.065f, 0);
        ds.transform.rotation = Quaternion.Euler(0, 0, -8f); // 床の傾きに沿わせる
        ds.AddComponent<Rotator>().degreesPerSecond = 25f;
        for (int i = 0; i < 2; i++)
            Prim(PrimitiveType.Cube, ds.transform, $"DrainArm_{i}", ds.transform.position,
                new Vector3(0, i * 90f, -8f), new Vector3(0.02f, 0.05f, 0.26f), accentMat);

        // 周回確定ゲート（排水口）
        var lap = Trigger(g, "LapGate", new Vector3(0.69f, 0.08f, 0), new Vector3(0.10f, 0.14f, 0.20f), 0);
        Object.DestroyImmediate(lap.GetComponent<ScoreZone>());
        lap.AddComponent<LapGate>();

        // リフト（基部トリガー: ここまではボールが物理で転がってくる）
        var liftGO = Trigger(g, "Lift", new Vector3(0.88f, 0.06f, 0), new Vector3(0.16f, 0.10f, 0.16f), 0);
        Object.DestroyImmediate(liftGO.GetComponent<ScoreZone>());
        var lift = liftGO.AddComponent<BallLift>();
        lift.speed = 0.9f; // 1.5倍スケールに合わせて搬送速度も増
        lift.waypoints = new Transform[]
        {
            Waypoint(liftGO.transform, "W0", new Vector3(0.9f, 0.10f, 0)),
            Waypoint(liftGO.transform, "W1", new Vector3(0.9f, SpiralTop + 0.18f, 0)),
            Waypoint(liftGO.transform, "W2", new Vector3(SpiralR, SpiralTop + 0.12f, 0)),
        };
        // リフトレール（見た目のみ・実機風の2本ガイド＋横ばり＋上部搬送レール）
        var rails = Group(g, "LiftRails");
        float topY = SpiralTop + 0.18f;
        foreach (float s in new[] { -1f, 1f })
        {
            // 垂直ガイドレール（昇降路の両脇）
            VisualBar(rails, s < 0 ? "GuideL" : "GuideR", new Vector3(0.9f, (0.05f + topY) * 0.5f, s * 0.07f),
                Vector3.zero, new Vector3(0.03f, topY - 0.05f, 0.03f));
            // 上部搬送レール（頂上→スパイラル入口へ）
            Vector3 p1 = new Vector3(0.9f, topY, s * 0.07f), p2 = new Vector3(SpiralR, SpiralTop + 0.12f, s * 0.07f);
            VisualBar(rails, s < 0 ? "TopRailL" : "TopRailR", (p1 + p2) * 0.5f,
                Quaternion.LookRotation(p2 - p1).eulerAngles, new Vector3(0.03f, 0.03f, Vector3.Distance(p1, p2)));
        }
        for (float y = 0.25f; y < topY; y += 0.35f) // 横ばり（ボール昇降路のすぐ背面）
            VisualBar(rails, $"Tie_{y:F2}", new Vector3(0.945f, y, 0), Vector3.zero, new Vector3(0.03f, 0.03f, 0.17f));
        VisualBar(rails, "Base", new Vector3(0.9f, 0.02f, 0), Vector3.zero, new Vector3(0.2f, 0.04f, 0.2f));
        // 回収トラフ: 外壁の完全に外側から始める（食い込むとポケットができて詰まる）
        Prim(PrimitiveType.Cube, rails, "TroughFloor", new Vector3(0.85f, -0.005f, 0), Vector3.zero, new Vector3(0.24f, 0.03f, 0.21f), trackMat); // 上面0.01=傾斜床の縁と面一
        Prim(PrimitiveType.Cube, rails, "TroughRailL", new Vector3(0.85f, 0.03f, 0.0975f), Vector3.zero, new Vector3(0.24f, 0.05f, 0.015f), railMat);
        Prim(PrimitiveType.Cube, rails, "TroughRailR", new Vector3(0.85f, 0.03f, -0.0975f), Vector3.zero, new Vector3(0.24f, 0.05f, 0.015f), railMat);
        Prim(PrimitiveType.Cube, rails, "TroughEnd", new Vector3(0.975f, 0.05f, 0), Vector3.zero, new Vector3(0.02f, 0.10f, 0.21f), railMat);
    }

    // ================= ヘルパー =================
    static Material MakeMat(string name, Color c)
    {
        string path = $"Assets/Materials/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c);
            AssetDatabase.CreateAsset(m, path);
        }
        return m;
    }

    static Transform Group(Transform root, string name)
    {
        var g = new GameObject(name).transform;
        g.SetParent(root);
        return g;
    }

    static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 euler, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = pos;
        go.transform.eulerAngles = euler;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (type == PrimitiveType.Cylinder)
        {
            // Cylinderの既定コライダはカプセル＝扁平円盤だと巨大な見えない球になる。凸メッシュに差し替え
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var mc = go.AddComponent<MeshCollider>();
            mc.convex = true;
            mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
        }
        go.GetComponent<Collider>().material = railPM;
        return go;
    }

    /// p1→p2 を結ぶ床＋両側レール付きランプ
    static void Ramp(Transform parent, string name, Vector3 p1, Vector3 p2, float width)
    {
        var g = Group(parent, name);
        Vector3 mid = (p1 + p2) * 0.5f;
        float len = Vector3.Distance(p1, p2);
        Quaternion rot = Quaternion.LookRotation(p2 - p1);
        Prim(PrimitiveType.Cube, g, "Floor", mid, rot.eulerAngles, new Vector3(width, 0.02f, len + 0.04f), trackMat);
        foreach (float s in new[] { -1f, 1f })
        {
            Vector3 railPos = mid + rot * new Vector3(s * (width * 0.5f + 0.01f), 0.05f, 0);
            Prim(PrimitiveType.Cube, g, s < 0 ? "RailL" : "RailR", railPos, rot.eulerAngles,
                new Vector3(0.02f, 0.1f, len + 0.04f), railMat);
        }
    }

    /// Blender製ファンネルメッシュ（Funnels.fbx）を配置。メッシュ底(内縁)がy=0基準
    static void FunnelMesh(Transform parent, string meshName, Vector3 pos)
    {
        Mesh mesh = null;
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath("Assets/Models/Funnels.fbx"))
            if (a is Mesh m && m.name == meshName) { mesh = m; break; }
        if (mesh == null) { Debug.LogError($"mesh not found: {meshName}"); return; }
        var go = new GameObject(meshName);
        go.transform.SetParent(parent);
        go.transform.position = pos;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = trackMat;
        var mc = go.AddComponent<MeshCollider>();
        mc.sharedMesh = mesh;
        mc.material = railPM;
    }

    static void WallRing(Transform parent, float r, float y, float h, int segs, float gapCenterDeg = -999f, float gapHalfDeg = 0f)
    {
        for (int i = 0; i < segs; i++)
        {
            float a = i / (float)segs * 360f;
            if (gapHalfDeg > 0f && Mathf.Abs(Mathf.DeltaAngle(a, gapCenterDeg)) < gapHalfDeg) continue; // 排出口
            float rad = a * Mathf.Deg2Rad;
            float w = 2f * Mathf.PI * r / segs + 0.02f;
            Prim(PrimitiveType.Cube, parent, $"Wall_{i}", new Vector3(r * Mathf.Cos(rad), y, r * Mathf.Sin(rad)),
                new Vector3(0, -a + 90f, 0), new Vector3(w, h, 0.02f), railMat);
        }
    }

    static GameObject Trigger(Transform parent, string name, Vector3 pos, Vector3 size, int points)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = pos;
        go.transform.localScale = size;
        go.GetComponent<Collider>().isTrigger = true;
        go.GetComponent<Renderer>().enabled = false;
        var z = go.AddComponent<ScoreZone>();
        z.points = points;
        if (points > 0) ScoreLabel(parent, name, pos + Vector3.up * 0.10f, points);
        return go;
    }

    /// ルートの点数マーカー（常時カメラ向き）
    static void ScoreLabel(Transform parent, string zoneName, Vector3 pos, int points)
    {
        var go = new GameObject(zoneName + "_Label");
        go.transform.SetParent(parent);
        go.transform.position = pos;
        var tm = go.AddComponent<TextMesh>();
        tm.text = $"{points}pt";
        tm.fontSize = 48;
        tm.characterSize = 0.02f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.color = new Color(1f, 0.85f, 0.2f);
        go.AddComponent<Billboard>();
    }

    /// コライダ無しの見た目専用バー（リフトレール等）
    static void VisualBar(Transform parent, string name, Vector3 pos, Vector3 euler, Vector3 scale)
    {
        var go = Prim(PrimitiveType.Cube, parent, name, pos, euler, scale, railMat);
        Object.DestroyImmediate(go.GetComponent<Collider>());
    }

    static Transform Waypoint(Transform parent, string name, Vector3 pos)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent);
        t.position = pos;
        return t;
    }
}
