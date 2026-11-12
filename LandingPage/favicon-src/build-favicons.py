#!/usr/bin/env python3
"""
Builds the landing page's icon set from favicon.svg / favicon-square.svg.

    python3 LandingPage/favicon-src/build-favicons.py

Needs macOS `sips` and Google Chrome (or set CHROME=...). No Python packages.

Why not just resize one big PNG: a 16px icon downscaled from 512px goes soft. Chrome rasterises
the SVG natively at each size, which keeps the hat's edges crisp. Chrome's headless window can't
go below ~500px, so each icon is drawn in the middle of a 512px transparent page and cropped out.
"""
import os, pathlib, shutil, struct, subprocess, sys, tempfile

HERE = pathlib.Path(__file__).resolve().parent
OUT = HERE.parent / "wwwroot"
CHROME = os.environ.get("CHROME", "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome")

# (file, pixel size, which artwork)   rounded = tile with rounded corners, square = full bleed
TARGETS = [
    ("favicon-16.png", 16, "favicon.svg"),
    ("favicon-32.png", 32, "favicon.svg"),
    ("favicon-48.png", 48, "favicon.svg"),
    ("favicon.png",    64, "favicon.svg"),            # the file index.html already referenced
    ("icon-192.png",  192, "favicon.svg"),            # was the Blazor template's icon; now ours
    ("apple-touch-icon.png", 180, "favicon-square.svg"),  # iOS applies its own rounded mask
]

def render(svg: pathlib.Path, size: int, dest: pathlib.Path, work: pathlib.Path) -> None:
    page = work / f"page-{svg.stem}-{size}.html"
    # The icon goes in the CENTRE of the 512px page. sips can only crop from the centre (its
    # --cropOffset is ignored here, which cost an afternoon's worth of confusion), so a
    # centred icon is the one a centre crop returns exactly. Every size is even, so (512-n)/2
    # is a whole number of pixels and nothing is resampled.
    inset = (512 - size) // 2
    page.write_text(
        '<!doctype html><meta charset="utf-8"><style>html,body{margin:0;background:transparent}'
        f'img{{position:absolute;left:{inset}px;top:{inset}px;width:{size}px;height:{size}px}}'
        f'</style><img src="{svg.as_uri()}">'
    )
    shot = work / f"shot-{svg.stem}-{size}.png"
    subprocess.run(
        [CHROME, "--headless=new", "--disable-gpu", "--hide-scrollbars",
         "--default-background-color=00000000", "--window-size=512,512",
         f"--screenshot={shot}", page.as_uri()],
        check=True, capture_output=True,
    )
    if size < 512:
        subprocess.run(["sips", "--cropToHeightWidth", str(size), str(size), str(shot), "--out", str(dest)],
                       check=True, capture_output=True)
    else:
        shutil.copy(shot, dest)

def make_ico(pngs: list[pathlib.Path], dest: pathlib.Path) -> None:
    """A .ico is a small header plus the images; modern ICOs can hold PNGs directly."""
    images = [(int(p.stem.split("-")[1]), p.read_bytes()) for p in pngs]
    header = struct.pack("<HHH", 0, 1, len(images))
    offset = 6 + 16 * len(images)
    directory, data = b"", b""
    for size, png in images:
        directory += struct.pack("<BBBBHHII", size % 256, size % 256, 0, 0, 1, 32, len(png), offset + len(data))
        data += png
    dest.write_bytes(header + directory + data)

# Rendered only to go inside favicon.ico; nothing links to them, so they aren't shipped.
ICO_ONLY = {"favicon-48.png"}

def main() -> int:
    if not pathlib.Path(CHROME).exists():
        sys.exit(f"Chrome not found at {CHROME}. Set CHROME=/path/to/chrome.")
    with tempfile.TemporaryDirectory() as tmp:
        work = pathlib.Path(tmp)
        where = lambda name: (work if name in ICO_ONLY else OUT) / name
        for name, size, art in TARGETS:
            render(HERE / art, size, where(name), work)
            print(f"  {name:22} {size:>3}px  ({art})" + ("  [ico only]" if name in ICO_ONLY else ""))
        make_ico([where("favicon-16.png"), where("favicon-32.png"), where("favicon-48.png")], OUT / "favicon.ico")
        print("  favicon.ico            16+32+48")
    return 0

if __name__ == "__main__":
    sys.exit(main())
