# RouletteSphereChaser

A Unity project that builds an **ornamental, self-circulating ball lottery park**. Numbered balls ride lifts to the top of a tower, fall through spirals, kuruun dishes and roulette wheels, and **score is committed once per lap**. Whatever route a ball takes, it ends up in the collection basin and is carried up again — a terrarium built for watching a machine that never stops.

![Park overview](Docs/screenshots/park-wide.png)

> 日本語 / English → [`README.md`](README.md)

## Who this is for

Both of the people we build for are **people who don't go to the arcade**.

- **Someone who finds noisy arcades and pachinko parlours hard to walk into, but still wants to watch a ball lottery machine.**
- **Someone who wants a light, vicarious taste of gambling and amusement.**

So this is **not a substitute for the real machine — it is a place you can go instead of one.** Every design decision follows from that.

- **Being quiet comes first.** No saturated primaries, no flashing, no aggressive glow, no loud audio — those are exactly the reasons the real venue is hard to enter.
- **The vicarious part stays light.** A win doesn't need a celebration; it only needs to be legible.
- **Seeing inside beats looking good.** Shells that enclose the balls are clear acrylic.
- The **ball** is the protagonist; the mechanisms are stage scenery.

When a call is close, we ask: *could you leave this running in a quiet room?*
The visual source of truth is [`Docs/DESIGN-materials.md`](Docs/DESIGN-materials.md); the structural one is [`Docs/DESIGN-v2.md`](Docs/DESIGN-v2.md) (both Japanese).

> [!NOTE]
> **Work in progress** — v2 ("multi-tower park") now stands at **8 towers, ~25 lottery mechanisms, 36 balls**, with the first pass of the exterior (palette, see-through shells, lighting) in place. The latest 36-ball / 300 s soak recorded **0 balls off-course, 0 stalled, 0 lost**. Remaining: score rebalancing (phase 7) and textures/ornament (phase 9-2 onward).

## Things to watch

| | |
| --- | --- |
| ![Tower A](Docs/screenshots/tower-a-overview.png) **Tower A** — balls come off the lift, descend four large spirals, and are distributed to mini kuruuns and mini roulettes | ![Grand roulette](Docs/screenshots/grand-roulette.png) **Grand roulette** — where the eight inward low-score lanes merge through the collector funnel |
| ![Numa kuruun](Docs/screenshots/numa-kuruun.png) **Numa kuruun (Tower G)** — three stacked dishes, each drawing again through a win hole / miss hole pair | ![Pachinko board](Docs/screenshots/pachinko.png) **Pachinko board (Tower B)** — 22 pins feeding a three-step chucker |
| ![Garapon](Docs/screenshots/garapon.png) **Garapon (Tower H)** — a karakorotta-style ring of 8 holes into a horizontal drum | ![Pocket disc](Docs/screenshots/pocket-disc.png) **Tilted pocket disc (Tower E)** — the whole dish turns at 18°/s; pocket arc width sets the odds |

Dishes, boards and troughs that **enclose the balls are clear acrylic**. This shows the interior without carving the geometry, so no collider changed at all (see [`Docs/DESIGN-materials.md`](Docs/DESIGN-materials.md) §2.5).

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

1. Open the project in Unity, then open `Assets/Scenes/ParkScene_v2.unity`
2. Run the menu item **`Tools > Build RouletteSphere Park (v2)`** to generate the park (idempotent — re-running rebuilds the same layout)
3. Press Play. Balls are fed in one by one and the loop starts

| Key | Action |
| --- | --- |
| `C` | Toggle the demo director (auto-picks shots from 8 channels) |
| `V` | Cut to the next shot |
| `Tab` | Follow the next ball |
| `0` | Back to the overview camera (releases auto-follow) |

| Menu | What it does |
| --- | --- |
| `Tools > Build RouletteSphere Park (v2)` | Generate the park (idempotent) |
| `Tools > Run Soak (36 balls)` | Long-run test. Records laps, score, **off-course / stalled / lost balls** and fixed-camera blind spots to `Docs/soak_*.json` and `Docs/camera_coverage.json` |
| `Tools > Capture Showcase Shots` | Re-take the screenshots used in this README into `Docs/screenshots/` |

> `Assets/Scenes/ParkScene_v1.unity` plus `Tools > Build RouletteSphere Greybox` is the finished v1 machine. It is frozen and kept for reference.

## Putting a character on a ball

Balls can wear a "spherized character" texture instead of the plain numbered look.

1. Drop an image into `Assets/Textures/BallSkins/` (the whole folder is outside version control)
2. Call `LotteryBall.SetCharacterTexture(tex)` at runtime. Pass `null` to go back to the numbered ball

The UV is a **two-disc front/back layout** (2:1 canvas; left disc = front hemisphere, right disc = mirrored back hemisphere). A drawing template lives at [`Docs/BallUV_Template.png`](Docs/BallUV_Template.png). The number patch sits on the top and bottom poles (10×10 atlas in `NumberAtlas.png`).

To lift the ball into your own project, use the porting kit `LotteryBallKit.unitypackage` (built from `Assets/LotteryBallKit/`).

> [!IMPORTANT]
> Images placed in `BallSkins/` are **not** covered by this repository's licence (CC BY 4.0); they follow the licence of the source image. See [`LICENSE-ASSETS.md`](LICENSE-ASSETS.md) §2.

## Layout

| Path | Contents |
| --- | --- |
| `Assets/Scenes/ParkScene_v2.unity` | v2 main scene (multi-tower park) |
| `Assets/Scenes/ParkScene_v1.unity` | v1 finished machine (frozen, for reference) |
| `Assets/Editor/ParkBuilder.cs` | v2 park builder. **Computes no coordinates** — it only interprets `ParkAssembly.fbx` + `ParkAssembly.params.json` |
| `Assets/Editor/GreyboxKit.cs` | Shared builder helpers (clearance rules and pitfall workarounds baked in) |
| `Assets/Editor/SoakRunner.cs` / `ShowcaseCapture.cs` | Soak-test launcher / README screenshot capture |
| `Assets/Scripts/` | `LotteryBall`, `ScoreZone`, `LapGate`, `Rotator`, `Oscillator`, `BallLift`, `BallSpawner`, the camera-direction set, `SoakRecorder`, `CameraCoverage` |
| `Assets/Models/ParkAssembly.fbx` | **The single placement artefact** (123 meshes + function markers) |
| `Assets/Textures/NumberAtlas.png` | Number atlas, 0–99 |
| `Assets/Textures/BallSkins/` | **Character skins go here (untracked — bring your own)** |
| `BlenderSources/ParkAssembly.blend` | **Placement source of truth.** Meshes are linked from five source `.blend` files |
| `BlenderSources/*.blend` | Mesh sources (`ParkBase`, `TowerA`, `TowerBCH`, `TowerDE`, `TowerFG`) |
| `Docs/DESIGN-v2.md` | v2 design doc (concept, towers, clearance rules, scoring) — **structural source of truth** |
| `Docs/DESIGN-materials.md` | Exterior design doc (palette, see-through shells, lighting) — **visual source of truth** |
| `Docs/screenshots/` | Images used in this README (generated by `Tools > Capture Showcase Shots`) |
| `Docs/*.py` | Blender-side tools (assembly build, verification, export, mesh health check) |
| `AGENTS.md` | AI agent configuration and the list of physics pitfalls — **operational source of truth** |

Every visible mesh is modelled in Blender; Unity handles physics, scoring and transport. Unity primitives are used only for invisible trigger colliders.

## AI agent configuration

The source of truth for AI agent configuration is [`AGENTS.md`](AGENTS.md). `CLAUDE.md` is a thin pointer to it, so **add or change configuration in `AGENTS.md` only**. The physics pitfalls hit during implementation (arch jams, tunnelling, coordinate conversion, broken meshes — 56 entries) are collected in `AGENTS.md` §3.

## License

This repository is **CC BY 4.0** — except for ball skins, which follow the licence of their source image. Please check the scope before reuse.

> RouletteSphereChaser © 2026 by ラジアン(柏木主税) / RadianN_kswg is licensed under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/)

- Full licence text: [`LICENSE`](LICENSE)
- **Per-asset scope and third-party notices**: [`LICENSE-ASSETS.md`](LICENSE-ASSETS.md)

## Links

- Hyakka Ryouran Laboratory creative DB: <https://database.numbertales-radiann.net/>
- Number Tales official site: <https://www.numbertales-radiann.com/>

© ラジアン(柏木主税) / ©RadianN_kswg
