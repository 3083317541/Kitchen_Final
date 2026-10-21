from pathlib import Path
from xml.sax.saxutils import quoteattr

from PIL import Image, ImageDraw


SIZE = 1024
SCALE = 4
BACKGROUND = "#F4BD50"
INK = "#543C32"
CREAM = "#FFF7DE"
CORAL = "#EC765D"
OUTPUT = Path(__file__).resolve().parents[2] / "output" / "branding"
canvas = Image.new("RGB", (SIZE * SCALE, SIZE * SCALE), BACKGROUND)
brush = ImageDraw.Draw(canvas)
elements = [f'<rect width="1024" height="1024" fill="{BACKGROUND}"/>']


def attributes(values):
    return " ".join(f"{key}={quoteattr(str(value))}" for key, value in values.items())


def shape(commands, fill, stroke=None, width=0):
    points = []
    previous = None
    origin = None
    for command in commands:
        operation, *values = command
        if operation == "M":
            previous = tuple(values)
            origin = previous
            points.append(previous)
        elif operation == "L":
            previous = tuple(values)
            points.append(previous)
        elif operation == "C":
            start_x, start_y = previous
            control_x, control_y, second_x, second_y, end_x, end_y = values
            for step in range(1, 81):
                fraction = step / 80
                inverse = 1 - fraction
                points.append((
                    inverse ** 3 * start_x + 3 * inverse ** 2 * fraction * control_x
                    + 3 * inverse * fraction ** 2 * second_x + fraction ** 3 * end_x,
                    inverse ** 3 * start_y + 3 * inverse ** 2 * fraction * control_y
                    + 3 * inverse * fraction ** 2 * second_y + fraction ** 3 * end_y,
                ))
            previous = (end_x, end_y)
        elif operation == "Z":
            points.append(origin)
    scaled = [(round(horizontal * SCALE), round(vertical * SCALE)) for horizontal, vertical in points]
    if fill != "none":
        brush.polygon(scaled, fill=fill)
    if stroke:
        brush.line(scaled, fill=stroke, width=width * SCALE, joint="curve")
        radius = width * SCALE / 2
        for horizontal, vertical in (scaled[0], scaled[-1]):
            brush.ellipse((horizontal - radius, vertical - radius, horizontal + radius, vertical + radius), fill=stroke)
    path = " ".join(command[0] + " ".join(str(value) for value in command[1:]) for command in commands)
    elements.append("<path " + attributes({"d": path, "fill": fill, "stroke": stroke or "none",
        "stroke-width": width, "stroke-linecap": "round", "stroke-linejoin": "round"}) + "/>")


def ellipse(bounds, fill):
    left, top, right, bottom = bounds
    brush.ellipse(tuple(round(value * SCALE) for value in bounds), fill=fill)
    elements.append("<ellipse " + attributes({"cx": (left + right) / 2, "cy": (top + bottom) / 2,
        "rx": (right - left) / 2, "ry": (bottom - top) / 2, "fill": fill}) + "/>")


def rounded_rectangle(bounds, radius, fill, stroke=None, width=0):
    left, top, right, bottom = bounds
    shape([
        ("M", left + radius, top), ("L", right - radius, top),
        ("C", right, top, right, top, right, top + radius), ("L", right, bottom - radius),
        ("C", right, bottom, right, bottom, right - radius, bottom), ("L", left + radius, bottom),
        ("C", left, bottom, left, bottom, left, bottom - radius), ("L", left, top + radius),
        ("C", left, top, left, top, left + radius, top), ("Z",),
    ], fill, stroke, width)


hat = [
    ("M", 328, 520),
    ("C", 240, 536, 186, 474, 202, 401),
    ("C", 214, 338, 268, 306, 335, 324),
    ("C", 341, 246, 407, 208, 477, 230),
    ("C", 533, 186, 618, 218, 642, 291),
    ("C", 709, 267, 782, 304, 804, 369),
    ("C", 835, 452, 785, 520, 696, 522),
    ("L", 680, 704),
    ("C", 677, 734, 655, 750, 624, 750),
    ("L", 400, 750),
    ("C", 367, 750, 347, 734, 344, 704),
    ("Z",),
]
shadow = [tuple([command[0]] + [value + (20 if index % 2 == 0 else 24)
    for index, value in enumerate(command[1:])]) for command in hat]
shape(shadow, "#D99A3F")
rounded_rectangle((318, 710, 746, 804), 30, "#D99A3F")

shape(hat, CREAM, INK, 20)
shape([("M", 644, 522), ("C", 668, 519, 682, 519, 696, 522), ("L", 680, 704),
    ("C", 677, 734, 655, 750, 624, 750), ("L", 590, 750),
    ("C", 626, 716, 632, 614, 644, 522), ("Z",)], "#F4E5BB")
shape([("M", 328, 520), ("L", 344, 704), ("C", 347, 734, 367, 750, 400, 750),
    ("L", 624, 750), ("C", 655, 750, 677, 734, 680, 704), ("L", 696, 522)], "none", INK, 20)

shape([("M", 271, 411), ("C", 273, 384, 294, 371, 316, 376)], "none", "#E8D6AC", 13)
shape([("M", 399, 317), ("C", 408, 291, 432, 280, 456, 286)], "none", "#E8D6AC", 13)
shape([("M", 638, 359), ("C", 665, 339, 700, 344, 717, 365)], "none", "#E8D6AC", 13)

ellipse((396, 611, 447, 637), "#F1AC87")
ellipse((577, 611, 628, 637), "#F1AC87")
rounded_rectangle((433, 571, 455, 610), 11, INK)
rounded_rectangle((569, 571, 591, 610), 11, INK)
shape([("M", 490, 620), ("C", 500, 638, 524, 638, 534, 620)], "none", INK, 13)

rounded_rectangle((298, 689, 726, 783), 28, CORAL, INK, 20)
shape([("M", 333, 714), ("L", 454, 714)], "none", "#FFB39A", 10)
rounded_rectangle((600, 717, 663, 753), 13, CREAM)

shape([("M", 839, 233), ("C", 843, 258, 850, 265, 875, 269),
    ("C", 850, 273, 843, 280, 839, 305),
    ("C", 835, 280, 828, 273, 803, 269),
    ("C", 828, 265, 835, 258, 839, 233), ("Z",)], CREAM)
ellipse((177, 643, 203, 669), CORAL)

OUTPUT.mkdir(parents=True, exist_ok=True)
image = canvas.resize((SIZE, SIZE), Image.Resampling.LANCZOS)
image.save(OUTPUT / "chef-hat-logo-1024.png", optimize=True)
image.resize((512, 512), Image.Resampling.LANCZOS).save(OUTPUT / "chef-hat-logo-512.png", optimize=True)
svg = '<svg xmlns="http://www.w3.org/2000/svg" width="1024" height="1024" viewBox="0 0 1024 1024">'
(OUTPUT / "chef-hat-logo.svg").write_text(svg + "\n" + "\n".join(elements) + "\n</svg>\n", encoding="utf-8")

for filename in ("chef-hat-logo-1024.png", "chef-hat-logo-512.png"):
    with Image.open(OUTPUT / filename) as exported:
        assert exported.format == "PNG" and exported.mode == "RGB"
        assert exported.width == exported.height and exported.width >= 512
        assert all(exported.getpixel(point) == (244, 189, 80) for point in (
            (0, 0), (exported.width - 1, 0), (0, exported.height - 1),
            (exported.width - 1, exported.height - 1),
        ))
        print(f"{OUTPUT / filename}: {exported.size}, {exported.mode}, opaque square")
