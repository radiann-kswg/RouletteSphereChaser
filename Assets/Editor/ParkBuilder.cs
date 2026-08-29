using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

/// Tools > Build RouletteSphere Park (v2)
/// 多塔パーク型ボールコースター（Docs/DESIGN-v2.md）のビルダー。冪等。
///
/// **フェーズ8-2以降、このクラスは「解釈器」である**（2026-08-24）。
/// 配置のSSOTは `BlenderSources/ParkAssembly.blend`。ここは
///   Assets/Models/ParkAssembly.fbx        … 全メッシュ＋機能マーカーEmpty
///   Assets/Models/ParkAssembly.params.json … 名前 -> 付けるコンポーネントとパラメータ
/// を読んで、コライダとコンポーネントを付け直すだけ。**座標は一切計算しない。**
/// 塔の寸法・位置・角度を変えたいときは Blender 側を編集して Docs/export_park_assembly.py を回す。
public static class ParkBuilder
{
    const string ScenePath = "Assets/Scenes/ParkScene_v2.unity";
    const string FbxPath = "Assets/Models/ParkAssembly.fbx";
    const string ParamsPath = "Assets/Models/ParkAssembly.params.json";

    /// Blender ワールド -> Unity ワールドの唯一の規約 G: `unity = (bx, bz, by)`。
    /// FBX往復（Blender書き出し＋Unity取り込み）で180°ヨーが1回入るので、根で戻すとGになる。
    /// 2026-08-24実測: 全123インスタンスのワールドAABBが旧シーンと最大0.063mm一致。
    /// **ズレたときに直すのはここ1行だけ**（罠19のper-part規約はもう存在しない）。
    static readonly Quaternion RootFix = Quaternion.Euler(0f, 180f, 0f);

    /// 透過アクリルにするシェル。**選定は勘ではなく実測**——`Docs/camera_coverage.json` の
    /// `blockers`（何に遮蔽されたかの回数）で上位に出たメッシュを並べてある。
    /// コンセプト「中が見えることを機構の見栄えより優先する」（DESIGN-v2 1.0章）の実装。
    public const string SeeThroughLayer = "SeeThrough";
    public static readonly System.Collections.Generic.HashSet<string> SeeThroughMats = new()
    {
        "TowerA_CollectorFunnel",     // Cam_A_GrandRoulette の遮蔽 770/1234 = 最大の犯人
        "TowerA_MiniKuruun",          // Cam_A_Overview 2328
        "TowerA_MiniRouletteBowl",    // 同 632
        "TowerA_GrandRouletteBowl",   // Cam_A_GrandRoulette 216
        "TowerB_PachiBoard",          // Cam_B_Pachinko_E/W の最大
        "TowerG_NumaKuruun",          // 沼クルーン（User指名）
        "TowerH_KarakoDish",          // Cam_H_Garapon 88
        "TowerC_Zigzag",              // Cam_C_Zigzag_N/S の最大（46/51）
        "TowerC_CatchTurn",           // 同 46/27
        "TowerG_MergeTray",           // Cam_G_Numa_W 12 / Cam_H_Garapon 29
        "TowerB_CatchTray",           // CatchChute。盤を透かした後の Cam_B_Pachinko_E/W の最大（65/73）
    };

    [System.Serializable]
    class MeshRow { public string name, path, collider, material; public Color rgb; }

    [System.Serializable]
    class MarkerRow
    {
        public string name, path, kind;         // kind: T / ROT / OSC / LIFT / LAP / LBL
        public int points, grantMultiplier;
        public Vector3 axis;
        public float dps, a, b, period, phase, speed, releaseJitter;
        public Vector3[] waypoints;
        public string text; public float fontSize; public bool billboard; public Color rgb;
        public float[] m;                        // Unity座標での localToWorldMatrix 上位3行（12要素）
        public Matrix4x4 Matrix
        {
            get
            {
                var x = Matrix4x4.identity;
                x.m00 = m[0]; x.m01 = m[1]; x.m02 = m[2]; x.m03 = m[3];
                x.m10 = m[4]; x.m11 = m[5]; x.m12 = m[6]; x.m13 = m[7];
                x.m20 = m[8]; x.m21 = m[9]; x.m22 = m[10]; x.m23 = m[11];
                return x;
            }
        }
    }

    [System.Serializable]
    class ParkParams { public MeshRow[] meshes; public MarkerRow[] markers; }

    [MenuItem("Tools/Build RouletteSphere Park (v2)")]
    public static void Build()
    {
        if (SceneManager.GetActiveScene().path != ScenePath)
        {
            EditorSceneManager.SaveOpenScenes();
            if (System.IO.File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath);
            else EditorSceneManager.SaveScene(
                EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single), ScenePath);
        }

        var old = GameObject.Find("Park");
        if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject("Park").transform;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (prefab == null) { Debug.LogError("[ParkBuilder] " + FbxPath + " が無い。Docs/export_park_assembly.py を先に回すこと"); return; }
        var asm = (GameObject)Object.Instantiate(prefab, Vector3.zero, RootFix, root);

        var pp = JsonUtility.FromJson<ParkParams>(System.IO.File.ReadAllText(ParamsPath));

        // FBX側の子をオブジェクト名で引けるように
        var src = new Dictionary<string, Transform>();
        foreach (var t in asm.GetComponentsInChildren<Transform>(true))
            if (t != asm.transform) src[t.name] = t;

        // 旧シーンと同じ階層を unity_path から復元する（比較・デバッグのため）
        var nodes = new Dictionary<string, Transform> { { "Park", root } };

        int placed = 0, triggers = 0, pivots = 0, labels = 0, drifted = 0;

        // ---- 1) 機能マーカー。回転体は必ず「スケール1のピボット」にする（罠46） ----
        // 回転体を先に作る: 倍率ラベル等がピボットの子になるので、親が先に nodes に載っていないと
        // 同名のダミーノードが二重にできる
        var ordered = new List<MarkerRow>();
        foreach (var m in pp.markers) if (m.kind == "ROT" || m.kind == "OSC") ordered.Add(m);
        foreach (var m in pp.markers) if (m.kind != "ROT" && m.kind != "OSC") ordered.Add(m);

        foreach (var m in ordered)
        {
            // マーカーの姿勢は params.json の行列を使う。
            // FBXのEmptyノードはUnityインポータが**負スケール×100**で表現するため（2026-08-24実測）、
            // 取り込んだEmptyのrotation/scaleは信用できない。位置だけは正しく入るので突き合わせに使う。
            var mtx = m.Matrix;
            if (src.TryGetValue(m.name, out var s))
            {
                float drift = Vector3.Distance(s.position, mtx.GetColumn(3));
                if (drift > 1e-3f)
                {
                    Debug.LogError($"[ParkBuilder] 規約ズレ: {m.path} JSON位置とFBX位置が {drift:F4}m 食い違う。" +
                                    "RootFix と Docs/export_park_assembly.py の G が一致していない");
                    drifted++;
                }
            }
            else Debug.LogWarning("[ParkBuilder] marker欠落: " + m.name);

            var go = new GameObject(Leaf(m.path));
            go.transform.SetParent(Node(nodes, root, Dir(m.path)), false);
            go.transform.SetPositionAndRotation(mtx.GetColumn(3), mtx.rotation);
            nodes[m.path] = go.transform;

            switch (m.kind)
            {
                case "T":
                    Box(go, mtx);
                    var sz = go.AddComponent<ScoreZone>();
                    sz.points = m.points;
                    sz.grantMultiplier = m.grantMultiplier;
                    triggers++;
                    break;

                case "ROT":
                    KinematicBody(go);
                    var rot = go.AddComponent<Rotator>();
                    rot.axis = m.axis;
                    rot.degreesPerSecond = m.dps;
                    pivots++;
                    break;

                case "OSC":
                    KinematicBody(go);
                    var osc = go.AddComponent<Oscillator>();
                    osc.axis = m.axis; osc.angleA = m.a; osc.angleB = m.b; osc.period = m.period; osc.phase = m.phase;
                    pivots++;
                    break;

                case "LIFT":
                    Box(go, mtx);
                    var lift = go.AddComponent<BallLift>();
                    lift.speed = m.speed;
                    lift.releaseJitter = m.releaseJitter;
                    var wps = new List<Transform>();
                    for (int wi = 0; wi < m.waypoints.Length; wi++)
                    {
                        var w = new GameObject("W" + wi).transform;
                        w.SetParent(go.transform);
                        w.position = m.waypoints[wi];
                        wps.Add(w);
                    }
                    lift.waypoints = wps.ToArray();
                    break;

                case "LAP":
                    Box(go, mtx);
                    go.AddComponent<LapGate>();
                    break;

                case "LBL":
                    var tmp = go.AddComponent<TMPro.TextMeshPro>();
                    var fa = PenchantFont();
                    if (fa != null) tmp.font = fa;
                    tmp.text = m.text;
                    tmp.fontSize = m.fontSize;
                    tmp.alignment = TMPro.TextAlignmentOptions.Center;
                    tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                    // **盤面に印字するラベル（GateLabel = 非ビルボード）は盤の色で決まる**。
                    // 得点ゲートを白（SCORE `#E8F1FA`）にしたので、`park_labels.json` の金色のままだと
                    // 白地に白で読めない（User報告 2026-08-24）。抽選盤と同じ藍（DECK `#33407F`）に上書きする。
                    // 宙に浮くDropLabel（ビルボード）は暗い背景の上なので JSON の色をそのまま使う。
                    tmp.color = m.billboard ? m.rgb : GateLabelColor;
                    tmp.rectTransform.sizeDelta = new Vector2(0.6f, 0.2f);
                    tmp.fontSharedMaterial = PenchantCullBack();   // 深度テスト＋背面カリング（壁の裏は見えない）
                    if (m.billboard) go.AddComponent<Billboard>();
                    labels++;
                    break;
            }
        }

        // ---- 2) メッシュ。マテリアルと非凸MeshCollider＋低摩擦を付けて所定の階層へ移す ----
        foreach (var mr in pp.meshes)
        {
            if (!src.TryGetValue(mr.name, out var t)) { Debug.LogWarning("[ParkBuilder] mesh欠落: " + mr.name); continue; }

            // パスが回転ピボットと同名なら、そのピボットの子にする（ピボット側はスケール1のまま）
            bool underPivot = nodes.ContainsKey(mr.path);
            var parent = underPivot ? nodes[mr.path] : Node(nodes, root, Dir(mr.path));
            t.SetParent(parent, true);                       // ワールド変換を保ったまま移す
            t.name = underPivot ? Leaf(mr.path) + "_Mesh" : Leaf(mr.path);

            var r = t.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = Mat(mr.material, mr.rgb);
            // 透過アクリルのシェルは「見えている」扱いにする（死角の実測でカウントしない）。
            // 物理は素通しにしないので、レイヤは描画とCameraCoverageのためだけ。
            if (SeeThroughMats.Contains(mr.material))
            {
                int layer = LayerMask.NameToLayer(SeeThroughLayer);
                if (layer >= 0) t.gameObject.layer = layer;
            }
            if (mr.collider == "mesh")
            {
                var mf = t.GetComponent<MeshFilter>();
                var mc = t.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mc.material = RailPM;
            }
            placed++;
        }

        Object.DestroyImmediate(asm);   // 空になったFBXルートは捨てる

        var camGroup = BuildFixedCameras(root);
        BuildRoamCameras(camGroup);
        BuildSpawnerAndCamera(root);
        if (GameObject.Find("CameraDirector") == null)
            new GameObject("CameraDirector").AddComponent<CameraDirector>();

        // 罠20: 生成直後はエディタ時コライダが同期していない。検証レイキャストが誤診する
        Physics.SyncTransforms();
        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[ParkBuilder] built from ParkAssembly.fbx: meshes={placed} triggers={triggers} pivots={pivots} labels={labels} drift={drifted}");
    }

    // ---- 小道具 ----

    static string Dir(string path) { int i = path.LastIndexOf('/'); return i < 0 ? "Park" : path.Substring(0, i); }
    static string Leaf(string path) { int i = path.LastIndexOf('/'); return i < 0 ? path : path.Substring(i + 1); }

    /// コース側コライダは低摩擦（AGENTS 3章-9）
    static PhysicsMaterial _railPM;
    static PhysicsMaterial RailPM
    {
        get
        {
            if (_railPM != null) return _railPM;
            _railPM = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/Materials/RailPM.asset");
            if (_railPM == null)
            {
                _railPM = new PhysicsMaterial("Rail") { dynamicFriction = 0.05f, staticFriction = 0.05f, bounciness = 0.1f };
                AssetDatabase.CreateAsset(_railPM, "Assets/Materials/RailPM.asset");
            }
            return _railPM;
        }
    }

    /// 役割ごとのマテリアル（`Docs/DESIGN-materials.md` 2章）。無ければURP/Litで新規作成する
    static Material Mat(string name, Color c)
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

    /// unity_path の中間ノードを必要に応じて作る
    static Transform Node(Dictionary<string, Transform> nodes, Transform root, string path)
    {
        if (nodes.TryGetValue(path, out var t)) return t;
        var g = new GameObject(Leaf(path)).transform;
        g.SetParent(Node(nodes, root, Dir(path)), false);
        nodes[path] = g;
        return g;
    }

    /// トリガー箱。行列のスケール成分がそのまま箱の寸法（旧: 1辺1のCube×localScale と同値）
    static void Box(GameObject go, Matrix4x4 mtx)
    {
        go.transform.localScale = mtx.lossyScale;
        go.AddComponent<BoxCollider>().isTrigger = true;
    }

    /// 回転体の土台。キネマティックRigidbody必須（罠14: 高速球にはContinuousSpeculative）
    static void KinematicBody(GameObject go)
    {
        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    static void BuildSpawnerAndCamera(Transform root)
    {
        var spawner = new GameObject("BallSpawner").AddComponent<BallSpawner>();
        spawner.transform.SetParent(root);
        spawner.transform.position = new Vector3(-1.0f, 1.6f, 1.0f);
        spawner.ballPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/LotteryBall.prefab");
        spawner.count = 36;   // DESIGN-v2 6章フェーズ7の負荷検証水準
        spawner.interval = 1f;
        spawner.characterSkin = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/BallSkins_Sample.png");

        var cam = Camera.main;
        if (cam == null) return;
        cam.transform.position = new Vector3(18f, 12f, 15f);
        cam.transform.LookAt(new Vector3(1f, 4f, 0));
        cam.farClipPlane = 100f;
        var follow = cam.GetComponent<FollowCamera>();
        if (follow == null) follow = cam.gameObject.AddComponent<FollowCamera>();
        // 既存コンポーネントのシリアライズ値に引きずられないよう毎回明示する
        follow.distance = 0.55f;
        follow.height = 0.28f;
        follow.smoothTime = 0.22f;   // 速度先読みがあるので滑らかさ寄りでよい（BallCamRig参照）
        var hud = cam.GetComponent<BallHUD>();
        if (hud == null) hud = cam.gameObject.AddComponent<BallHUD>();
        hud.font = PenchantFont();
        hud.followCam = cam.GetComponent<FollowCamera>();
    }

    // ---- PenchantManufacture 書体（得点表示・HUD共通。TMP SDF） ----
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
            var srcFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/PenchantManufacture.otf");
            _penchantFont = TMPro.TMP_FontAsset.CreateFontAsset(srcFont, 64, 6,
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

    /// 盤面印字ラベルの色。得点ゲートが白（DESIGN-materials 2章 SCORE）なので、
    /// 抽選盤と同じ藍（DECK `#33407F`）で刷る。**盤の色を変えたらここも見直すこと。**
    static readonly Color GateLabelColor = new Color(0.200f, 0.251f, 0.498f, 1f);

    /// 抽選機ごとの定点カメラ（既定オフ）。見る/魅せるための道具なので配置SSOTの対象外＝ここに残す。
    /// **周回速度・振り幅・担当範囲・表示名は `CameraCoverage.Rigs` の1行**にまとめてある。
    /// ここに置くのは位置・注視点・FOVだけ——南北/東西のミラーをループで表していて、
    /// 表に展開すると対になる2台が別々に動かせてしまうため。
    static Transform BuildFixedCameras(Transform root)
    {
        var g = new GameObject("Cameras").transform;
        g.SetParent(root);
        void Cam(string n, Vector3 pos, Vector3 look, float fov)
        {
            var rig = CameraCoverage.Rigs[n];
            var go = new GameObject("Cam_" + n);
            go.transform.SetParent(g);
            go.transform.position = pos;
            go.transform.LookAt(look);
            var c = go.AddComponent<Camera>();
            c.fieldOfView = fov;
            c.farClipPlane = 60f;
            c.depth = -10f;      // MainCameraより後ろ（有効化しても既定表示を奪わない）
            c.enabled = false;   // 既定オフ（CameraDirectorが1台ずつ点ける）
            // 注視点＝機構の中心。半径・高さ・開始角は配置から自動取得される。
            // focusRadius は担当グループの実バウンズから決め、死角の実測（CameraCoverage）の母数になる
            var orb = go.AddComponent<OrbitCamera>();
            orb.pivot = look;
            orb.degreesPerSecond = rig.orbitDps;
            orb.focusRadius = FocusRadius(root, rig, look);
            orb.elevationAmplitude = rig.elevationAmp;
            orb.elevationPeriod = 17f;
            orb.azimuthAmplitude = rig.azimuthAmp;

            // 画角を担当範囲に合わせる。**死角の最大要因は「画角に入っていない」だった**（実測:
            // 大ルーレット76% / ガラポン83% が画角外）ので、focusRadius が必ず収まるFOVにする
            float dist = Vector3.Distance(pos, look);
            if (dist > 0.01f)
            {
                float need = 2f * Mathf.Atan(Mathf.Min(orb.focusRadius / dist, 3f)) * Mathf.Rad2Deg;
                c.fieldOfView = Mathf.Clamp(Mathf.Max(fov, need * 1.05f), fov, 88f);
            }
        }
        Cam("A_Overview", new Vector3(0f, 10.6f, -9.5f), new Vector3(0f, 8.6f, 0f), 50f);
        Cam("A_GrandRoulette", new Vector3(0f, 6.9f, -4.4f), new Vector3(0f, 5.9f, 0f), 45f);
        Cam("H_Garapon", new Vector3(0f, 4.9f, -4.2f), new Vector3(0f, 4.0f, 0f), 45f);
        for (int s = 0; s < 2; s++)
        {
            float sg = s == 0 ? -1f : 1f;
            string sfx = s == 0 ? "S" : "N";
            Cam("F_JPSpinner_" + sfx, new Vector3(0f, 7.2f, sg * 8.6f), new Vector3(0f, 6.5f, sg * 5f), 42f);
            Cam("E_PocketDisc_" + sfx, new Vector3(-1.05f, 4.05f, sg * 7.0f), new Vector3(1.45f, 1.6f, sg * 5f), 46f);
            Cam("C_Zigzag_" + sfx, new Vector3(2.6f, 4.4f, sg * 8.4f), new Vector3(2.6f, 3.6f, sg * 5f), 55f);
        }
        for (int s = 0; s < 2; s++)
        {
            float sg = s == 0 ? 1f : -1f;
            string sfx = s == 0 ? "E" : "W";
            Cam("G_Numa_" + sfx, new Vector3(sg * 5f, 6.4f, -4.8f), new Vector3(sg * 5f, 5.4f, 0f), 50f);
            // 盤面はZ面（法線±Z）。**正面（-Z側）に置いて方位は振り子**（AzimuthAmp）にする。
            // 注視点は盤(y3.53)とステップチャッカー(y2.39)の中間。周回させると裏に回って死角0.88になった
            Cam("B_Pachinko_" + sfx, new Vector3(sg * 7.15f, 3.7f, -3.4f), new Vector3(sg * 7.15f, 3.0f, 0.2f), 50f);
            Cam("D_Kuruun_" + sfx, new Vector3(sg * 5f, 2.6f, -3.4f), new Vector3(sg * 5f, 1.8f, 0f), 50f);
        }
        Cam("DrainStation", new Vector3(11.2f, 1.5f, -3.2f), new Vector3(11.2f, 0.3f, 0f), 50f);
        return g;
    }

    /// カメラが「映すべき」半径。担当グループ（`CameraCoverage.Rigs` の `groups`）の実メッシュが
    /// 注視点からどこまで広がっているかで決める。死角の実測はこの球の中に居た球だけを母数にする。
    static float FocusRadius(Transform root, CameraCoverage.Rig rig, Vector3 look)
    {
        float r = 0f;
        foreach (var name in rig.groups)
        {
            var go = root.Find(name);
            if (go == null) continue;
            foreach (var rend in go.GetComponentsInChildren<Renderer>(true))
            {
                if (rend.GetComponent<TMPro.TextMeshPro>() != null) continue;
                r = Mathf.Max(r, Vector3.Distance(look, rend.bounds.center) + rend.bounds.extents.magnitude);
            }
        }
        // 既定の機構スケール(2.5m)で頭打ち。塔全体を担当する全景系だけ focusCap で広げる
        return Mathf.Clamp(r, 0.8f, rig.focusCap);
    }

    /// 演出用のローミングカメラ（User要望 2026-08-24）。
    /// **抽選機チャンネル4台**（数十秒ごとに別の抽選機へ乗り換える）と
    /// **ボールチャンネル4台**（数十秒ごとに別のボールへ乗り換える）を別建てで用意する。
    /// デモプレイ演出のショット源であり、ソークでは脱線した球を拾う目にもなる。
    static void BuildRoamCameras(Transform g)
    {
        const int count = 4;

        Camera NewCam(string name, Vector3 pos, float fov)
        {
            var go = new GameObject(name);
            go.transform.SetParent(g);
            go.transform.position = pos;
            var c = go.AddComponent<Camera>();
            c.fieldOfView = fov;
            c.farClipPlane = 60f;
            c.depth = -10f;
            c.enabled = false;   // CameraDirector が1台ずつ点ける
            return c;
        }

        for (int i = 0; i < count; i++)
        {
            var mech = NewCam("Cam_Mech_" + (i + 1), new Vector3(0f, 6f, -6f), 45f).gameObject
                       .AddComponent<RandomFixedCamera>();
            mech.minHold = 20f; mech.maxHold = 40f;
            mech.startOffset = i * (30f / count);   // 台ごとに位相をずらして同時切替を避ける

            var ball = NewCam("Cam_Ball_" + (i + 1), new Vector3(0f, 6f, -6f), 45f).gameObject
                       .AddComponent<RandomFollowCamera>();
            ball.minHold = 20f; ball.maxHold = 40f;
            ball.startOffset = 10f + i * (30f / count);
        }

        // Display 1 に出す親カメラ。上の8チャンネルから自動で選んで映す（計9台構成）
        NewCam("Cam_Mix", new Vector3(0f, 6f, -6f), 45f).gameObject.AddComponent<RandomMixCamera>();
    }
}
