from PIL import Image, ImageDraw

# Drawn large and downsampled so the 14px slot gets antialiased edges, and left
# transparent so the badge CSS supplies the blurple behind it.
SCALE = 8
SIZE = 28
BIG = SIZE * SCALE


def s(*values):
    return tuple(int(value * BIG / 112) for value in values)


mask = Image.new("L", (BIG, BIG), 0)
draw = ImageDraw.Draw(mask)

# Clyde: wide rounded face, stubby flared tails, and a concave underside.
draw.rounded_rectangle(s(10, 18, 102, 70), radius=int(30 * BIG / 112), fill=255)
draw.polygon([s(22, 56), s(10, 88), s(46, 72)], fill=255)
draw.polygon([s(90, 56), s(102, 88), s(66, 72)], fill=255)
draw.ellipse(s(22, 70, 90, 104), fill=0)
draw.ellipse(s(30, 33, 48, 57), fill=0)
draw.ellipse(s(64, 33, 82, 57), fill=0)

icon = Image.new("RGBA", (BIG, BIG), (255, 255, 255, 255))
icon.putalpha(mask)
icon.resize((SIZE, SIZE), Image.Resampling.LANCZOS).save(
    "src/ServerBrowser/assets/discord.png"
)
