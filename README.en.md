# RouletteSphereChaser

A Unity project that builds an **ornamental, self-circulating ball lottery park**. Numbered balls ride lifts to the top of a tower, fall through spirals, kuruun dishes and roulette wheels, and **score is committed once per lap**. Whatever route a ball takes, it ends up in the collection basin and is carried up again — a terrarium built for watching a machine that never stops.

The long-term goal is a large ball coaster in the spirit of Kobe's "Din Don", grown by adding towers and mechanisms one at a time.

> 日本語 → [`README.md`](README.md)

> [!NOTE]
> **Work in progress** — v2 ("multi-tower park") is implemented **through phase 2**. Working today: the collection floor → drain → two lifts circulation loop (8-ball smoke test: 50 laps in 150 s, zero jams), Tower A ① (distributor dish + agitator + 4 large spirals), number-atlas display, and the ball-follow camera. Towers B–H are designed but not built. The design source of truth is [`Docs/DESIGN-v2.md`](Docs/DESIGN-v2.md) (Japanese).

## Requirements

- Unity **6000.5.9f1** (Universal Render Pipeline)
- Blender **5.2.0 LTS** (only if you want to edit `BlenderSources/*.blend`)
- [Git LFS](https://git-lfs.com/) — used for binary assets (`.fbx`, `.blend`, `.png`, …)

Git LFS is required to clone the repository.

```sh
git lfs install
git clone https://github.com/radiann-kswg/RouletteSphereChaser.git
```

## Running it

1. Open the project in Unity, then open `Assets/Scenes/ParkScene.unity`
2. Run the menu item **`Tools > Build RouletteSphere Park (v2)`** to generate the course (idempotent — re-running rebuilds the same layout)
3. Press Play. Balls are fed in one by one and the loop starts

| Key | Action |
| --- | --- |
| `Tab` | Follow the next ball |
| `0` | Return to the overview camera |

> `Assets/Scenes/SampleScene.unity` plus `Tools > Build RouletteSphere Greybox` is the finished v1 machine. It is frozen and kept for reference.

## Putting a character on a ball

Balls can wear a "spherized character" texture instead of the plain numbered look.

1. Drop your image into `Assets/Textures/BallSkins/` (the whole folder is outside version control)
2. Call `LotteryBall.SetCharacterTexture(tex)` at runtime. Passing `null` restores the numbered ball

The UV layout is a **two-disc (front/back) projection**: a 2:1 canvas, left circle = front hemisphere, right circle = back hemisphere mirrored. A drawing template is provided at [`Docs/BallUV_Template.png`](Docs/BallUV_Template.png). The number patch sits on the top and bottom faces, driven by the 10×10 `NumberAtlas.png`.

> [!IMPORTANT]
> Images placed in `BallSkins/` are **not** covered by this repository's CC BY 4.0 license — they follow **the license of the source image**. See [`LICENSE-ASSETS.md`](LICENSE-ASSETS.md) §6.

## Layout

| Path | Contents |
| --- | --- |
| `Assets/Scenes/ParkScene.unity` | The v2 scene (multi-tower park) |
| `Assets/Scenes/SampleScene.unity` | The v1 machine (frozen, for reference) |
| `Assets/Editor/ParkBuilder.cs` | v2 course builder (`Tools > Build RouletteSphere Park (v2)`) |
| `Assets/Editor/GreyboxKit.cs` | Shared builder helpers (clearance rules and pitfall guards built in) |
| `Assets/Editor/GreyboxBuilder.cs` | v1 course builder (frozen) |
| `Assets/Scripts/` | `LotteryBall` / `ScoreZone` / `LapGate` / `Rotator` / `Oscillator` / `BallLift` / `BallSpawner` / `FollowCamera` / `Billboard` |
| `Assets/Models/` | `.fbx` for mechanisms and structure (all modelled in Blender by the author) |
| `Assets/Textures/NumberAtlas.png` | Atlas of numbers 0–99 |
| `Assets/Textures/BallSkins/` | **Where character skins go — untracked, bring your own** |
| `BlenderSources/` | The `.blend` sources for the models |
| `Docs/DESIGN-v2.md` | v2 design document (tower layout, clearance rules, scoring) — **design source of truth** |
| `Docs/BallUV_Template.png` | Drawing template for ball textures |
| `AGENTS.md` | AI agent configuration and the list of known physics pitfalls — **operational source of truth** |

Every visible mesh is authored in Blender; Unity handles physics, scoring and transport. Unity primitives are used only for invisible trigger colliders.

## AI agent configuration

The single source of truth for AI agent configuration is [`AGENTS.md`](AGENTS.md). `CLAUDE.md` is a thin pointer to it, so **add or change settings in `AGENTS.md` only**. Section 3 of `AGENTS.md` collects the physics pitfalls hit during development (arch jams, tunnelling, Blender↔Unity axis conversion, and so on).

## License

This repository is licensed under **CC BY 4.0** — with the ball skins as the one exception (they follow their source image's license). Please check the scope before reusing anything.

> RouletteSphereChaser © 2026 by ラジアン(柏木主税) / RadianN_kswg is licensed under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/)

- Full license text: [`LICENSE`](LICENSE)
- **Per-asset scope and third-party notices**: [`LICENSE-ASSETS.md`](LICENSE-ASSETS.md)

## Links

- 100BeautiesLab. Creations DB: <https://database.numbertales-radiann.net/?lang=en>
- Number Tales official site: <https://www.numbertales-radiann.com/>

© ラジアン(柏木主税) / ©RadianN_kswg
