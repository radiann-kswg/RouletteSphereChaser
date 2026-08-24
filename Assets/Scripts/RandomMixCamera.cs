using System.Collections.Generic;
using UnityEngine;

/// Display 1 に出す「全カメラ対応」のランダム切替カメラ（User指示 2026-08-24）。
/// 抽選機チャンネル（`RandomFixedCamera`）とボールチャンネル（`RandomFollowCamera`）の
/// 計8台から数秒〜十数秒ごとに1台を選び、その姿勢をそのまま映す。
/// レースゲームのデモプレイでいう「オンボード↔コース脇を切り替える親カメラ」にあたる。
public class RandomMixCamera : MonoBehaviour
{
    public float minHold = 7f, maxHold = 14f;
    /// 抽選機チャンネルを選ぶ比率（残りはボールチャンネル）
    [Range(0f, 1f)] public float mechRatio = 0.55f;

    readonly List<Camera> mech = new(), ball = new();
    Camera src, self;
    float nextSwitch;

    /// いま映しているチャンネル（HUDが機構名かボール情報かを決めるのに使う）
    public Camera Source => src;

    void Start()
    {
        self = GetComponent<Camera>();
        foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.InstanceID))
        {
            if (c == self) continue;
            if (c.GetComponent<RandomFixedCamera>() != null) mech.Add(c);
            else if (c.GetComponent<RandomFollowCamera>() != null) ball.Add(c);
        }
        Pick();
    }

    void Update()
    {
        if (src == null || Time.time >= nextSwitch) Pick();
    }

    public void Pick()
    {
        var pool = (mech.Count > 0 && (ball.Count == 0 || Random.value < mechRatio)) ? mech : ball;
        if (pool.Count == 0) { nextSwitch = Time.time + 1f; return; }
        var pick = pool[Random.Range(0, pool.Count)];
        if (pick == src && pool.Count > 1) pick = pool[(pool.IndexOf(pick) + 1) % pool.Count];
        src = pick;
        nextSwitch = Time.time + Random.Range(minHold, maxHold);
    }

    void LateUpdate()
    {
        if (src == null) return;
        transform.SetPositionAndRotation(src.transform.position, src.transform.rotation);
        if (self != null) self.fieldOfView = src.fieldOfView;
    }
}
