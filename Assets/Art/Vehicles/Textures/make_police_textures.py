"""
Генерация текстур полицейской ливреи и светодиодной балки.
Запуск:  python3 make_police_textures.py
Результат: Police_Livery.png (2048x2048, с альфой), Police_LightBar_Emission.png (1024x256)
"""
from PIL import Image, ImageDraw, ImageFont

W = H = 2048
img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
d = ImageDraw.Draw(img)

WHITE = (236, 238, 242, 255)
NAVY = (18, 26, 48, 255)
RED = (196, 32, 44, 255)
BLUE = (22, 58, 140, 255)
BLACK = (12, 14, 20, 255)

FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
f_big = ImageFont.truetype(FONT, 300)
f_mid = ImageFont.truetype(FONT, 150)
f_sm = ImageFont.truetype(FONT, 90)


def center_text(draw, y, text, font, fill, spacing=8):
    widths = [draw.textbbox((0, 0), ch, font=font)[2] for ch in text]
    total = sum(widths) + spacing * (len(text) - 1)
    x = (W - total) / 2
    for ch, w in zip(text, widths):
        draw.text((x, y), ch, font=font, fill=fill)
        x += w + spacing


# верхняя белая полоса POLICE
d.rectangle([0, 120, W, 460], fill=WHITE)
center_text(d, 150, "POLICE", f_big, BLACK)

# нижняя тёмно-синяя полоса + красная линия
d.rectangle([0, H - 360, W, H - 120], fill=NAVY)
d.rectangle([0, H - 372, W, H - 360], fill=BLUE)
d.rectangle([0, H - 120, W, H - 108], fill=RED)

# шашечки по краям
sq = 44
for i in range(4):
    for j in range(9):
        y0 = 520 + j * sq
        col_a = WHITE if (j % 2 == 0) else BLACK
        col_b = BLACK if (j % 2 == 0) else WHITE
        x0 = 40 + i * sq
        d.rectangle([x0, y0, x0 + sq, y0 + sq], fill=col_a if (i + j) % 2 == 0 else col_b)
        x1 = W - 40 - (i + 1) * sq
        d.rectangle([x1, y0, x1 + sq, y0 + sq], fill=col_a if (i + j) % 2 == 0 else col_b)

center_text(d, 620, "911", f_mid, WHITE)
center_text(d, 800, "NEON DISTRICT PD", f_sm, (150, 190, 255, 255))
center_text(d, 930, "UNIT 07", f_sm, (255, 255, 255, 255))

img.save("Police_Livery.png")

# эмиссия светодиодной балки: красная половина / синяя половина с затуханием
em = Image.new("RGBA", (1024, 256), (0, 0, 0, 255))
px = em.load()
for x in range(1024):
    for y in range(256):
        dx = abs(x - 512) / 512
        dy = abs(y - 128) / 128
        v = max(0.0, 1.0 - (dx * 0.9 + dy * 0.6))
        if x < 512:
            px[x, y] = (int(255 * v), int(40 * v), int(60 * v), 255)
        else:
            px[x, y] = (int(50 * v), int(110 * v), int(255 * v), 255)
em.save("Police_LightBar_Emission.png")
print("ok: Police_Livery.png, Police_LightBar_Emission.png")
