# LotteryBallKit — 番号ボールアセット

RouletteSphereChaser の抽選ボールを他のUnityプロジェクトで使うための移植キット。

## 内容

- `Prefabs/NumberBall.prefab` … そのまま置ける物理ボール（径0.1m・Rigidbody CCD・SphereCollider）
- `Models/LotteryBall.fbx` … スフィア化キューブ球（本体384面・全周UV）＋番号デカールディスク（球面から0.2mm浮き・submesh1）
- `Textures/NumberAtlas.png` … 番号0〜99アトラス（10×10・下段=0〜9、白地黒数字・PenchantManufacture書体）
- `Textures/BallSkins_Sample.png` … キャラスキンのサンプル（前後2円ディスクUV）
- `Textures/BallUV_Template.png` … スキン作画用UVガイド（左円=前半球／右円=後半球=鏡像。赤い弧は番号デカールに隠れる範囲）
- `Scripts/NumberBall.cs` … 番号印字（アトラスUVオフセット）＋キャラテクスチャ差し替え
- `Materials/BallKit_Body.mat` / `BallKit_Number.mat` … URP Lit マテリアル
- `Sources/LotteryBall.blend.bytes` … Blender原本（拡張子を `.blend` に戻すと Blender 5.x で開ける）
- `Sources/gen_number_atlas.py` … アトラス再生成スクリプト（要 PIL と PenchantManufacture フォント）

## 使い方

1. `NumberBall.prefab` をシーンに配置
2. Inspector の `Number`（0〜99）で番号を設定（`NumberBall.Apply()` がUVオフセットに反映）
3. キャラ絵にするなら `numberBall.SetCharacterTexture(tex)` を呼ぶ（`null` で白ボールに戻る）

### スキンの描き方

キャンバスは2:1（例 2048×1024）。左円=前半球（正面・方位等距離図法、円中心=顔の中心）、
右円=後半球（左右鏡像＝後ろから見た絵をそのまま描ける）。`BallUV_Template.png` をガイドに。

## 注意

- マテリアルは **URP Lit**（`_BaseMap`/`_BaseColor`）想定。Built-in RP では `NumberBall.cs` のプロパティ名を `_MainTex`/`_Color` に読み替えること。
- 番号の変更は MaterialPropertyBlock 経由（マテリアル資産は汚れない）。エディタの非プレイ描画では反映されないビューがある（プレイ時は正常）。

## ライセンス

CC BY 4.0 — `LICENSE.md` を参照。サンプルスキン `BallSkins_Sample.png` も同ライセンス。
