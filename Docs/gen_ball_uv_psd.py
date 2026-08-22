# BallUV_Template.psd : テクスチャ作成用のレイヤー付きテンプレート（2048x1024 / 2:1）
# 入力: Blenderが書き出す Temp/uv_dump.json（本体UVポリゴン＋番号デカール縁）
#   Blender側: docs/AGENTS.md 参照（body_uv() で faces / rims をダンプ）
# 出力レイヤー（下→上）: 背景(白) / 下絵(空) / UVガイド / 番号デカール範囲
import json
import numpy as np
from PIL import Image, ImageDraw, ImageFont
import pytoshop
from pytoshop import image_data
from pytoshop.user import nested_layers
from pytoshop.enums import BlendMode, ColorMode, Compression

W, H, SS = 2048, 1024, 2
FONT = "Assets/Fonts/PenchantManufacture.otf"
d = json.load(open("Temp/uv_dump.json"))
px = lambda p: (p[0]*W*SS, (1-p[1])*H*SS)

def canvas():
    return Image.new("RGBA", (W*SS, H*SS), (0, 0, 0, 0))

guide = canvas(); g = ImageDraw.Draw(guide)
for f in d["faces"]:
    g.polygon([px(p) for p in f], outline=(90, 90, 90, 255), width=SS)
f = ImageFont.truetype(FONT, 34*SS)
g.text((W*SS*0.25, H*SS*0.955), "FRONT", font=f, fill=(90, 90, 90, 255), anchor="mm")
g.text((W*SS*0.75, H*SS*0.955), "BACK",  font=f, fill=(90, 90, 90, 255), anchor="mm")

badge = canvas(); b = ImageDraw.Draw(badge)
for rim in d["rims"]:
    for p, q in zip(rim, rim[1:]):
        if p[1] != q[1]:            # 前半球/後半球の切れ目はつながない
            continue
        b.line([px(p[0]), px(q[0])], fill=(220, 60, 60, 255), width=3*SS)

def layer(name, img, visible=True, opacity=255):
    a = np.asarray(img.resize((W, H), Image.LANCZOS))
    ch = {-1: a[..., 3], 0: a[..., 0], 1: a[..., 1], 2: a[..., 2]}
    return nested_layers.Image(name=name, visible=visible, opacity=opacity, blend_mode=BlendMode.normal,
                               top=0, left=0, bottom=H, right=W, channels=ch)

white = Image.new("RGBA", (W*SS, H*SS), (255, 255, 255, 255))
layers = [                                   # pytoshopは先頭が最前面
    layer("番号デカール範囲", badge),
    layer("UVガイド", guide),
    layer("下絵", canvas()),
    layer("背景", white),
]
psd = nested_layers.nested_layers_to_psd(layers, color_mode=ColorMode.rgb, compression=Compression.zip)
# 統合画像（サムネイル/PSD対応の弱いビューア用）を自前で焼き込む
flat = Image.alpha_composite(Image.alpha_composite(white, guide), badge).convert("RGB").resize((W, H), Image.LANCZOS)
a = np.asarray(flat).transpose(2, 0, 1).copy()
psd.image_data = image_data.ImageData(channels=a, compression=Compression.raw)  # PILなど統合画像しか読まない環境向け（rleはpackbits拡張が要る）
with open("Docs/BallUV_Template.psd", "wb") as fp:
    psd.write(fp)
print("wrote Docs/BallUV_Template.psd")
