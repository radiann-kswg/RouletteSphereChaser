# RouletteSphereChaser

観賞用の**循環型ボール抽選機パーク**を Unity で組み上げるプロジェクトです。番号付きのボールがリフトで塔の頂上へ運ばれ、スパイラル・クルーン・ルーレット盤などの抽選機構を通って落ちていき、**1 巡ごとに点数が確定**します。どの経路から落ちても回収槽へ集まり、また上へ運ばれる——止まらない機械を眺めるための箱庭です。

神戸「Din Don」のような大型ボールコースターを目標に、塔と機構を継ぎ足しながら育てています。

> English → [`README.en.md`](README.en.md)

> [!NOTE]
> **開発中** — v2「多塔パーク型」の**フェーズ 2 まで実装済み**です。動くもの: 回収フロア〜排水路〜リフト 2 基の循環（8 球スモークで 50 周 / 150 秒・詰まりゼロ）、タワー A ①（分岐盤ディッシュ＋撹拌腕＋大スパイラル×4）、番号アトラス表示、ボール追従カメラ。タワー B〜H は設計済み・未実装です。設計の正本は [`Docs/DESIGN-v2.md`](Docs/DESIGN-v2.md)。

## 動作環境

- Unity **6000.5.9f1**（Universal Render Pipeline）
- Blender **5.2.0 LTS**（`BlenderSources/*.blend` を編集する場合のみ）
- [Git LFS](https://git-lfs.com/)（`.fbx` / `.blend` / `.png` などのバイナリアセットに使用）

クローンにはGit LFS が必要です。

```sh
git lfs install
git clone https://github.com/radiann-kswg/RouletteSphereChaser.git
```

## 動かしかた

1. Unity で本プロジェクトを開き、`Assets/Scenes/ParkScene.unity` を開く
2. メニュー **`Tools > Build RouletteSphere Park (v2)`** を実行してコースを生成する（冪等。何度実行しても同じ形に作り直されます）
3. Play。ボールが順次投入され、循環が始まります

| 操作 | 動作 |
| --- | --- |
| `Tab` | ボールを 1 つずつ順番に追従する |
| `0` | 全景カメラに戻る |

> `Assets/Scenes/SampleScene.unity` ＋ `Tools > Build RouletteSphere Greybox` は v1 の完成機です。参照用に凍結してあります。

## ボールにキャラクターの絵を貼る

ボールは「球体化キャラクター」のテクスチャに差し替えられます。

1. 画像を `Assets/Textures/BallSkins/` に置く（フォルダごとバージョン管理の管轄外です）
2. 実行時に `LotteryBall.SetCharacterTexture(tex)` を呼ぶ。`null` を渡すと番号ボール表示に戻ります

UV は**前後 2 円ディスク方式**（キャンバス 2:1・左円＝前半球 / 右円＝後半球の鏡像）。下描き用テンプレートが [`Docs/BallUV_Template.png`](Docs/BallUV_Template.png) にあります。番号パッチはボールの上下面（`NumberAtlas.png` の 10×10 アトラス）です。

> [!IMPORTANT]
> `BallSkins/` に置いた画像は本リポジトリのライセンス（CC BY 4.0）の対象外で、**テクスチャ画像元のライセンスに従います**。詳細は [`LICENSE-ASSETS.md`](LICENSE-ASSETS.md) §2。

## 構成

| パス | 内容 |
| --- | --- |
| `Assets/Scenes/ParkScene.unity` | v2 本編シーン（多塔パーク） |
| `Assets/Scenes/SampleScene.unity` | v1 完成機（凍結・参照用） |
| `Assets/Editor/ParkBuilder.cs` | v2 コースビルダー（`Tools > Build RouletteSphere Park (v2)`） |
| `Assets/Editor/GreyboxKit.cs` | ビルダー共通ヘルパ（クリアランス基準・罠対策を標準装備） |
| `Assets/Editor/GreyboxBuilder.cs` | v1 コースビルダー（凍結） |
| `Assets/Scripts/` | `LotteryBall` / `ScoreZone` / `LapGate` / `Rotator` / `Oscillator` / `BallLift` / `BallSpawner` / `FollowCamera` / `Billboard` |
| `Assets/Models/` | 抽選機構・土台の `.fbx`（すべて Blender で自作） |
| `Assets/Textures/NumberAtlas.png` | 番号 0〜99 のアトラス |
| `Assets/Textures/BallSkins/` | **キャラクタースキンの置き場（管轄外・各自で用意）** |
| `BlenderSources/` | モデルの原本 `.blend` |
| `Docs/DESIGN-v2.md` | v2 設計書（塔構成・クリアランス基準・スコア設計）— **設計の正本** |
| `Docs/BallUV_Template.png` | ボールテクスチャの下描きテンプレート |
| `AGENTS.md` | AI エージェント設定・物理の既知の罠リスト — **運用の正本** |

見た目に出るメッシュはすべて Blender で作り、Unity 側は物理・スコア・搬送を担当する分担です。Unity プリミティブは不可視のトリガーコライダにのみ使っています。

## AI エージェント設定

本リポジトリの AI エージェント設定の SSOT は [`AGENTS.md`](AGENTS.md) です。`CLAUDE.md` は `AGENTS.md` を参照するだけの薄いポインタなので、**設定の追加・変更は `AGENTS.md` に対してのみ**行ってください。実装で踏んだ物理の罠（アーチ詰まり・トンネリング・座標系変換など）は `AGENTS.md` 3 章にまとめてあります。

## ライセンス

本リポジトリは **CC BY 4.0** です。ただし**ボールスキンだけは対象外**（画像元のライセンス依存）なので、利用前に適用範囲をご確認ください。

> RouletteSphereChaser © 2026 by ラジアン(柏木主税) / RadianN_kswg is licensed under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/)

- ライセンス全文: [`LICENSE`](LICENSE)
- **資産ごとの適用範囲・第三者コンポーネントの表示**: [`LICENSE-ASSETS.md`](LICENSE-ASSETS.md)

## リンク

- 百花繚乱研究所 創作 DB: <https://database.numbertales-radiann.net/>
- ナンバーテールズ公式サイト: <https://www.numbertales-radiann.com/>

© ラジアン(柏木主税) / ©RadianN_kswg
