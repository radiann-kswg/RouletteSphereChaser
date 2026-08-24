# DESIGN-materials.md — 外装（マテリアル／配色）設計書

> フェーズ9「美化」の配色・質感の正本。造形（メッシュ）の正本は `DESIGN-v2.md`、運用ルールは `AGENTS.md`。
> **決定日 2026-08-24（User合意）**: テーマ＝**メダルゲーム筐体風 × 少し暗いゲームセンターの煌びやかさ**／
> 色の割り当て軸＝**役割ごとに統一**／第1段は**単色＋PBR数値のみ**（テクスチャ・装飾は本書で設計してから着手）。

---

## 1. 世界観と狙い

薄暗いゲームセンターの一角に据えられた大型メダルゲーム筐体。**筐体そのものは黒に近いガンメタで沈み**、
そこにクロームと金メッキの搬送路、深いブルーの抽選盤、真っ赤な回転体が浮かび上がる。
**光っているのは「点が入る場所」だけ**——得点ゲートとJP経路が発光し、暗がりの中で視線をそこへ集める。

設計上の狙いは3つ。

1. **25基あっても迷子にならない**。色は塔ではなく**役割**で決める。初見でも「金＝高レア」「赤＝回る」「青＝抽選盤」「光＝点が入る」が読める。
2. **暗所で構造が潰れない**。筐体を黒で沈める代わりに、搬送路をクローム／金にして**反射で輪郭を出す**。
   ライティングは環境光を落とし、上方から2灯だけ当てる（ゲーセンの天井スポット）。
3. **ボール（キャラクター球）が主役として立つ**。背景側の彩度を抑え、球のテクスチャが最も明るく見えるようにする。

---

## 2. 役割の分類と塗り分け（第1段＝実装済みの値）

URP/Lit の `_BaseColor` / `_Metallic` / `_Smoothness` / `_EmissionColor` のみで作る。テクスチャは貼らない。

| # | 役割 | 意味 | BaseColor | Metallic | Smooth | Emission |
|---|------|------|-----------|----------|--------|----------|
| 1 | **CABINET**（筐体・構造） | 床・排水部・背板。世界を沈める黒 | `#20242C` | 0.25 | 0.35 | — |
| 2 | **CHROME**（汎用搬送） | トラフ・チュート・ガイド・受け皿。反射で輪郭を出す | `#B9C0CC` | 1.00 | 0.82 | — |
| 3 | **GOLD**（高レア搬送） | HighLane 8本。「ここを通れば高得点」の記号 | `#FFC53C` | 1.00 | 0.80 | — |
| 4 | **JACKPOT**（JP経路） | JPレール／JPチューブ。金に赤を差し、弱く自発光 | `#FF7A2A` | 0.90 | 0.75 | `#FF5A14` ×0.6 |
| 5 | **DECK**（抽選盤・皿） | クルーン／ボウル／分配盤／沼皿。艶あり塗装の盤面 | `#26306B` | 0.15 | 0.62 | — |
| 6 | **ROTOR**（回転体・可動） | ホイール・ギア・ドラム・撹拌腕・シーソー。動くものは赤 | `#C8262C` | 0.65 | 0.70 | — |
| 7 | **ROTOR_HI**（高得点回転体） | Eチャレンジ盤など「倍率が付く盤」。ROTORの明色版 | `#E8563A` | 0.70 | 0.75 | `#FF3A20` ×0.25 |
| 8 | **SCORE**（得点ゲート・当たり口） | 点が入る場所。**唯一はっきり光る**   | `#EAF4FF` | 0.20 | 0.85 | `#28D8FF` ×1.6 |
| 9 | **SHOWPIECE**（大スパイラル） | 見せ場。アクリル風の淡いシアン・高光沢 | `#9EE8FF` | 0.05 | 0.94 | — |

### マテリアル → 役割の割り当て

| 役割 | マテリアル |
|------|-----------|
| CABINET | `ParkBase` / `DrainStation` / `TowerB_PachiBoard` |
| CHROME | `LiftGuide` / `TowerF_CatchTray` / `TowerF_MissTray` / `TowerB_CatchTray` / `TowerC_CatchTurn` / `TowerC_Zigzag` / `TowerG_MergeTray` / `TowerH_CatchFunnel` / `TowerE_Pickup` |
| GOLD | `TowerA_HighLane` |
| JACKPOT | `TowerG_JPRail` / `TowerF_JPTube` |
| DECK | `TowerA_Distributor` / `TowerA_MiniKuruun` / `TowerA_MiniRouletteBowl` / `TowerA_GrandRouletteBowl` / `TowerA_CollectorFunnel` / `TowerA_RouletteBowl` / `TowerA_GrandKuruun` / `TowerD_Kuruun` / `TowerG_NumaKuruun` / `TowerG_NumaBoard` / `TowerH_KarakoDish` / `TowerF_SpinnerDish` / `TowerF_Separator` / `TowerE_Deck` |
| ROTOR | `TowerA_MiniRouletteWheel` / `TowerA_GrandRouletteWheel` / `TowerA_RouletteWheel` / `TowerE_Gear` / `TowerE_Disc` / `TowerE_Wheel` / `TowerH_Drum` / `TowerH_Swing` / `TowerC_Seesaw` / `Greybox_Accent`（撹拌腕） |
| ROTOR_HI | `TowerE_DiscHi` |
| SCORE | `TowerA_ScoreGate` / `TowerB_StepChucker` |
| SHOWPIECE | `TowerA_Spiral` |

> **例外を作らない**こと。「この塔だけ色を変えたい」は塔ごと配色への逆戻りで、25基では必ず読めなくなる。
> どうしても差を付けたいときは**役割を1つ増やす**（ROTOR_HI がその例）。

---

## 3. シーン側の設定（第1段＝実装済み）

| 項目 | 値 | 理由 |
|------|----|------|
| Ambient | Flat `#141821` / intensity 1.0 | ゲーセンの薄暗さ。Skybox環境光だと全体が青白く浮く |
| Skybox | 暗いProceduralスカイ（Exposure 0.15） | 背景を黒に近付けつつ、金属の反射先を完全な黒にしない |
| Directional Light | intensity 0.55 / 色 `#C8D8FF`（寒色） | メインは弱く。輪郭は金属反射に任せる |
| ArcadeSpot N / S | Spot 2灯・上方から俯瞰・暖色 `#FFD8A0` | 天井スポット。構造の可読性を担保する |
| Global Volume | Bloom（threshold 1.0 / intensity 0.9） | 発光している「点が入る場所」だけを滲ませる |

**注意**: これらは `Park` ルートの外に置く（`ParkBuilder` の再ビルドで消えない）。
ライトを `Park` の子にすると再ビルドのたびに消える。

---

## 4. 第2段の設計（テクスチャ・装飾）— **未着手・着工前にUser確認**

第1段の単色で配色バランスを確定させてから着手する。順番を守ること（テクスチャを先に貼ると、
色が悪いのかテクスチャが悪いのか切り分けられなくなる）。

### 4.1 前提（守る制約）

- 各メッシュは**単一マテリアル＋UV展開済み**（`AGENTS.md` 造形ポリシー）。スピンメッシュは **u=周方向 / v=断面** で展開してある。
  → **周方向に繰り返す帯パターン**（ストライプ・番号帯・ローレット）はUVと相性が良い。写真的なテクスチャは向かない。
- テクスチャは `Assets/Textures/` に置き、**PIL による手続き生成**（`Docs/gen_*.py`）で作る。
  再現性が要るので手描きPNGを直接置かない（ボールスキンだけは例外＝User手描き・git管轄外）。
- **1回に1機構ずつ**貼って見た目を確認する（`AGENTS.md` 反省2「まとめて作ってから直そうとしない」）。

### 4.2 貼るもの（優先順）

| 優先 | 対象 | 内容 | 生成方法 |
|------|------|------|----------|
| 1 | SCORE ゲート盤面 | 点数の数字を焼き込んだ発光パネル（現行はTMPラベルを別途重ねている） | `gen_score_panel.py`（Penchant書体・点数別に10種） |
| 2 | DECK 抽選盤 | 穴の周りに**当たり/ハズレの色環**と点数目盛り。ルーレット盤のフレット割り | `gen_deck_dial.py`（u=方位に等分割の扇形） |
| 3 | CHROME 搬送 | ヘアライン金属（uに沿った微細な条線）＋端部の縞（安全帯） | `gen_metal_hairline.py`（1px条線ノイズ＋タイル） |
| 4 | GOLD/JACKPOT | ローレット（滑り止め）と光沢のむら | 同上のパラメータ違い |
| 5 | CABINET | パンチングメタル（丸穴の等間隔）＋角の擦れ | `gen_punch_panel.py` |
| 6 | ROTOR | 回転方向を示す**矢羽根**と、掴み位置のゴム帯 | `gen_rotor_stripe.py` |

### 4.3 装飾（メッシュ追加を伴うので DESIGN-v2 側の管轄）

以下は**マテリアルではなく造形**の話なので、着工は `ParkAssembly.blend` への追加として扱う。

- 筐体外周のフレーム／幕板（いまパークは"浮いている"ので、床と壁で囲うと筐体らしくなる）
- 得点表示まわりのベゼル（TMPラベルが板から浮いて見えるのを抑える）
- タワーH樋の化粧（`AGENTS.md` フェーズ6積み残し）
- **罠52で「造形が球を囲っていて死角70%超」と判定された機構**（パチンコ盤・沼クルーン・大ルーレット）の
  **窓抜き・縁の低背化**。美化と可視性を同時に解決するので、装飾より先にやる価値がある。

---

## 5. 実装メモ

- マテリアルは `Assets/Materials/*.mat` の実体を書き換える。`GreyboxKit.Mat()` は
  **既存の .mat があればそれを返す**（無いときだけ生成する）ので、ビルダー再実行で色が戻ることはない。
- `Docs/park_materials.json` の `rgb` は **Blender側のビューポート色**であって、Unityの見た目には使われない
  （`ParkBuilder` は `.mat` を優先する）。Blenderでの作業性のために後追いで揃えてよいが、正本は本書。
- Emission を使うマテリアルは `EnableKeyword("_EMISSION")` と
  `globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive` を忘れない（付け忘れると光らない）。
