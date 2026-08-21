using UnityEngine;

/// 点数マーカー等を常にカメラへ向ける。
public class Billboard : MonoBehaviour
{
    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam != null)
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position); // TextMeshの表を向ける
    }
}
