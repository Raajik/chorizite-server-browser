from PIL import Image, ImageDraw

# Matches the Discord mark: supersampled, transparent, badge CSS supplies the fill.
SCALE = 8
SIZE = 28
BIG = SIZE * SCALE
STROKE = int(7 * BIG / 112)


def s(*values):
    return tuple(int(value * BIG / 112) for value in values)


mask = Image.new("L", (BIG, BIG), 0)
draw = ImageDraw.Draw(mask)

# Globe: outer circle, one meridian ellipse, two latitude lines.
draw.ellipse(s(12, 12, 100, 100), outline=255, width=STROKE)
draw.ellipse(s(42, 12, 70, 100), outline=255, width=STROKE)
draw.line([s(17, 42), s(95, 42)], fill=255, width=STROKE)
draw.line([s(17, 70), s(95, 70)], fill=255, width=STROKE)

icon = Image.new("RGBA", (BIG, BIG), (198, 226, 242, 255))
icon.putalpha(mask)
icon.resize((SIZE, SIZE), Image.Resampling.LANCZOS).save(
    "src/ServerBrowser/assets/web.png"
)
