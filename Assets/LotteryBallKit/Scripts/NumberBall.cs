using UnityEngine;

/// 番号ボール（移植用・スコア機能なし）。
/// submesh0=本体（キャラテクスチャ差し替え可） / submesh1=番号デカール（10×10アトラスのUVオフセットで0〜99表示）。
/// URP Lit想定（_BaseMap / _BaseColor）。Built-in RPで使う場合は _MainTex / _Color に読み替えること。
public class NumberBall : MonoBehaviour
{
    [Range(0, 99)] public int number;
    public Color tint = Color.white;

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

    /// 番号・ティントをマテリアルへ反映（MaterialPropertyBlock使用＝マテリアル資産を汚さない）
    public void Apply()
    {
        if (rend == null) return;
        var body = new MaterialPropertyBlock();
        rend.GetPropertyBlock(body, 0);
        body.SetColor(BaseColor, tint);
        rend.SetPropertyBlock(body, 0);

        // アトラスは下段=0〜9・上段=90番台のレイアウト。オフセット=(0.1*(n%10), 0.1*(n/10))
        var num = new MaterialPropertyBlock();
        rend.GetPropertyBlock(num, 1);
        num.SetVector(BaseMapST, new Vector4(1f, 1f, 0.1f * (number % 10), 0.1f * (number / 10)));
        rend.SetPropertyBlock(num, 1);
    }

    /// 球体化キャラのテクスチャを貼る（前後2円ディスクUV）。null で白ボールに戻す。
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
}
