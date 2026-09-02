using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// 定点カメラの「死角」を実測する計測器（ソーク中に常駐）。
///
/// 死角の定義を**ボール基準**にしているのが要点。メッシュ表面のサンプルだと裏面が常に見えないので
/// 「見えない＝死角」の判定にならない。ここでは
///   担当機構の範囲にボールが居た瞬間を母数にして、
///     (a) 画角の外            … 画角不足の死角
///     (b) 画角内だが遮蔽された … 手前の造形に隠れる死角
///     (c) 見えた
/// を数える。(a)+(b) が多い台だけオービットさせればよい。
public class CameraCoverage : MonoBehaviour
{
	public float sampleInterval = 0.25f;
	public string outPath = "Docs/camera_coverage.json";

	/// 定点カメラ1台ぶんの設定。
	/// **カメラを足す／振る舞いを変えるときに触るのはこの表だけ**（以前は同じキーの辞書が6つに散っていた）。
	/// 位置・注視点・FOVだけは南北/東西のミラーを崩さないよう `ParkBuilder.BuildFixedCameras` のループに残してある。
	public class Rig
	{
		/// 担当する Park 直下グループ。死角の母数と、HUDの通過ログの絞り込みに使う
		public string[] groups;
		/// HUDに出す機構名。**PenchantManufacture書体はCJK未収録**なので英名で持つ（罠53）
		public string display;
		/// 周回速度[deg/s]。**0 なら回さない**。一周25〜45秒＝観賞に耐える速さ。
		/// 死角の有無は `Docs/camera_coverage.json`（`Tools > Run Soak` が実測）を見て決める
		public float orbitDps;
		/// 方位の振り幅[deg]。0＝360°周回、>0＝配置時の方位を中心に往復。
		/// **平らな盤面は正面からしか中が見えない**ので、周回させると一周のほとんどが裏側＝死角になる
		/// （パチンコ盤は周回のままだと死角0.88だった。2026-08-24実測）
		public float azimuthAmp;
		/// 見下ろし角の振り幅[m]。方位を回すだけでは**すり鉢やトラフの中は永久に見えない**
		public float elevationAmp;
		/// 担当範囲の半径の上限[m]。既定は機構スケール、全景系だけ広く取る
		public float focusCap = 2.5f;
	}

	public static readonly Dictionary<string, Rig> Rigs = new()
	{
		["A_Overview"] = new Rig { groups = new[] { "TowerA", "TowerA23" }, display = "Tower A - Spiral Overview", orbitDps = 5f, focusCap = 6f },
		["A_GrandRoulette"] = new Rig { groups = new[] { "TowerA23" }, display = "Tower A - Grand Roulette", orbitDps = 9f, elevationAmp = 0.8f },
		["H_Garapon"] = new Rig { groups = new[] { "TowerH" }, display = "Tower H - Garapon", orbitDps = 12f, elevationAmp = 0.7f },
		["F_JPSpinner_S"] = new Rig { groups = new[] { "TowerF_S" }, display = "Tower F - JP Spinner South", orbitDps = 12f, elevationAmp = 0.6f },
		["F_JPSpinner_N"] = new Rig { groups = new[] { "TowerF_N" }, display = "Tower F - JP Spinner North", orbitDps = 12f, elevationAmp = 0.6f },
		["E_PocketDisc_S"] = new Rig { groups = new[] { "TowerE_S" }, display = "Tower E - Pocket Disc South", orbitDps = 12f, elevationAmp = 0.6f },
		["E_PocketDisc_N"] = new Rig { groups = new[] { "TowerE_N" }, display = "Tower E - Pocket Disc North", orbitDps = 12f, elevationAmp = 0.6f },
		["C_Zigzag_S"] = new Rig { groups = new[] { "TowerC_S" }, display = "Tower C - Zigzag South", orbitDps = 10f, elevationAmp = 0.6f },
		["C_Zigzag_N"] = new Rig { groups = new[] { "TowerC_N" }, display = "Tower C - Zigzag North", orbitDps = 10f, elevationAmp = 0.6f },
		["G_Numa_E"] = new Rig { groups = new[] { "TowerG_E" }, display = "Tower G - Numa Kuruun East", orbitDps = 12f, elevationAmp = 0.6f },
		["G_Numa_W"] = new Rig { groups = new[] { "TowerG_W" }, display = "Tower G - Numa Kuruun West", orbitDps = 12f, elevationAmp = 0.6f },
		["B_Pachinko_E"] = new Rig { groups = new[] { "TowerB_E" }, display = "Tower B - Pachinko East", orbitDps = 14f, azimuthAmp = 38f, elevationAmp = 0.5f },
		["B_Pachinko_W"] = new Rig { groups = new[] { "TowerB_W" }, display = "Tower B - Pachinko West", orbitDps = 14f, azimuthAmp = 38f, elevationAmp = 0.5f },
		["D_Kuruun_E"] = new Rig { groups = new[] { "TowerD_E" }, display = "Tower D - Kuruun East", orbitDps = 14f, elevationAmp = 0.5f },
		["D_Kuruun_W"] = new Rig { groups = new[] { "TowerD_W" }, display = "Tower D - Kuruun West", orbitDps = 14f, elevationAmp = 0.5f },
		// 排水は Lifts(高さ14m) も担当だが、カメラは喉元の寄りショット。
		// focusCap を4mにするとFOVが88°の魚眼になったので機構スケールのままにする
		["DrainStation"] = new Rig { groups = new[] { "DrainStation", "Lifts" }, display = "Drain Station / Lifts", orbitDps = 10f, elevationAmp = 0.5f },
	};

	/// `Cam_xxx` -> `xxx`
	public static string KeyOf(string cameraName)
	{
		return cameraName.StartsWith("Cam_") ? cameraName.Substring(4) : cameraName;
	}

	/// 「死角」を体積で測るための格子サイズ[m]。ボールが居た区画のうち、
	/// **一度も映らなかった区画**の割合が本当の死角。時間平均の可視率とは別物なので両方出す。
	public float voxel = 0.15f;

	class Cov
	{
		public Camera cam;
		public Vector3 focus;      // 「映すべき」範囲の中心（＝カメラの注視点）
		public float radius;       // 同・半径
		public int inRegion, outOfFrustum, occluded, seen;
		public readonly HashSet<Vector3Int> visited = new(), everSeen = new();
		public readonly Dictionary<string, int> blockers = new();   // 遮蔽したコライダ名 -> 回数
	}

	readonly List<Cov> covs = new();
	float acc;

	void Start()
	{
		var park = GameObject.Find("Park");
		var cams = park.transform.Find("Cameras");
		if (cams == null) { enabled = false; return; }

		foreach (Transform t in cams)
		{
			var cam = t.GetComponent<Camera>();
			var orb = t.GetComponent<OrbitCamera>();
			if (cam == null || orb == null) continue;   // 担当範囲が決まっている定点カメラだけ
			covs.Add(new Cov { cam = cam, focus = orb.pivot, radius = orb.focusRadius });
		}
		Debug.Log($"[Coverage] tracking {covs.Count} fixed cameras");
	}

	void Update()
	{
		acc += Time.deltaTime;
		if (acc < sampleInterval) return;
		acc = 0f;

		var balls = Object.FindObjectsByType<LotteryBall>();
		foreach (var c in covs)
		{
			var planes = GeometryUtility.CalculateFrustumPlanes(c.cam);
			Vector3 eye = c.cam.transform.position;
			foreach (var ball in balls)
			{
				Vector3 p = ball.transform.position;
				if ((p - c.focus).sqrMagnitude > c.radius * c.radius) continue;
				c.inRegion++;
				var cell = new Vector3Int(Mathf.FloorToInt(p.x / voxel), Mathf.FloorToInt(p.y / voxel), Mathf.FloorToInt(p.z / voxel));
				c.visited.Add(cell);

				bool inFrustum = true;
				foreach (var pl in planes)
					if (pl.GetDistanceToPoint(p) < -0.05f) { inFrustum = false; break; }
				if (!inFrustum) { c.outOfFrustum++; continue; }

				// 球の手前に何かあるか。球の表面ぶんだけ手前で止めて自分自身を拾わない
				Vector3 d = p - eye;
				float dist = d.magnitude - 0.06f;
				// 透過アクリルのシェル（SeeThroughレイヤ）は視界を塞がないので数えない。
				// 物理コライダとしては生きているのでボールの通り道は変わらない。
				int mask = ~0;
				int st = LayerMask.NameToLayer("SeeThrough");
				if (st >= 0) mask &= ~(1 << st);
				if (dist > 0f && Physics.Raycast(eye, d.normalized, out var hit, dist, mask, QueryTriggerInteraction.Ignore))
				{
					c.occluded++;
					// 「何に隠されたか」を数える。これが無いと、どのメッシュを抜けばいいのか当て推量になる
					string k = hit.collider.name;
					c.blockers.TryGetValue(k, out int n);
					c.blockers[k] = n + 1;
				}
				else { c.seen++; c.everSeen.Add(cell); }
			}
		}
	}

	/// 遮蔽回数の多い順に上位5件。「どのメッシュを透かす/抜くか」を決めるための実測値
	static string TopBlockers(Cov c)
	{
		var top = new List<KeyValuePair<string, int>>(c.blockers);
		top.Sort((a, b) => b.Value.CompareTo(a.Value));
		var parts = new List<string>();
		for (int i = 0; i < top.Count && i < 5; i++)
			parts.Add("\"" + top[i].Key + "\":" + top[i].Value);
		return string.Join(",", parts);
	}

	/// SoakRecorder から呼ばれる（ソーク終了時）
	public void Dump()
	{
		var rows = new List<string>();
		foreach (var c in covs)
		{
			float total = Mathf.Max(1, c.inRegion);
			var orb = c.cam.GetComponent<OrbitCamera>();
			float cells = Mathf.Max(1, c.visited.Count);
			rows.Add("  {\"cam\":\"" + c.cam.name + "\""
				   + ",\"samples\":" + c.inRegion
				   + ",\"seen\":" + c.seen
				   + ",\"outOfFrustum\":" + c.outOfFrustum
				   + ",\"occluded\":" + c.occluded
				   + ",\"timeCoverage\":" + (c.seen / total).ToString("F4")
				   + ",\"cellsVisited\":" + c.visited.Count
				   + ",\"cellsSeen\":" + c.everSeen.Count
				   + ",\"blindSpot\":" + (1f - c.everSeen.Count / cells).ToString("F4")
				   + ",\"orbitDps\":" + (orb != null ? orb.degreesPerSecond : 0f).ToString("F1")
				   + ",\"radius\":" + c.radius.ToString("F2")
				   + ",\"blockers\":{" + TopBlockers(c) + "}}");
		}
		var sb = new StringBuilder();
		sb.AppendLine("{ \"cameras\": [");
		sb.AppendLine(string.Join(",\n", rows));
		sb.AppendLine("] }");
		System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "..", outPath), sb.ToString());
		Debug.Log("[Coverage] wrote " + outPath);
	}
}
