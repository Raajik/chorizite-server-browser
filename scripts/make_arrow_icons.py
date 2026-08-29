from PIL import Image, ImageDraw

SCALE = 4
SIZE = 11
FILL = (198, 190, 172, 255)


def save(name, points):
    image = Image.new("RGBA", (SIZE * SCALE, SIZE * SCALE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    draw.polygon([(x * SCALE, y * SCALE) for x, y in points], fill=FILL)
    image.resize((SIZE, SIZE), Image.Resampling.LANCZOS).save(
        f"src/ServerBrowser/assets/{name}.png"
    )


save("arrow-up", [(5.5, 2), (9.5, 8), (1.5, 8)])
save("arrow-down", [(5.5, 9), (9.5, 3), (1.5, 3)])
# Right-pointing chevron for expandable server rows; drawn hollow on the
# favorites row so it reads as "click to expand" rather than "reorder".
save("chevron-right", [(4, 2), (8.5, 5.5), (4, 9)])
save("chevron-down", [(2, 4), (5.5, 8.5), (9, 4)])
