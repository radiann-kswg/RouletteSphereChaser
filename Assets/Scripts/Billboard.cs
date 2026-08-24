using UnityEngine;

/// 点数マーカー等を**いま映しているカメラ**へ正対させる。
///
/// デモ演出では Display 1 に出るカメラが次々に変わるので、`Camera.main` 固定だと
/// 定点カメラや追従カメラから見たときに文字が斜めを向いて読めない（User報告 2026-08-24）。
/// `CameraDirector.Active` を見て、いま画面を作っているカメラへ向ける。
///
/// ゲート盤面に印字してある得点（`GateLabel_*`）にはこれを付けない。あれは造形の一部で、
/// 板から浮いて回ると逆に嘘になるため。
public class Billboard : MonoBehaviour
{
    /// 文字を寝かせず、水平回転だけでカメラを向く（読みやすさ優先）
    public bool keepUpright = true;

    void LateUpdate()
    {
        var cam = CameraDirector.Active;
        if (cam == null) return;
        Vector3 dir = transform.position - cam.transform.position;
        if (keepUpright) dir.y = 0f;
        if (dir.sqrMagnitude < 1e-6f) return;
        transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);   // TMPの表を向ける
    }
}
