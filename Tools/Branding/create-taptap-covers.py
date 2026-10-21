import json
import math
import re
from pathlib import Path
from xml.etree import ElementTree

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "output/branding/taptap-covers"
FONT = ROOT / "Assets/ThirdParty/GoogleFonts/LilitaOne/LilitaOne-Regular.ttf"
BACKGROUND = "#121212"
YELLOW = "#F6CF7C"
SCALE = 3


def scaled_bounds(bounds):
    return tuple(round(value * SCALE) for value in bounds)


def chef_logo():
    image = Image.new("RGBA", (1024 * SCALE, 1024 * SCALE))
    brush = ImageDraw.Draw(image)
    tree = ElementTree.parse(ROOT / "output/branding/chef-hat-logo.svg")
    for element in tree.getroot():
        kind = element.tag.rsplit("}", 1)[-1]
        if kind == "ellipse":
            center_x, center_y, radius_x, radius_y = (
                float(element.get(key)) for key in ("cx", "cy", "rx", "ry")
            )
            brush.ellipse(scaled_bounds((center_x - radius_x, center_y - radius_y,
                center_x + radius_x, center_y + radius_y)), fill=element.get("fill"))
        elif kind == "path":
            tokens = re.findall(r"[MCLZ]|-?\d+(?:\.\d+)?", element.get("d"))
            vertices = []
            cursor = 0
            while cursor < len(tokens):
                operation = tokens[cursor]
                cursor += 1
                count = {"M": 2, "L": 2, "C": 6, "Z": 0}[operation]
                values = [float(value) for value in tokens[cursor:cursor + count]]
                cursor += count
                if operation in ("M", "L"):
                    previous = tuple(values)
                    vertices.append(previous)
                elif operation == "C":
                    start_x, start_y = previous
                    first_x, first_y, second_x, second_y, end_x, end_y = values
                    for step in range(1, 81):
                        fraction = step / 80
                        inverse = 1 - fraction
                        vertices.append((
                            inverse ** 3 * start_x + 3 * inverse ** 2 * fraction * first_x
                            + 3 * inverse * fraction ** 2 * second_x + fraction ** 3 * end_x,
                            inverse ** 3 * start_y + 3 * inverse ** 2 * fraction * first_y
                            + 3 * inverse * fraction ** 2 * second_y + fraction ** 3 * end_y,
                        ))
                    previous = (end_x, end_y)
                else:
                    vertices.append(vertices[0])
            points = [(round(horizontal * SCALE), round(vertical * SCALE))
                for horizontal, vertical in vertices]
            if element.get("fill") != "none":
                brush.polygon(points, fill=element.get("fill"))
            if element.get("stroke", "none") != "none":
                width = float(element.get("stroke-width")) * SCALE
                brush.line(points, fill=element.get("stroke"), width=round(width), joint="curve")
                for horizontal, vertical in (points[0], points[-1]):
                    radius = width / 2
                    brush.ellipse((horizontal - radius, vertical - radius,
                        horizontal + radius, vertical + radius), fill=element.get("stroke"))
    return image.crop(image.getbbox())


def title(word, width, color, angle, tracking):
    font = ImageFont.truetype(str(FONT), 300 * SCALE)
    length = sum(font.getlength(character) for character in word) + tracking * SCALE * (len(word) - 1)
    layer = Image.new("RGBA", (math.ceil(length) + 80 * SCALE, 440 * SCALE))
    brush = ImageDraw.Draw(layer)
    cursor = 40 * SCALE
    for character in word:
        brush.text((round(cursor), 40 * SCALE), character, font=font, fill=color, anchor="lt")
        cursor += font.getlength(character) + tracking * SCALE
    layer = layer.crop(layer.getbbox()).rotate(angle, Image.Resampling.BICUBIC, expand=True)
    return layer.resize((round(width * SCALE), round(layer.height * width * SCALE / layer.width)),
        Image.Resampling.LANCZOS)


def paste(image, layer, position):
    image.alpha_composite(layer, (round(position[0] * SCALE), round(position[1] * SCALE)))


def paperclip(height):
    image = Image.open(ROOT / "Assets/_Game/Art/UI/BreadPaperclip.png").convert("RGBA")
    eyes = Image.open(ROOT / "Assets/_Game/Art/UI/BreadPaperclipEyes.png").convert("RGBA")
    image.alpha_composite(eyes, (128, 170))
    return image.resize((round(height * image.width / image.height * SCALE), round(height * SCALE)),
        Image.Resampling.LANCZOS)


def card(width, height, logo, portrait):
    padding = 100
    layer = Image.new("RGBA", ((width + padding * 2) * SCALE, (height + padding * 2) * SCALE))
    brush = ImageDraw.Draw(layer)
    brush.rounded_rectangle(scaled_bounds((padding, padding, width + padding, height + padding)),
        radius=42 * SCALE, fill="#FFFFFF")
    brush.rounded_rectangle(scaled_bounds((padding - 12, padding - 27,
        padding + width * 0.51, padding + 42)), radius=26 * SCALE, fill=YELLOW)
    logo_width = width * (0.73 if portrait else 0.84)
    logo_height = logo_width * logo.height / logo.width
    symbol = logo.resize((round(logo_width * SCALE), round(logo_height * SCALE)), Image.Resampling.LANCZOS)
    paste(layer, symbol, (padding + (width - logo_width) / 2,
        padding + (height - logo_height) / 2 + 20))
    clip_height = 210 if portrait else 163
    clip = paperclip(clip_height)
    paste(layer, clip, (padding + width - clip.width / SCALE - 38, padding - clip_height * 0.57))
    return layer.rotate(-2, Image.Resampling.BICUBIC, expand=True)


def cover(width, height, logo, portrait):
    image = Image.new("RGBA", (width * SCALE, height * SCALE), BACKGROUND)
    brush = ImageDraw.Draw(image)
    spacing = 32 if portrait else 27
    for vertical in range(16, height, spacing):
        for horizontal in range(16, width, spacing):
            brush.ellipse(scaled_bounds((horizontal - 1, vertical - 1,
                horizontal + 1, vertical + 1)), fill="#2B2B2B")
    if portrait:
        first = title("KITCHEN", 990, "#FFFFFF", 3, 7)
        second = title("CLUB", 815, YELLOW, -2, 16)
        paste(image, first, ((width - first.width / SCALE) / 2, 210))
        paste(image, second, ((width - second.width / SCALE) / 2, 210 + first.height / SCALE + 56))
        paper = card(880, 700, logo, True)
        paste(image, paper, ((width - paper.width / SCALE) / 2, 825))
    else:
        first = title("KITCHEN", 1020, "#FFFFFF", 3, 7)
        second = title("CLUB", 815, YELLOW, -2, 16)
        title_height = first.height / SCALE + 55 + second.height / SCALE
        top = (height - title_height) / 2
        paste(image, first, (90, top))
        paste(image, second, (108, top + first.height / SCALE + 55))
        paper = card(500, 495, logo, False)
        paste(image, paper, (1105, 80))
    return image.convert("RGB").resize((width, height), Image.Resampling.LANCZOS)


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    logo = chef_logo()
    exports = []
    for portrait, width, height, minimum_width, minimum_height in (
        (False, 1840, 860, 460, 215),
        (True, 1200, 1800, 600, 900),
    ):
        image = cover(width, height, logo, portrait)
        orientation = "portrait" if portrait else "landscape"
        for export_width, export_height in ((width, height), (minimum_width, minimum_height)):
            exported = image.resize((export_width, export_height), Image.Resampling.LANCZOS)
            filename = f"kitchen-club-taptap-{orientation}-{export_width}x{export_height}.png"
            destination = OUTPUT / filename
            exported.save(destination, optimize=True)
            with Image.open(destination) as verified:
                verified.load()
                assert verified.format == "PNG" and verified.mode == "RGB"
                assert verified.size == (export_width, export_height)
            exports.append({"file": filename, "width": export_width, "height": export_height,
                "format": "PNG", "mode": "RGB", "bytes": destination.stat().st_size,
                "visible_text": "KITCHEN CLUB"})
    (OUTPUT / "export-manifest.json").write_text(json.dumps(exports, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(exports, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
