from pathlib import Path
import argparse
import math

from PIL import Image, ImageDraw, ImageFont, ImageOps


ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "output" / "branding"
parser = argparse.ArgumentParser()
formats = parser.add_mutually_exclusive_group()
formats.add_argument("--wallpaper", action="store_true")
formats.add_argument("--clean-cover", action="store_true")
formats.add_argument("--portrait", action="store_true")
options = parser.parse_args()
no_title = options.wallpaper or options.clean_cover or options.portrait
SCALE = 3
WIDTH, HEIGHT = (1920, 620) if options.wallpaper else ((1080, 1920) if options.portrait else (1920, 1080))
BACKGROUND = "#161616"
CREAM = "#FFF9E9"
YELLOW = "#F8D780"
CORAL = "#E98C73"
GREEN = "#99B889"
canvas = Image.new("RGB", (WIDTH * SCALE, HEIGHT * SCALE), BACKGROUND)
brush = ImageDraw.Draw(canvas)


def ellipse(bounds, fill, outline=None, width=1):
    brush.ellipse(tuple(round(value * SCALE) for value in bounds), fill=fill,
        outline=outline, width=round(width * SCALE))


def line(points, fill, width):
    scaled = [(round(horizontal * SCALE), round(vertical * SCALE)) for horizontal, vertical in points]
    brush.line(scaled, fill=fill, width=round(width * SCALE), joint="curve")
    radius = width / 2
    for horizontal, vertical in (points[0], points[-1]):
        ellipse((horizontal - radius, vertical - radius, horizontal + radius, vertical + radius), fill)


def path(commands, fill=None, outline=None, width=1):
    vertices = []
    previous = None
    origin = None
    for operation, *values in commands:
        if operation == "M":
            previous = tuple(values)
            origin = previous
            vertices.append(previous)
        elif operation == "L":
            previous = tuple(values)
            vertices.append(previous)
        elif operation == "C":
            start_x, start_y = previous
            first_x, first_y, second_x, second_y, end_x, end_y = values
            for step in range(1, 101):
                fraction = step / 100
                inverse = 1 - fraction
                vertices.append((
                    inverse ** 3 * start_x + 3 * inverse ** 2 * fraction * first_x
                    + 3 * inverse * fraction ** 2 * second_x + fraction ** 3 * end_x,
                    inverse ** 3 * start_y + 3 * inverse ** 2 * fraction * first_y
                    + 3 * inverse * fraction ** 2 * second_y + fraction ** 3 * end_y,
                ))
            previous = (end_x, end_y)
        elif operation == "Z":
            vertices.append(origin)
    if fill:
        brush.polygon([(round(horizontal * SCALE), round(vertical * SCALE))
            for horizontal, vertical in vertices], fill=fill)
    if outline:
        line(vertices, outline, width)


def title(word, size, tracking, position, color, angle):
    font = ImageFont.truetype(str(ROOT / "Assets/ThirdParty/GoogleFonts/LilitaOne/LilitaOne-Regular.ttf"), size * SCALE)
    text_width = sum(font.getlength(character) for character in word) + tracking * SCALE * (len(word) - 1)
    layer = Image.new("RGBA", (math.ceil(text_width) + 36 * SCALE, (size + 60) * SCALE))
    painter = ImageDraw.Draw(layer)
    cursor = 18 * SCALE
    for character in word:
        painter.text((round(cursor), 18 * SCALE), character, font=font, fill=color, anchor="lt")
        cursor += font.getlength(character) + tracking * SCALE
    layer = layer.rotate(angle, resample=Image.Resampling.BICUBIC, expand=True)
    canvas.paste(layer, (round(position[0] * SCALE), round(position[1] * SCALE)), layer)


for vertical in range(25, HEIGHT, 43):
    for horizontal in range(25, WIDTH, 43):
        ellipse((horizontal - 1.1, vertical - 1.1, horizontal + 1.1, vertical + 1.1),
            "#35342F" if no_title else "#45443F")

if no_title:
    backdrop = canvas
    canvas = Image.new("RGBA", (1920 * SCALE, 1080 * SCALE))
    brush = ImageDraw.Draw(canvas)

line([(1297, 789), (1173, 593)], "#37362F", 54)
line([(1287, 779), (1163, 583)], CREAM, 27)
path([("M", 1168, 601), ("C", 1134, 619, 1109, 618, 1097, 589),
    ("L", 1060, 517), ("C", 1055, 506, 1061, 498, 1072, 491),
    ("L", 1116, 466), ("C", 1128, 460, 1139, 464, 1146, 475),
    ("L", 1187, 547), ("C", 1200, 570, 1194, 589, 1168, 601), ("Z",)],
    BACKGROUND, CREAM, 13)
line([(1094, 516), (1120, 562)], CREAM, 8)
line([(1117, 503), (1143, 549)], CREAM, 8)

line([(1603, 608), (1749, 445)], "#373126", 76)
line([(1593, 596), (1739, 433)], YELLOW, 58)
line([(1600, 587), (1738, 434)], BACKGROUND, 28)
ellipse((1273, 553, 1685, 965), "#25221D")
ellipse((1256, 536, 1668, 948), YELLOW)
ellipse((1275, 555, 1649, 929), BACKGROUND)
ellipse((1299, 579, 1625, 905), "#292923", "#5E5541", 3)
path([("M", 1306, 685), ("C", 1322, 637, 1354, 610, 1397, 596)], None, CREAM, 7)

path([("M", 1379, 658), ("C", 1366, 616, 1410, 604, 1447, 620),
    ("C", 1481, 634, 1495, 680, 1470, 706),
    ("C", 1439, 732, 1390, 699, 1379, 658), ("Z",)], CORAL)
path([("M", 1397, 646), ("C", 1422, 632, 1451, 652, 1452, 679)], None, "#F9C39B", 10)
path([("M", 1510, 724), ("C", 1538, 691, 1580, 715, 1584, 750),
    ("C", 1590, 790, 1553, 825, 1521, 808),
    ("C", 1490, 792, 1482, 758, 1510, 724), ("Z",)], "#C47E58")
line([(1542, 737), (1522, 769)], "#F3C99B", 8)
line([(1564, 750), (1544, 783)], "#F3C99B", 8)
path([("M", 1361, 785), ("C", 1384, 746, 1431, 757, 1449, 787),
    ("C", 1473, 825, 1446, 862, 1405, 861),
    ("C", 1366, 857, 1340, 822, 1361, 785), ("Z",)], CORAL)
line([(1380, 794), (1427, 823)], "#F9C39B", 9)

path([("M", 1350, 751), ("C", 1316, 742, 1323, 711, 1342, 705),
    ("C", 1347, 725, 1359, 732, 1350, 751), ("Z",)], GREEN)
path([("M", 1488, 866), ("C", 1464, 837, 1485, 818, 1513, 827),
    ("C", 1506, 846, 1501, 852, 1488, 866), ("Z",)], GREEN)
ellipse((1491, 654, 1505, 668), YELLOW)
ellipse((1462, 760, 1475, 773), YELLOW)
ellipse((1348, 837, 1360, 849), YELLOW)

path([("M", 1454, 493), ("C", 1430, 472, 1482, 460, 1459, 435)], None, "#8A846F", 7)
path([("M", 1507, 490), ("C", 1483, 456, 1534, 444, 1513, 417)], None, YELLOW, 8)

ellipse((1107, 826, 1221, 940), CORAL)
ellipse((1120, 839, 1208, 927), "#EFAD8B", BACKGROUND, 4)
for angle in (-90, 30, 150):
    radians = math.radians(angle)
    center_x = 1164 + math.cos(radians) * 23
    center_y = 883 + math.sin(radians) * 23
    ellipse((center_x - 5, center_y - 8, center_x + 5, center_y + 8), CREAM)
path([("M", 1122, 835), ("C", 1093, 830, 1095, 806, 1118, 809),
    ("C", 1124, 787, 1142, 797, 1137, 813),
    ("C", 1157, 812, 1159, 832, 1133, 838), ("Z",)], GREEN)
path([("M", 1208, 972), ("C", 1184, 950, 1193, 928, 1222, 933),
    ("C", 1239, 908, 1266, 920, 1254, 944),
    ("C", 1281, 954, 1261, 976, 1235, 971),
    ("C", 1225, 988, 1213, 988, 1208, 972), ("Z",)], GREEN)
line([(1213, 968), (1240, 942)], "#657C5D", 5)

if not no_title:
    title("KITCHEN", 285, 8, (122, 163), CREAM, 3)
    title("CLUB", 352, 16, (136, 485), YELLOW, -2)
    line([(180, 913), (413, 913)], "#5B5547", 3)
    line([(436, 913), (496, 913)], YELLOW, 3)
line([(1728, 762), (1745, 733)], "#8A846F", 7)
line([(1755, 793), (1789, 786)], YELLOW, 7)

OUTPUT.mkdir(parents=True, exist_ok=True)
if no_title:
    art_bounds = (660, 500) if options.wallpaper else ((940, 1020) if options.portrait else (1050, 870))
    artwork = ImageOps.contain(canvas.crop(canvas.getbbox()), tuple(value * SCALE for value in art_bounds), Image.Resampling.LANCZOS)
    if options.wallpaper:
        art_position = (1170 * SCALE, (HEIGHT * SCALE - artwork.height) // 2)
    elif options.portrait:
        art_position = ((WIDTH * SCALE - artwork.width) // 2, 890 * SCALE)
    else:
        art_position = (780 * SCALE, (HEIGHT * SCALE - artwork.height) // 2)
    backdrop.paste(artwork, art_position, artwork)

    if not options.wallpaper:
        canvas = Image.new("RGBA", (520 * SCALE, 500 * SCALE))
        brush = ImageDraw.Draw(canvas)
        path([("M", 120, 95), ("L", 335, 95), ("L", 335, 46),
            ("C", 335, 14, 399, 14, 399, 46), ("L", 399, 95),
            ("L", 430, 95), ("C", 459, 95, 470, 109, 470, 135),
            ("L", 470, 411), ("C", 470, 439, 452, 450, 427, 450),
            ("L", 120, 450), ("C", 94, 450, 80, 436, 80, 409),
            ("L", 80, 135), ("C", 80, 108, 94, 95, 120, 95), ("Z",)],
            "#22211D", YELLOW, 13)
        ellipse((356, 40, 376, 60), BACKGROUND, YELLOW, 5)
        line([(105, 150), (105, 392)], "#55503D", 4)
        for center_x, center_y in ((181, 226), (232, 294), (172, 352)):
            ellipse((center_x - 43, center_y - 43, center_x + 43, center_y + 43), CORAL)
            ellipse((center_x - 29, center_y - 29, center_x + 29, center_y + 29), "#EFB894")
            for angle in (0, 120, 240):
                radians = math.radians(angle)
                seed_x = center_x + math.cos(radians) * 16
                seed_y = center_y + math.sin(radians) * 16
                ellipse((seed_x - 3, seed_y - 5, seed_x + 3, seed_y + 5), CREAM)
        path([("M", 286, 383), ("L", 305, 156),
            ("C", 355, 183, 367, 268, 346, 317),
            ("L", 323, 317), ("L", 319, 383), ("Z",)], CREAM)
        line([(302, 369), (298, 422)], CORAL, 24)
        ellipse((294, 404, 302, 412), CREAM)
        path([("M", 205, 178), ("C", 174, 166, 170, 137, 197, 139),
            ("C", 200, 111, 231, 124, 224, 146),
            ("C", 253, 141, 251, 172, 224, 177), ("Z",)], GREEN)
        path([("M", 390, 379), ("C", 353, 359, 365, 326, 392, 342),
            ("C", 410, 308, 443, 330, 423, 357),
            ("C", 452, 375, 427, 401, 405, 384), ("Z",)], GREEN)
        line([(385, 382), (416, 348)], "#657C5D", 5)
        board = canvas.rotate(14, resample=Image.Resampling.BICUBIC, expand=True)
        board_size = (660, 650) if options.portrait else (650, 640)
        board = ImageOps.contain(board, tuple(value * SCALE for value in board_size), Image.Resampling.LANCZOS)
        board_position = ((WIDTH * SCALE - board.width) // 2, 170 * SCALE) if options.portrait else (85 * SCALE, 195 * SCALE)
        backdrop.paste(board, board_position, board)

    canvas = backdrop
if options.wallpaper:
    dimensions = (3840, 1240)
    destination = OUTPUT / "kitchen-club-library-wallpaper-3840x1240.png"
    size_limit = 10 * 1024 * 1024
elif options.clean_cover or options.portrait:
    dimensions = (1440, 2560) if options.portrait else (2560, 1440)
    orientation = "portrait" if options.portrait else "landscape"
    destination = OUTPUT / f"kitchen-club-no-logo-{orientation}-{dimensions[0]}x{dimensions[1]}.png"
    size_limit = 4 * 1024 * 1024
else:
    dimensions = (2560, 1440)
    destination = OUTPUT / "kitchen-club-store-cover-2560x1440.png"
    size_limit = 4 * 1024 * 1024
canvas.resize(dimensions, Image.Resampling.LANCZOS).save(destination, optimize=True)
with Image.open(destination) as result:
    assert result.format == "PNG" and result.mode == "RGB"
    assert result.size == dimensions
    assert destination.stat().st_size < size_limit
print(f"{destination}: {dimensions[0]}x{dimensions[1]}, RGB PNG, {destination.stat().st_size / 1024:.0f} KB")
