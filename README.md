# RouletteSphereChaser

観賞用の**循環型ボール抽選機パーク**を Unity で組み上げるプロジェクトです。番号付きのボールがリフトで塔の頂上へ運ばれ、スパイラル・クルーン・ルーレット盤などの抽選機構を通って落ちていき、**1 巡ごとに点数が確定**します。どの経路から落ちても回収槽へ集まり、また上へ運ばれる——止まらない機械を眺めるための箱庭です。

![パーク全景](Docs/screenshots/park-wide.png)

> 日本語 / English → [`README.en.md`](README.en.md)

## 誰のためのアプリか

想定しているのは、**どちらも「店には行かない人」**です。

- **騒がしいゲームセンターやパチンコ店には入りにくいけれど、ボール抽選機は眺めていたい人**
- **ギャンブルやアミューズメントを、軽く疑似体験したい人**

つまりこれは**実機の代わりではなく、実機に行かずに済むための場所**です。ここから設計判断が決まります。

- **静かであること**を最優先にする。射幸心を煽る原色・点滅・強い発光・大音量は採らない。
- **疑似体験は"軽く"**。当たりを派手に祝わず、点が入ったことが読めれば十分。
- **中が見えること**を機構の見栄えより優先する。球を囲う造形は透過アクリルにしてある。
- 主役は**球**、機構は舞台装置。

迷ったときの判断基準は「**静かな部屋でずっと流しておけるか**」です。
外装の正本は [`Docs/DESIGN-materials.md`](Docs/DESIGN-materials.md)、構造の正本は [`Docs/DESIGN-v2.md`](Docs/DESIGN-v2.md)。

> [!NOTE]
> **開発中** — v2「多塔パーク型」は **8 塔・抽選機構 25 基・36 球運用**まで組み上がり、外装（配色・透過シェル・照明）の第 1 段まで入っています。直近の 36 球 300 秒ソークで **コースアウト 0・停止 0・迷子 0**。残っているのは配点の再計算（フェーズ 7）とテクスチャ・装飾（フェーズ 9-2 以降）です。

## 眺めどころ

| | |
| --- | --- |
| ![タワーA全景](Docs/screenshots/tower-a-overview.png) **タワー A** — リフトで運ばれた球が大スパイラル 4 基を降り、ミニクルーン → ミニルーレットへ分配される | ![大ルーレット](Docs/screenshots/grand-roulette.png) **大ルーレット** — 内向き 8 口の低得点ルートが集約ファンネルで合流する見せ場 |
| ![沼クルーン](Docs/screenshots/numa-kuruun.png) **沼クルーン（タワー G）** — 3 段の皿を落ちながら「当たり穴／ハズレ穴」で連続抽選 | ![パチンコ盤](Docs/screenshots/pachinko.png) **パチンコ盤（タワー B）** — 釘 22 本の盤面から 3 段ステップチャッカーへ |
| ![ガラポン](Docs/screenshots/garapon.png) **ガラポン（タワー H）** — カラコロッタ式の穴リング 8 から横軸ドラムへ | ![ポケット盤](Docs/screenshots/pocket-disc.png) **傾斜ポケット盤（タワー E）** — 皿ごと 18°/s で回り、ポケットの角幅で確率を作る |

皿・盤・トラフのうち**球を囲っているものは透過アクリル**にしてあります。造形を削らずに中を見せるためで、コライダは一切変えていません（詳細は [`Docs/DESIGN-materials.md`](Docs/DESIGN-materials.md) 2.5 章）。

## 動作環境

- Unity **6000.6.0f1**（Universal Render Pipeline）
- Blender **5.2.0 LTS**（`BlenderSources/*.blend` を編集する場合のみ）
- [Git LFS](https://git-lfs.com/)（`.fbx` / `.blend` / `.png` などのバイナリアセットに使用）

クローンには Git LFS が必要です。

```sh
git lfs install
git clone https://github.com/radiann-kswg/RouletteSphereChaser.git
```

## 動かしかた

1. Unity で本プロジェクトを開き、`Assets/Scenes/ParkScene_v2.unity` を開く
2. メニュー **`Tools > Build RouletteSphere Park (v2)`** を実行してパークを生成する（冪等。何度実行しても同じ形に作り直されます）
3. Play。ボールが順次投入され、循環が始まります

| 操作 | 動作 |
| --- | --- |
| `C` | デモ演出のオン/オフ（8 チャンネルから自動でショットを選ぶ） |
| `V` | 次のショットへ送る |
| `Tab` | ボールを 1 つずつ順番に追従する |
| `0` | 全景カメラに戻る（オート追従を解除） |

| メニュー | 内容 |
| --- | --- |
| `Tools > Build RouletteSphere Park (v2)` | パークの生成（冪等） |
| `Tools > Run Soak (36 balls)` | 36 球の長時間試験。周回数・得点・**コースアウト/停止/迷子**と定点カメラの死角を `Docs/soak_*.json` / `Docs/camera_coverage.json` に記録 |
| `Tools > Capture Showcase Shots` | 本 README 用のスクリーンショットを `Docs/screenshots/` に撮り直す |

## ボールにキャラクターの絵を貼る

ボールは「球体化キャラクター」のテクスチャに差し替えられます。

1. 画像を `Assets/Textures/BallSkins/` に置く（フォルダごとバージョン管理の管轄外です）
2. 実行時に `LotteryBall.SetCharacterTexture(tex)` を呼ぶ。`null` を渡すと番号ボール表示に戻ります

UV は**前後 2 円ディスク方式**（キャンバス 2:1・左円＝前半球 / 右円＝後半球の鏡像）。下描き用テンプレートが [`Docs/BallUV_Template.png`](Docs/BallUV_Template.png) にあります。番号パッチはボールの上下面（`NumberAtlas.png` の 10×10 アトラス）です。

ボール単体を自分のプロジェクトへ持っていきたい場合は、サブモジュールの [`LotteryBallKit/`](https://github.com/radiann-kswg/LotteryBallKit) を使ってください。

> [!IMPORTANT]
> `BallSkins/` に置いた画像は本リポジトリのライセンス（CC BY 4.0）の対象外で、**テクスチャ画像元のライセンスに従います**。詳細は [`LICENSE-ASSETS.md`](LICENSE-ASSETS.md) §2。

## 構成

| パス | 内容 |
| --- | --- |
| `Assets/Scenes/ParkScene_v2.unity` | v2 本編シーン（多塔パーク） |
| `Assets/Editor/ParkBuilder.cs` | v2 パークビルダー。**座標は一切計算せず** `ParkAssembly.fbx` ＋ `ParkAssembly.params.json` を読むだけの解釈器 |
| `Assets/Editor/SoakRunner.cs` / `ShowcaseCapture.cs` | 長時間試験の起動係 / README 用スクショの撮影 |
| `Assets/Scripts/` | `LotteryBall` / `ScoreZone` / `LapGate` / `Rotator` / `Oscillator` / `BallLift` / `BallSpawner` / カメラ演出一式 / `SoakRecorder` / `CameraCoverage` |
| `Assets/Models/ParkAssembly.fbx` | **配置の唯一の成果物**（メッシュ 123 ＋機能マーカー） |
| `Assets/Textures/NumberAtlas.png` | 番号 0〜99 のアトラス |
| `Assets/Textures/BallSkins/` | **キャラクタースキンの置き場（管轄外・各自で用意）** |
| `BlenderSources/ParkAssembly.blend` | **配置の SSOT**。メッシュは 5 つの原本 `.blend` からリンク |
| `BlenderSources/*.blend` | メッシュの原本（`ParkBase` / `TowerA` / `TowerBCH` / `TowerDE` / `TowerFG`） |
| `Docs/DESIGN-v2.md` | v2 設計書（コンセプト・塔構成・クリアランス基準・スコア設計）— **構造の正本** |
| `Docs/DESIGN-materials.md` | 外装設計書（配色パレット・透過シェル・照明）— **外装の正本** |
| `Docs/screenshots/` | README 掲載画像（`Tools > Capture Showcase Shots` が生成） |
| `Docs/*.py` | Blender 側のツール（アセンブリ生成・検証・書き出し・メッシュ健全性検査） |
| `AGENTS.md` | AI エージェント設定・物理の既知の罠リスト — **運用の正本** |

見た目に出るメッシュはすべて Blender で作り、Unity 側は物理・スコア・搬送を担当する分担です。Unity プリミティブは不可視のトリガーコライダにのみ使っています。

## AI エージェント設定

本リポジトリの AI エージェント設定の SSOT は [`AGENTS.md`](AGENTS.md) です。`CLAUDE.md` は `AGENTS.md` を参照するだけの薄いポインタなので、**設定の追加・変更は `AGENTS.md` に対してのみ**行ってください。実装で踏んだ物理の罠（アーチ詰まり・トンネリング・座標系変換・メッシュ破綻など 56 件）は `AGENTS.md` 3 章にまとめてあります。

## ライセンス

本リポジトリは **CC BY 4.0** です。ただし**ボールスキンだけは対象外**（画像元のライセンス依存）なので、利用前に適用範囲をご確認ください。

> RouletteSphereChaser © 2026 by ラジアン(柏木主税) / RadianN_kswg is licensed under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/)

- ライセンス全文: [`LICENSE`](LICENSE)
- **資産ごとの適用範囲・第三者コンポーネントの表示**: [`LICENSE-ASSETS.md`](LICENSE-ASSETS.md)

## リンク

- 百花繚乱研究所 創作 DB: <https://database.numbertales-radiann.net/>
- ナンバーテールズ公式サイト: <https://www.numbertales-radiann.com/>

© ラジアン(柏木主税) / ©RadianN_kswg
