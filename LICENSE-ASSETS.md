# 資産ごとのライセンス / Per-asset Licensing

日本語 → §1〜§4 ／ English → §5–§8

---

## 1. 既定: CC BY 4.0

本リポジトリの成果物は、特記なき限り **Creative Commons Attribution 4.0 International (CC BY 4.0)** の下で提供されます。全文は [`LICENSE`](LICENSE)。

> RouletteSphereChaser © 2026 by ラジアン(柏木主税) / RadianN_kswg is licensed under CC BY 4.0

対象は、C# スクリプト・エディタ拡張・Unity プロジェクト設定・シーン・マテリアル、および **自作の 3D モデル**（`Assets/Models/*.fbx` と原本 `BlenderSources/*.blend`）・番号アトラス `Assets/Textures/NumberAtlas.png`・UV テンプレート `Docs/BallUV_Template.png`・`Docs/` および `AGENTS.md` 等の文書です。

利用時は上記のクレジット表記（原作者名・ライセンス名・ライセンス URL）を添えてください。改変は自由ですが、改変した旨を示してください。

## 2. 例外: ボールスキン（キャラクターテクスチャ）

ボールに貼る球体化キャラクターのテクスチャは **CC BY 4.0 の対象外** であり、**リポジトリの管轄外**（`.gitignore` 済み）です。

| 項目 | 内容 |
| --- | --- |
| 置き場所 | `Assets/Textures/BallSkins/`（`.gitkeep` 以外は追跡しない） |
| ライセンス | **テクスチャ画像元のライセンスに従う**（本リポジトリは一切の権利主張をしない） |
| 適用方法 | `LotteryBall.SetCharacterTexture(Texture)` で実行時に差し替え |

**クローンした人は、自分が権利を持つ画像・利用条件を満たした画像を各自でここに置いてください。** リポジトリには同梱されません。

### 2.1 原作者が使用しているスキンの例

原作者（ラジアン）が試作で使用しているスキンには、**「百花繚乱研究所」の一次創作キャラクター**を球体化したものが含まれます。これらは以下のライセンスに従うため、**CC BY 4.0 ではなく CC BY-NC 4.0（非営利限定）** の対象です。

> 100BeautiesLab.(百花繚乱研究所) Primary Works/Creations © 2021-2026 by RadianN_kswg(ラジアン/柏木主税) is licensed under CC BY-NC 4.0

- ライセンス全文: <https://creativecommons.org/licenses/by-nc/4.0/>
- **公式ガイドライン（正文）**: <https://github.com/radiann-kswg/100BeautiesLab_CreationsDB/blob/develop/guideline.md>
- 創作 DB: <https://database.numbertales-radiann.net/>

> [!IMPORTANT]
> これらのキャラクターを含むスキンを配布・公開する場合は、**商用利用不可**を含む上記ガイドラインに従ってください。本リポジトリのコード（CC BY 4.0）と混同しないでください。

## 3. 第三者コンポーネントの表示

| 対象 | 出典 | ライセンス |
| --- | --- | --- |
| `.gitignore` | [github/gitignore](https://github.com/github/gitignore) — `Unity.gitignore` | CC0-1.0 |
| `.gitattributes` | [gitattributes/gitattributes](https://github.com/gitattributes/gitattributes) — `Unity.gitattributes` | MIT |
| Unity テンプレート由来のアセット・Unity パッケージ | Unity Technologies（URP テンプレート / Unity Registry） | Unity Companion License 等、各パッケージの条件に従う |

## 4. 食い違いがあった場合

本ファイルの記述と、参照先の公式ガイドライン・各ライセンス全文が食い違う場合は、**参照先を正**とします。

---

## 5. Default: CC BY 4.0

Unless stated otherwise, the contents of this repository are provided under the **Creative Commons Attribution 4.0 International (CC BY 4.0)** license. Full text: [`LICENSE`](LICENSE).

> RouletteSphereChaser © 2026 by ラジアン(柏木主税) / RadianN_kswg is licensed under CC BY 4.0

This covers the C# scripts, editor tooling, Unity project settings, scenes and materials, as well as the **original 3D models** (`Assets/Models/*.fbx` and their `BlenderSources/*.blend` sources), the number atlas `Assets/Textures/NumberAtlas.png`, the UV template `Docs/BallUV_Template.png`, and the documentation under `Docs/` and `AGENTS.md`.

When reusing, include the credit above (author, license name, license URL). You may modify freely, but you must indicate that changes were made.

## 6. Exception: ball skins (character textures)

Spherized character textures applied to the balls are **NOT covered by CC BY 4.0** and are **kept out of version control** (git-ignored).

| Item | Detail |
| --- | --- |
| Location | `Assets/Textures/BallSkins/` (nothing but `.gitkeep` is tracked) |
| License | **Whatever license the source image carries.** This repository claims no rights over it. |
| How it is applied | Swapped at runtime via `LotteryBall.SetCharacterTexture(Texture)` |

**If you cloned this repository, drop in your own images — ones you own, or ones whose terms you satisfy.** None are shipped with the repository.

### 6.1 Example: the skins used by the original author

Some skins used by the author (RadianN_kswg) are spherized versions of **original characters by 100BeautiesLab. (百花繚乱研究所)**. Those follow the license below, i.e. **CC BY-NC 4.0 (non-commercial only), not CC BY 4.0**.

> 100BeautiesLab. Primary Works/Creations © 2021-2026 by RadianN_kswg is licensed under CC BY-NC 4.0

- License text: <https://creativecommons.org/licenses/by-nc/4.0/>
- **Official guidelines (authoritative)**: <https://github.com/radiann-kswg/100BeautiesLab_CreationsDB/blob/develop/guideline.en.md>
- Creations DB: <https://database.numbertales-radiann.net/?lang=en>

> [!IMPORTANT]
> If you distribute or publish a skin containing these characters, follow the guidelines above — including the **no commercial use** condition. Do not conflate them with this repository's code, which is CC BY 4.0.

## 7. Third-party components

| Component | Source | License |
| --- | --- | --- |
| `.gitignore` | [github/gitignore](https://github.com/github/gitignore) — `Unity.gitignore` | CC0-1.0 |
| `.gitattributes` | [gitattributes/gitattributes](https://github.com/gitattributes/gitattributes) — `Unity.gitattributes` | MIT |
| Assets and packages from the Unity template | Unity Technologies (URP template / Unity Registry) | Unity Companion License and the terms of each package |

## 8. In case of conflict

Where this file disagrees with the referenced official guidelines or the full license texts, **the referenced documents prevail**.

---

© ラジアン(柏木主税) / ©RadianN_kswg
