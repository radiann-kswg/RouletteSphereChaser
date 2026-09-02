# 外装テクスチャ（DESIGN-materials.md 4章・第2段）を役割ごとに手続き生成する。
#   python Docs/gen_park_textures.py [--force]      … リポジトリ直下で実行
# 出力:
#   Assets/Textures/ParkSamples/<役割>.png + roles.txt … **サンプル（git管轄・LFS）**。ParkBuilder のフォールバック元
#   Assets/Textures/Park/<マテリアル名>.png             … **作業用（git管轄外）**。ParkBuilder はまずこちらを _BaseMap へ差す。
#       ボールスキンと同じ運用: ライセンス適用外の画像を各自ここへ置いて差し替える。**既にあるファイルは上書きしない**（--force で上書き）
#   PSD テンプレートは Docs/gen_park_texture_psd.py（Docs/ParkTextures PSD/<マテリアル名>.psd）
# 方針: URP/Lit は BaseMap×BaseColor なので、テクスチャは「白地に薄い陰影」だけ＝配色（2章）はそのまま生きる。派手にしない（0章）。
import os
import shutil
import sys
import numpy as np
from PIL import Image

N = 1024
SAMPLES, OUT = "Assets/Textures/ParkSamples", "Assets/Textures/Park"
ROLES = {  # DESIGN-materials.md 2章「マテリアル → 役割」。透過アクリル（SeeThroughMats）は貼らない
    "CABINET":   ["ParkBase", "DrainStation"],
    "CHROME":    ["LiftGuide", "TowerF_CatchTray", "TowerF_MissTray", "TowerH_CatchFunnel", "TowerE_Pickup"],
    "LANE":      ["TowerA_HighLane"],
    "JACKPOT":   ["TowerG_JPRail", "TowerF_JPTube"],
    "DECK":      ["TowerA_Distributor", "TowerA_RouletteBowl", "TowerA_GrandKuruun", "TowerD_Kuruun",
                  "TowerG_NumaBoard", "TowerF_SpinnerDish", "TowerF_Separator", "TowerE_Deck"],
    "ROTOR":     ["TowerA_MiniRouletteWheel", "TowerA_GrandRouletteWheel", "TowerA_RouletteWheel", "TowerE_Gear",
                  "TowerE_Disc", "TowerE_Wheel", "TowerH_Drum", "TowerH_Swing", "TowerC_Seesaw", "Greybox_Accent"],
    "ROTOR_HI":  ["TowerE_DiscHi"],
    "SCORE":     ["TowerA_ScoreGate", "TowerB_StepChucker"],
    "SHOWPIECE": ["TowerA_Spiral"],
}
u, v = np.meshgrid(np.arange(N) / N, np.arange(N) / N)   # u=横(周方向) v=縦(断面)
rng = np.random.default_rng(78)


def hairline(strength=0.06, lines=1.0):
    """横方向のヘアライン（uに沿った条線）。行ごとの明暗ノイズをuに引き伸ばす"""
    row = np.repeat(rng.normal(0, 1, (N, 1)), N, axis=1) * lines
    fine = rng.normal(0, 0.3, (N, N))
    return 1.0 - strength * np.clip(np.abs(row + fine) * 0.5, 0, 1)


def knurl(pitch=24, strength=0.08):
    """ローレット（45°の交差溝）"""
    a = np.abs(((u + v) * N / pitch) % 1.0 - 0.5) * 2
    b = np.abs(((u - v) * N / pitch) % 1.0 - 0.5) * 2
    return 1.0 - strength * np.maximum(a, b) ** 6


def punch(pitch=40, r=0.30, strength=0.18):
    """パンチングメタル（丸穴の等間隔）"""
    cx = (u * N / pitch) % 1.0 - 0.5
    cy = (v * N / pitch) % 1.0 - 0.5
    return 1.0 - strength * (np.hypot(cx, cy) < r)


def dial(sectors=36, strength=0.05):
    """抽選盤のフレット割り: u=方位の等分割扇形（交互に薄いトーン差）＋細い境界線"""
    s = np.floor(u * sectors)
    tone = 1.0 - strength * (s % 2)
    edge = np.abs((u * sectors) % 1.0 - 0.5) > 0.49
    return tone - 0.10 * edge


def chevron(count=12, strength=0.05):
    """回転方向を示す矢羽根（u方向に流れる斜め帯）"""
    t = ((u * count) + np.abs(v - 0.5) * 0.8) % 1.0
    return 1.0 - strength * (t < 0.35)


def panel(strength=0.10):
    """得点パネル: 縁に向かって薄く落ちる額縁。中央は白のまま（TMPラベルの背景）"""
    d = np.maximum(np.abs(u - 0.5), np.abs(v - 0.5)) * 2
    return 1.0 - strength * np.clip((d - 0.8) / 0.2, 0, 1)


def frost(strength=0.04):
    """乳白アクリルのすりガラス感"""
    n = np.kron(rng.normal(0, 1, (N // 8, N // 8)), np.ones((8, 8)))
    return 1.0 - strength * np.clip(np.abs(n) * 0.5, 0, 1)


GEN = {
    "CABINET": lambda: punch(),
    "CHROME": lambda: hairline(),
    "LANE": lambda: hairline(strength=0.03),
    "JACKPOT": lambda: knurl(),
    "DECK": lambda: dial(),
    "ROTOR": lambda: chevron() * hairline(strength=0.03),
    "ROTOR_HI": lambda: chevron(strength=0.03) * hairline(strength=0.02),
    "SCORE": lambda: panel(),
    "SHOWPIECE": lambda: frost(),
}

if __name__ == "__main__":
    force = "--force" in sys.argv
    os.makedirs(SAMPLES, exist_ok=True)
    os.makedirs(OUT, exist_ok=True)
    copied = 0
    with open(f"{SAMPLES}/roles.txt", "w", encoding="utf-8") as f:
        for role, mats in ROLES.items():
            sample = f"{SAMPLES}/{role}.png"
            Image.fromarray((np.clip(GEN[role](), 0, 1) * 255).astype(np.uint8), "L").convert("RGB").save(sample)
            for m in mats:
                f.write(f"{m}={role}\n")
                dst = f"{OUT}/{m}.png"
                if force or not os.path.exists(dst):
                    shutil.copyfile(sample, dst)
                    copied += 1
            print(role, len(mats))
    print(f"samples -> {SAMPLES}/ ; copied {copied} to {OUT}/ (skip existing unless --force)")
