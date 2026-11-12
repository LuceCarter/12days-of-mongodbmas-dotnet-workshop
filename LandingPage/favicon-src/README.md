# Favicon source

The site's icon: the MongoDB leaf wearing a Santa hat, on an evergreen tile.

| File | What it is |
|---|---|
| `mongodb_leaf_green.png` | MongoDB's official leaf in Spring Green, **unmodified**. From the MongoDB brand asset library (the 2026 brand). |
| `favicon.svg` | The icon: tile with rounded corners, the leaf as an embedded image, and the hat drawn as vector shapes on top. |
| `favicon-square.svg` | The same, with square corners — for `apple-touch-icon.png`, because iOS applies its own rounded mask. |
| `build-favicons.py` | Renders every size into `../wwwroot/`. |

## Rebuilding

```bash
python3 LandingPage/favicon-src/build-favicons.py
```

Needs macOS `sips` and Google Chrome (or `CHROME=/path/to/chrome`). No Python packages. It writes
`favicon-16.png`, `favicon-32.png`, `favicon.png`, `icon-192.png`, `apple-touch-icon.png` and a
`favicon.ico` containing 16, 32 and 48 pixel images. Commit the results: the build does not run this.

Each size is rendered natively by Chrome rather than shrunk from one large image — a 16px icon
downscaled from 512px goes soft, and at that size the hat's edges are most of the picture.

## Editing the hat

The hat is the `<path>` and `<circle>` shapes in the SVG, drawn in the leaf's own coordinate space
(the leaf is 530 × 1136 units, tip at the top centre). The cone's left edge must pass **above** the
leaf's tip at x≈265, or a sliver of leaf shows through the hat; the first version had exactly that.

`dotnet test src/Content.Tests` checks that `index.html` only links files that exist and that each
icon is really the size its name says.

## A note on the artwork

The leaf is MongoDB's trademarked logo and is used here unmodified, as part of MongoDB-authored
learning content. If this repository ever moves outside that context, check MongoDB's brand
guidelines first.
