# AGENTS.md — RouletteSphereChaser

AIエージェント設定の単一情報源（SSOT）。運用ルール・設計方針の追記は本ファイルにのみ行うこと。

## 1. プロジェクト概要

- 神戸「Din Don」のような**観賞用の循環型ボール抽選機**。Unity `6000.5.9f1`（URP）＋ Blender `5.2.0LTS`。
- 番号付きボールが自律循環し、通過した抽選機構の結果に応じて**1巡ごとに点数が確定**する。
- ボールはカメラ個別追従（Tabで切替・0で全景）と、**球体化キャラクターへのテクスチャ差し替え**に対応（`LotteryBall.SetCharacterTexture()`）。

### 開発方針（重要）

> **v2計画: 多塔パーク型・約21m×13m×高さ14m（縦積み高密度）・抽選機構約25基・32球安定稼働。
> 正式な設計正本は `Docs/DESIGN-v2.md`。フェーズ1（パーク基盤・水平拡張版）は実装・検証済み。**
> **造形方針: 抽選機・レールの見た目はBlenderモデリング主体**（演出重視。Unityは物理・スコア・搬送担当）。プリミティブのグレーボックスはプロトタイピング時のみ。

> **今後はレールと抽選機構をさらに増やし、大きめの「ボールコースター」として拡張していく。**
> 新機構・新ルートの追加が前提の設計を保つこと：
> - コース生成は `Assets/Editor/GreyboxBuilder.cs`（`Tools > Build RouletteSphere Greybox`、冪等）に集約し、機構は `Rotator` / `Oscillator` / `ScoreZone` 等の小さな汎用コンポーネントの組み合わせで作る。
> - 各抽選ルートには点数マーカー（`ScoreLabel`）を必ず添える。
> - どの経路から落ちても回収槽→リフトで循環が続く「フェイルセーフ」構造を崩さない。

## 2. ファイル構成

- `Assets/Models/LotteryBall.fbx` … ボール（submesh0=本体: キャラ用全周UV / submesh1=番号パッチ: `NumberAtlas.png` 10×10のUVオフセットで番号切替）
- `Assets/Models/Funnels.fbx` … クルーン・回収槽のすり鉢（Blenderスピンメッシュ、MeshCollider）
- `Assets/Textures/NumberAtlas.png` … 番号0〜99アトラス（PILで生成）
- `Assets/Scripts/` … `LotteryBall` / `ScoreZone` / `LapGate` / `Rotator` / `Oscillator` / `BallLift` / `BallSpawner` / `FollowCamera` / `Billboard`
- `Assets/Editor/GreyboxBuilder.cs` … コース一式の生成（再実行で作り直し）
- `BlenderSources/LotteryBall.blend` … Blender原本（ボール＋すり鉢）

## 3. 物理・実装の既知の罠（必読）

1. **Cylinderプリミティブのコライダはカプセル**。扁平円盤にすると巨大な見えない球になる。円盤/柱は凸MeshColliderへ差し替え（`GreyboxBuilder.Prim`が対応済み）。
2. ボールのRigidbodyは `sleepThreshold = 0`。渋滞待機中にスリープすると坂でも永久停止する。
3. すり鉢・カーブ面は箱の並びで作らない（内縁が土手化して詰まる）。Blenderのスピンメッシュを使う。
4. 穴はボール径の3.6倍でもアーチ詰まりする。撹拌棒必須。ただし高速回転（50°/s級）はボールを遠心軌道に乗せるので12°/s程度、腕は中心の穴を塞がない放射状に。
5. 並走する壁の隙間はボール径の1.5倍以上空ける（挟まり防止）。
6. `ScoreZone` トリガーを回転体の子にしない（回転のたび再加点される）。判定は固定側に置く。
7. フラップゲートは中心をデッキ面に置き背を高く（閉=上半分が壁、開=床と面一の橋）。**開く向きは必ず内側倒し**（外側に倒すと半開時に覆いかぶさる壁になり、開放時間のほとんどで通過不能＝ルートが死ぬ）。
8. ボール番号パッチのUVはFBXの軸変換で鏡文字になるため、Blender側でu反転済み。パッチUVを触るときは Unity 側で実際に文字向きを目視確認すること。
9. コース側コライダは低摩擦 `Rail.physicsMaterial`、レール類はボールの進行レーンを横切らない配置に。**`Ramp()`はレール付き——既存レーンの近くに置くときは必ずレールの占有域を確認**（同じ事故を2回起こした）。
10. **縦落下シャフトの幅はボール径の2倍以上**（縦穴アーチ）、**すり鉢・ドレンの開口は径の3倍以上**でも両側から絞ると口の前でアーチする→**開口前に低速撹拌ローターを置く**か多列で受ける。
11. ジグザグ折り返しレーンは**全段を同一鉛直面（z一定）**に敷く。斜め配置は落下時の横ズレで受け損ねる。
12. 単列ドレン（1列吸い込み）は多球で必ず輻輳する。**v2では回収・搬送を広幅多列 or 複数リフトに分散**すること。
13. 全体スケールはビルダー末尾の `root.localScale`（現行1.5倍）。ボール（径0.1）はランタイム生成で等倍のまま。機構追加時は相対クリアランスをボール径基準で考える。

## 4. Unity MCP 運用

- シーン編集・検証は unity-mcp 経由（`Unity_RunCommand` / `Unity_ManageMenuItem` / `Unity_ReadConsole`）。
- スクリプト編集直後は再コンパイルで一時的に「Unity not detected」になる。20〜40秒待って再試行。
- **プレイ中にスクリプトを編集しない**。exit → 待機（コンパイル完了確認）→ ビルダー再実行 → play の順を厳守。
- GameObjectのentityIdはJSON数値精度で化けるため、ID渡しのキャプチャは不可。スクリーンショットは `RunCommand` でRenderTexture→PNG書き出し→ファイル読取で行う。
- 動作検証はボール座標/速度/laps/scoreの定期プローブと `Physics.OverlapSphere` の接触列挙が有効。

## 5. 引き継ぎ（次セッションの開始手順）

**現状（2026-08-22時点）**

- v1完成機: `SampleScene`＋`GreyboxBuilder.cs`（凍結。参照用に残置、以後は触らない）
- v2フェーズ1完成: `ParkScene`＋`ParkBuilder.cs`＋`GreyboxKit.cs`。矩形傾斜フロア(20.6×13m)→V字漏斗壁→排水路→リフト2基(頂部14m)の循環を8球スモークで検証済み（最新: 50周/150秒・詰まり・場外ゼロ。1球が一時静止する場面あり—次セッションで長時間観察推奨）
- **次の作業: `Docs/DESIGN-v2.md` 6章フェーズ2「タワーA（4階層縦積み）」から。**上の階層から1段ずつ建てて、段ごとにプレイ検証すること。**造形はBlenderモデリング主体**（bmesh生成→FBX→MeshCollider。手順はボール/すり鉢の実績パターン参照）
- 保留中: ボールUVの再設計。**Userが参考ボールモデル2つをBlenderで用意済み**だが、ファイル所在が未確認（開いている`LotteryBall.blend`と`BlenderSources/`には無し）。次セッション冒頭でUserに場所を確認し、参考UVに合わせて`LotteryBall`のUV（テクスチャ改変しやすい配置）を再構成→FBX再出力→鏡文字チェックを行うこと

**セッション開始手順**

1. UserにUnityエディタ（RouletteSphereChaser）とBlender 5.2.0LTSの起動を依頼（他のUnityプロジェクトは閉じる）
2. `Unity_GetProjectData` で疎通確認。「Unity not detected」は再コンパイル中の一時症状のことが多い→20〜40秒待って再試行
3. 作業ブランチは `main`（現状ブランチ運用は単一。変更時はここを更新）

**検証ループ（厳守）**

exit playmode → `AssetDatabase.Refresh` → 40秒待機（コンパイル完了確認）→ ビルダーメニュー実行 → play → 2〜3分放置 → `Unity_RunCommand` でボール座標/速度/laps/score をプローブ。詰まり調査は `Physics.OverlapSphere` の接触列挙が最速。スクリーンショットはRenderTexture→PNG書き出し→Read（entityIdはJSON精度で渡せない）。

**git（定期コミット必須）**

- サンドボックスからは操作不可（lfs無し・削除不可）。**`Tools > Git Commit All` メニュー**を使う。メッセージは事前に `EditorPrefs "GitTools.Message"` へ。pushはUser指示時のみ。

## 6. 運用ルール

- 回答は日本語。`Library/` `Temp/` `Logs/` `obj/` `UserSettings/` は編集・コミット対象外。
- `.meta` はUnityエディタに任せる。
- **gitは節目ごとに定期コミットする**（User方針 2026-08-21）。サンドボックスのマウントはgit-lfs無し・削除不可でgit操作不能のため、**コミットはUnityメニュー `Tools > Git Commit All`（`Assets/Editor/GitTools.cs`）経由**で行う。メッセージは実行前に `EditorPrefs "GitTools.Message"` へ設定。pushはUserの指示があったときのみ。
- ロールプレイはルート `AGENTS.md` の既定（錦野歌嫁）に従う。
