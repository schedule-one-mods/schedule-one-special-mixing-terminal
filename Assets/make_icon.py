"""Regenerate original mod artwork with Pillow; not needed to build/run the mod."""
from pathlib import Path
from PIL import Image, ImageDraw

scale = 4
image = Image.new("RGBA", (256 * scale, 256 * scale))
draw = ImageDraw.Draw(image)

def polygon(points, color):
    draw.polygon([(int(x * scale), int(y * scale)) for x, y in points], fill=color)

def box(bounds, radius, color):
    draw.rounded_rectangle(tuple(int(v * scale) for v in bounds), radius=radius * scale, fill=color)

def line(points, color, width):
    draw.line([(int(x * scale), int(y * scale)) for x, y in points], fill=color, width=width * scale, joint="curve")

# Low-poly CRT silhouette and warm cabinet bevels.
polygon([(38, 24), (206, 24), (221, 39), (221, 172), (204, 186), (38, 186), (25, 172), (25, 39)], "#62513e")
polygon([(38, 24), (206, 24), (207, 169), (38, 169)], "#d9c895")
polygon([(207, 25), (221, 39), (221, 172), (207, 181)], "#9e865a")
polygon([(26, 39), (38, 25), (38, 168), (26, 173)], "#b3a173")
box((46, 38, 198, 155), 8, "#776849")
box((53, 45, 191, 147), 6, "#122e36")
box((58, 50, 186, 141), 4, "#214550")
# A flask and a short three-stage route: legible at inventory size.
polygon([(111, 65), (137, 65), (137, 75), (132, 75), (132, 88), (152, 119), (149, 127), (99, 127), (96, 119), (116, 88), (116, 75), (111, 75)], "#dcebd7")
polygon([(111, 105), (137, 105), (147, 120), (144, 122), (104, 122), (101, 120)], "#e9ad40")
box((118, 109, 123, 114), 2, "#fff3b3")
line([(69, 70), (82, 70), (82, 96), (90, 96)], "#7ed5bb", 4)
for bounds in [(65, 65, 74, 74), (159, 82, 168, 91), (169, 115, 178, 124)]:
    box(bounds, 2, "#7ed5bb")
line([(163, 91), (163, 119), (169, 119)], "#7ed5bb", 3)
box((51, 161, 86, 165), 1, "#928258")
box((179, 160, 187, 165), 2, "#88c872")
# Stand, desktop unit and keyboard.
polygon([(101, 181), (153, 181), (163, 194), (90, 194)], "#aa9466")
polygon([(34, 191), (211, 191), (229, 219), (223, 231), (25, 231), (19, 220)], "#66543d")
polygon([(34, 187), (211, 187), (226, 217), (22, 217)], "#dbc78e")
polygon([(22, 217), (226, 217), (223, 226), (25, 226)], "#b39a64")
polygon([(49, 194), (187, 194), (197, 211), (39, 211)], "#7c7258")
for row in range(2):
    for column in range(11):
        x = 51 + column * 12 - row * 3
        y = 196 + row * 7
        box((x, y, x + 8, y + 4), 1, "#e8d9ac")
image = image.resize((256, 256), Image.Resampling.LANCZOS)
image.save(Path(__file__).with_name("terminal-icon.png"))
