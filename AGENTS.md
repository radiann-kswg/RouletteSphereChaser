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
- `Assets/Models/TowerA_*.fbx` … タワーA機構（Spiral/Distributor/Agitator。原本 `BlenderSources/TowerA.blend`）
- `Assets/Models/ParkBase.fbx` / `DrainStation.fbx` / `LiftGuide.fbx` / `DrainStirrer.fbx` … 土台・回収系（原本 `BlenderSources/ParkBase.blend`。Basin=ParkBase.fbx）
- `BlenderSources/LotteryBall.blend` … Blender原本（ボール＋すり鉢）

> **造形ポリシー（User方針 2026-08-22）**: シーンに配置するメッシュは**すべてBlenderモデリング**とする（DrainStirrer含め可視メッシュ100%Blender化済み）。Unityプリミティブはトリガー等の不可視コライダのみ許容。各メッシュは単一マテリアル＋UV展開済みで、`Assets/Materials/` のマテリアル（`TowerA_Spiral` 等）の `_BaseMap` に任意テクスチャを差し替えられる。
> **接合部の仕上げ**: 複数パーツからなるメッシュは重ね置きにせず、パーツを少し食い込ませて**ブーリアンUnion→角度制限ベベル(0.01〜0.025, clamp)→スマートUV再展開**で一体化する（継ぎ接ぎ感の除去。ベベルはコライダも僅かに丸めるので、変更後はレイキャストで床高さ等価を確認すること）。コプレーナ接触のままUnionすると壊れやすいので必ず食い込ませる。

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
14. **薄肉Blenderシェル（3cm級）はトンネリングする**。ボールPrefabのRigidbodyは `ContinuousDynamic` 必須。回転・可動のキネマティック体は `ContinuousSpeculative` を付ける（Discreteのままだと動く相手とのCCDペア不成立で貫通する—回転ディッシュで実測）。
15. **静止コーン真頂点への無摂動投下は禁止**。完全対称だと頂点上で垂直バウンドを繰り返し頂点シームから貫通する。回転キャップ＋`BallLift.releaseJitter`（解放時ランダム水平速度）で対称性を崩す。
16. **上が開いた円筒（ハブ等）はボールのコップ罠**。内径2.6dでも嵌まって出られない。中央部の造形は必ず円錐キャップ（帽子型）にする。
17. 撹拌腕の低速送り（〜0.3m/s）では**+0.06mの登り床すら越えられない**。排出樋（スナウト）は緩い下り一直線に。
18. **投下点の固定オフセットは分配方位バイアス**になる（西側スパイラル0件の実測）。分岐盤への投下は中心＋解放ジッタで一様化する。
19. **Blender右手系→Unity左手系の座標規約（2026-08-22確定）**: ベース系（ParkBase.blend）は**Blender Z-up規約**で作成する＝blender X=Unity X／blender Z=Unity Y(上)／blender Y=−Unity Z（純回転・det+1）。Unityインポータは`bakeAxisConversion=ON`、配置は`InstantiateFbx`の`Euler(90,0,0)`で world=(x, y, −z)。**Z反転が1軸残る**ため、ベース系メッシュはZ対称で作る（Z非対称の造形を入れるときは要実測・要補正見直し）。機構系（TowerA.blend）はbake無し・素通しマッピング＋`Euler(-90,yaw,0)`の従来運用。新規FBXは必ずインスタンス後にレイキャスト/バウンズで実測確認。
20. **エディットモードでコライダ追加後にTransformを動かしたら`Physics.SyncTransforms()`**を呼んでから検証レイキャストすること（未同期だとコライダが旧位置に残り「すり抜け」に見える。プレイ中は自動同期）。

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
- ✅ **タワーA①完成（2026-08-22）**: 分岐盤ディッシュ（静止・ノッチ4＋radialスナウト）＋回転撹拌腕（4枚羽根＋円錐キャップ・18°/s）＋**大スパイラル×4**（中心距離2.15m対角配置・内向き排出テールで各自の中央シャフト→盆地落下）。LiftN投下(0,13.5,0)＋解放ジッタ0.6。8球170秒で分配 14:15:8:11・詰まり/場外/貫通ゼロ。土台（Basin/DrainStation/Liftガイド）もBlenderメッシュ化済み。
- ✅ **設計改訂3（2026-08-22）**: 8塔構成に拡張（`Docs/DESIGN-v2.md` 2章）。F=JPスピナー塔(0,-5)・G=沼塔(0,5)・H=ガラポン塔(-8.8,0)を新設（皿・穴系＝D類似の家系）、B/C/Eは軽量化、Eの回転ドラムはHへ移設。
- ✅ **フェーズ3完了（2026-08-22）**: タワーA②③＝**スパイラル毎の専用チェーン×4（シャフト合流なし・User指示）**。各スパイラル中心(±1.52,±1.52)にミニクルーン(r1.34@8.45・DrainStirrer流用撹拌)→ミニルーレット(ボウルr0.87@7.25＋6フレットホイール18°/s・静止側スコア20/40/60/100)。8球全球スコア獲得を検証。メッシュ: `TowerA_Mini*.fbx`（Grand版3点は未使用・予備として残置）。**外装方針（User 2026-08-22）: 構造完成後、実際のメダルゲーム/抽選機風のテクスチャ・マテリアルで化粧する前提**——各機構は部位別マテリアル＋スピンUV(u=周方向,v=断面)で作ってある。
- **次の作業: フェーズ4「タワーF/G」**（JPスピナー塔(0,-5)・沼塔(0,5)。DESIGN-v2 2章）。給球はAの①排出帯(9.6m)からの分岐レール新設が必要。
- ✅ ボールメッシュ/UV再設計済み（2026-08-22）: **メッシュはスフィア化キューブ（クアッド球, 8×8×6面）**（User参考`SphereCube_Test`準拠。極が無く円盤縁のメッシュが破綻しない）。**本体UVは「前後2円ディスク」方式**（参考`UVSphere_Test`準拠）。
  - キャンバスは**2:1**（例2048×1024）。**左円=前半球**（Unity +Z 正面、方位等距離図法・円中心=顔の中心）、**右円=後半球**（左右鏡像=後ろから見た絵をそのまま描ける）。円は画像上で真円（UV空間ではu半径0.24/v半径0.48）
  - **番号パッチは上面/下面**（Blender±Z=Unity±Y、25°円錐、アトラスUV・submesh1）。数字の上=キャラ後頭部側（正面斜め上から覗き込んで読める向き）。FBX鏡像対策のu反転は全UVに適用済み
  - アーティスト用テンプレート: `Docs/BallUV_Template.png`。テスト用テクスチャ: `Assets/Textures/RefBallTex.png`（User作・猫キャラ）。UV変更時は必ずUnity側で正面＋真上レンダリングして鏡像チェック（前髪の流れ・数字の向きで判定）

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
