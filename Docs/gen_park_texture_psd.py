# 外装テクスチャ作成用のレイヤー付きPSDテンプレート（マテリアルごと・1024x1024）。BallUV_Template.psd と同じ作法。
#   1) Blender で ParkAssembly.blend を開き Docs/dump_park_uv.py を exec → Temp/park_uv_dump.json
#      （{マテリアル名: {メッシュデータ名: [[[u,v],...], ...]}}）
#   2) python Docs/gen_park_texture_psd.py [マテリアル名...]   … 省略時は全部
# 出力: Docs/ParkTextures PSD/Samples/<マテリアル名>.psd（**git管轄・LFS**）。自分の作画版は Docs/ParkTextures PSD/ 直下へ（管轄外）
# 要 `pip install pytoshop packbits`（packbits が無いと統合画像が raw になり 3MB/枚 → 約0.3MB/枚）
# レイヤー（下→上）: 背景(白) / 下絵(空) / 模様サンプル(Park/Samples の役割PNG) / UVガイド(メッシュごとに色分け＋凡例)
import json
import os
import sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont
import pytoshop
from pytoshop import image_data
from pytoshop.user import nested_layers
from pytoshop.enums import BlendMode, ColorMode, Compression

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gen_park_textures import ROLES, SAMPLES   # noqa: E402  役割表を共有
try:                                            # pytoshop 同梱の packbits は未ビルドなので pip 版を差す
    import packbits
    import pytoshop.codecs
    pytoshop.codecs.packbits = packbits
    FLAT = Compression.rle
except ImportError:
    FLAT = Compression.raw

N, SS = 1024, 2
FONT = "Assets/Fonts/PenchantManufacture.otf"
OUT = "Docs/ParkTextures PSD/Samples"
COLORS = [(90, 90, 90), (200, 60, 60), (40, 120, 220), (30, 150, 70), (200, 130, 20), (140, 60, 180)]
role_of = {m: r for r, ms in ROLES.items() for m in ms}
dump = json.load(open("Temp/park_uv_dump.json"))
px = lambda p: ((p[0] % 1.0) * N * SS, (1 - p[1] % 1.0) * N * SS)   # UVは 0..1 に畳む（タイリング前提）


def canvas():
    return Image.new("RGBA", (N * SS, N * SS), (0, 0, 0, 0))


def layer(name, img, visible=True, opacity=255):
    a = np.asarray(img.resize((N, N), Image.LANCZOS))
    ch = {-1: a[..., 3], 0: a[..., 0], 1: a[..., 1], 2: a[..., 2]}
    return nested_layers.Image(name=name, visible=visible, opacity=opacity, blend_mode=BlendMode.normal,
                               top=0, left=0, bottom=N, right=N, channels=ch)


def build(mat):
    meshes = dump.get(mat)
    if not meshes:
        print("skip (UVダンプ無し):", mat)
        return
    guide = canvas()
    g = ImageDraw.Draw(guide)
    font = ImageFont.truetype(FONT, 22 * SS)
    for i, (mesh, faces) in enumerate(meshes.items()):
        col = COLORS[i % len(COLORS)] + (255,)
        for f in faces:
            if len(f) >= 3:
                g.polygon([px(p) for p in f], outline=col, width=SS)
        g.text((12 * SS, (12 + 26 * i) * SS), f"{mesh}  ({len(faces)} faces)", font=font, fill=col)
    g.text((N * SS - 12 * SS, N * SS - 12 * SS), f"{mat}  [{role_of.get(mat, '-')}]  u=horizontal / v=vertical",
           font=font, fill=(90, 90, 90, 255), anchor="rd")

    role = role_of.get(mat)
    sample = Image.open(f"{SAMPLES}/{role}.png").convert("RGBA").resize((N * SS, N * SS)) if role else canvas()
    white = Image.new("RGBA", (N * SS, N * SS), (255, 255, 255, 255))
    layers = [                                   # pytoshopは先頭が最前面
        layer("UVガイド", guide),
        layer("模様サンプル", sample, opacity=255),
        layer("下絵", canvas()),
        layer("背景", white),
    ]
    psd = nested_layers.nested_layers_to_psd(layers, color_mode=ColorMode.rgb, compression=Compression.zip)
    flat = Image.alpha_composite(Image.alpha_composite(white, sample), guide).convert("RGB").resize((N, N), Image.LANCZOS)
    psd.image_data = image_data.ImageData(channels=np.asarray(flat).transpose(2, 0, 1).copy(), compression=FLAT)
    os.makedirs(OUT, exist_ok=True)
    with open(f"{OUT}/{mat}.psd", "wb") as fp:
        psd.write(fp)
    print("wrote", f"{OUT}/{mat}.psd", len(meshes), "meshes")


if __name__ == "__main__":
    targets = sys.argv[1:] or [m for ms in ROLES.values() for m in ms if m in dump]
    for m in targets:
        build(m)
