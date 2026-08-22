# BallUV_Template.png : 2:1キャンバスの本体UVガイド（前半球=左円 / 後半球=右円）
# 入力: Blenderが書き出す Temp/uv_dump.json（本体UVポリゴン＋番号バッジ縁）
import json
from PIL import Image, ImageDraw
W, H, SS = 2048, 1024, 2
d = json.load(open("Temp/uv_dump.json"))
im = Image.new("RGB", (W*SS, H*SS), "black")
dr = ImageDraw.Draw(im)
px = lambda p: (p[0]*W*SS, (1-p[1])*H*SS)
for f in d["faces"]:
    dr.polygon([px(p) for p in f], fill=(214, 214, 214), outline=(40, 40, 40), width=SS)
for rim in d["rims"]:                                   # 番号バッジが覆う範囲（前後で分断）
    for a, b in zip(rim, rim[1:]):
        if a[1] != b[1]: continue                       # 前半球/後半球の切れ目はつながない
        dr.line([px(a[0]), px(b[0])], fill=(220, 60, 60), width=3*SS)
im.resize((W, H), Image.LANCZOS).save("Docs/BallUV_Template.png")
print("wrote Docs/BallUV_Template.png")
