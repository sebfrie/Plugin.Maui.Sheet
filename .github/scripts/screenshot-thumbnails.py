"""Writes screenshots/thumbs/<name>.png for every screenshots/<name>.png.

The README shows the thumbnails with plain markdown (nuget.org does not render HTML <img> tags, so sizes
cannot be set there) and links each one to its full-size screenshot. Phone screenshots become 270 px wide.
Windows screenshots are cropped to the middle of the window, where the sheet is centred, and become 480 px
wide, so the sheet stays legible.
"""
import pathlib
from PIL import Image

source = pathlib.Path("screenshots")
target = source / "thumbs"
target.mkdir(exist_ok=True)
for png in sorted(source.glob("*.png")):
    with Image.open(png) as image:
        image = image.convert("RGB")
        if image.width > image.height:
            crop_width, crop_height = min(640, image.width), min(400, image.height)
            left, top = (image.width - crop_width) // 2, (image.height - crop_height) // 2
            image, width = image.crop((left, top, left + crop_width, top + crop_height)), 480
        else:
            width = 270
        height = round(image.height * width / image.width)
        image.resize((width, height), Image.LANCZOS).save(target / png.name, optimize=True)
    print(f"{png.name} -> thumbs/{png.name} ({width}x{height})")
