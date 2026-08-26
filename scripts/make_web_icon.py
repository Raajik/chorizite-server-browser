from PIL import Image, ImageDraw

size = 32
image = Image.new("RGBA", (size, size), (35, 65, 79, 255))
draw = ImageDraw.Draw(image)
line = (142, 201, 232, 255)
# Globe mark sized for the same 14px slot the Discord icon uses.
draw.ellipse((6, 6, 26, 26), outline=line, width=3)
draw.ellipse((12, 6, 20, 26), outline=line, width=2)
draw.line((7, 13, 25, 13), fill=line, width=2)
draw.line((7, 19, 25, 19), fill=line, width=2)
image.save("src/ServerBrowser/assets/web.png")
