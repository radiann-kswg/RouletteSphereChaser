# NumberAtlas: 10x10 tiles, white bg + black digits (no ring).
# tile(col,row_from_top) -> number (9-row)*10+col ; UV origin bottom-left.
from PIL import Image, ImageDraw, ImageFont
TILE, SS = 128, 4          # supersample x4
FIT_R = 40                 # 数字の外接円半径(px)。バッジ半径(56.3px)に対し約71%
FONT = "Assets/Fonts/PenchantManufacture.otf"
def fit_size(text):
    lo, hi = 8, 200
    while lo < hi:
        mid = (lo + hi + 1) // 2
        f = ImageFont.truetype(FONT, mid * SS)
        l, t, r, b = f.getbbox(text)
        if ((r - l) ** 2 + (b - t) ** 2) ** 0.5 / 2 <= FIT_R * SS: lo = mid
        else: hi = mid - 1
    return lo
# 全番号のフィットサイズの最小値に統一（数字の高さを揃える・User指定）
COMMON = min(fit_size(str(n)) for n in range(100))
atlas = Image.new("RGB", (TILE * 10, TILE * 10), "white")
for n in range(100):
    col, row = n % 10, 9 - n // 10
    tile = Image.new("RGB", (TILE * SS, TILE * SS), "white")
    d = ImageDraw.Draw(tile)
    f = ImageFont.truetype(FONT, COMMON * SS)
    l, t, r, b = f.getbbox(str(n))
    d.text(((TILE * SS - (r - l)) / 2 - l, (TILE * SS - (b - t)) / 2 - t), str(n), font=f, fill="black")
    atlas.paste(tile.resize((TILE, TILE), Image.LANCZOS), (col * TILE, row * TILE))
atlas.save("Assets/Textures/NumberAtlas.png")
print("wrote", atlas.size)
