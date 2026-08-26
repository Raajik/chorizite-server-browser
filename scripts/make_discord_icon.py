from PIL import Image, ImageDraw

size = 32
image = Image.new("RGBA", (size, size), (88, 101, 242, 255))
draw = ImageDraw.Draw(image)
# Compact Discord-inspired controller mark, designed for a 17px UI slot.
draw.rounded_rectangle((6, 8, 26, 23), radius=7, fill=(255, 255, 255, 255))
draw.ellipse((10, 13, 14, 17), fill=(88, 101, 242, 255))
draw.ellipse((18, 13, 22, 17), fill=(88, 101, 242, 255))
draw.polygon([(8, 19), (5, 25), (12, 22)], fill=(255, 255, 255, 255))
draw.polygon([(24, 19), (27, 25), (20, 22)], fill=(255, 255, 255, 255))
image.save("src/ServerBrowser/assets/discord.png")
