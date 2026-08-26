import math
from PIL import Image, ImageDraw

SCALE = 4
SIZE = 17
CENTER = (SIZE * SCALE / 2, SIZE * SCALE / 2)


def points():
    result = []
    for index in range(10):
        angle = -math.pi / 2 + index * math.pi / 5
        radius = (7 if index % 2 == 0 else 3.2) * SCALE
        result.append((
            CENTER[0] + math.cos(angle) * radius,
            CENTER[1] + math.sin(angle) * radius,
        ))
    return result


def save(name, fill, outline):
    image = Image.new("RGBA", (SIZE * SCALE, SIZE * SCALE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    draw.polygon(points(), fill=fill, outline=outline, width=1 * SCALE)
    image.resize((SIZE, SIZE), Image.Resampling.LANCZOS).save(
        f"src/ServerBrowser/assets/{name}.png"
    )


save("star-on", (255, 217, 90, 255), (255, 238, 160, 255))
save("star-off", (0, 0, 0, 0), (130, 125, 115, 255))
