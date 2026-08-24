using UnityEngine;

/// 抽選ボール本体。番号表示（アトラスUVオフセット）・キャラテクスチャ差し替え・スコア台帳を持つ。
public class LotteryBall : MonoBehaviour
{
    [Range(0, 99)] public int number;
    public Color tint = Color.white;

    [Header("Score (runtime)")]
    public int pendingPoints; // 今の1巡で獲得中
    public int totalScore;
    public int laps;

    /// 高得点チャレンジ段（タワーD/Eの上段）で獲得した倍率。
    /// 直後の通常抽選段が加点するときに掛かって消費される（1に戻る）。1巡終了でもリセット。
    public int nextMultiplier = 1;

    Renderer rend;
    static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");

    void Awake()
    {
        rend = GetComponentInChildren<Renderer>();
        Apply();
    }

    void OnValidate()
    {
        if (rend != null) Apply();
    }

    /// 番号・ティントをマテリアルへ反映（submesh 0=本体, 1=番号パッチ）
    public void Apply()
    {
        if (rend == null) return;
        var body = new MaterialPropertyBlock();
        rend.GetPropertyBlock(body, 0);
        body.SetColor(BaseColor, tint);
        rend.SetPropertyBlock(body, 0);

        // ponytail: _BaseMap_ST を MPB で上書き。URPで効かない環境なら番号ごとのマテリアルキャッシュに切替
        var num = new MaterialPropertyBlock();
        rend.GetPropertyBlock(num, 1);
        num.SetVector(BaseMapST, new Vector4(1f, 1f, 0.1f * (number % 10), 0.1f * (number / 10)));
        rend.SetPropertyBlock(num, 1);
    }

    /// 球体化キャラのテクスチャを貼る。null で番号ボール表示に戻す。
    public void SetCharacterTexture(Texture tex)
    {
        if (rend == null) rend = GetComponentInChildren<Renderer>();
        var body = new MaterialPropertyBlock();
        if (tex != null)
        {
            body.SetTexture(BaseMap, tex);
            body.SetColor(BaseColor, Color.white);
        }
        rend.SetPropertyBlock(body, 0); // tex==null なら空MPBでマテリアル既定に戻る
        if (tex == null) Apply();
    }

    public void CommitLap()
    {
        totalScore += pendingPoints;
        pendingPoints = 0;
        nextMultiplier = 1;   // 使わずに1巡終えた倍率は持ち越さない
        laps++;
    }
}
