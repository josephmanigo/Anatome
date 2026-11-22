"""Tileable instrument maps. RGB stores surface colour, alpha stores smoothness."""
from pathlib import Path

import numpy as np
from PIL import Image

ART = Path(__file__).resolve().parents[1] / "unity/ThoraxLab/Assets/ThoraxLab"
OUT = ART / "Resources/Learning/InstrumentFinishes"
SIZE = 256


def relief(name, height, strength):
    dx = (np.roll(height, 1, axis=1) - np.roll(height, -1, axis=1)) * strength
    dy = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) * strength
    normal = np.stack((dx, dy, np.ones_like(height)), axis=2)
    normal /= np.linalg.norm(normal, axis=2, keepdims=True)
    assert np.allclose(np.linalg.norm(normal, axis=2), 1)
    Image.fromarray(np.uint8(np.rint((normal + 1) * 127.5))).save(OUT / (name + ".png"))


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    rng = np.random.default_rng(3819)
    grit = rng.normal(0, 1, (SIZE, SIZE))
    # Overlapping, irregular pores leave small plateaus between the mould impressions.
    grain = (2 * grit + sum(np.roll(grit, shift, axis) for axis in (0, 1)
                            for shift in (-1, 1))) / np.sqrt(8)
    grain = np.clip(grain, -2.6, 2.6)
    rgb = np.repeat((235 + grain * 5)[..., None], 3, axis=2)
    rgba = np.dstack((rgb, 125 + grain * 16))
    assert rgba.shape == (SIZE, SIZE, 4) and (rgba >= 0).all() and (rgba <= 255).all()
    Image.fromarray(np.uint8(np.rint(rgba))).save(OUT / "Mould grain.png")
    relief("Mould relief", grain, .24)

    for source, name, strength in (("Brushed stainless", "Brushed relief", .018),
                                   ("Woven linen", "Woven relief", .075)):
        with Image.open(ART / "Laboratory" / (source + ".png")) as tile:
            height = np.asarray(tile.resize((SIZE, SIZE), Image.Resampling.LANCZOS),
                                dtype=float)[:, :, 0]
        relief(name, height, strength)


if __name__ == "__main__":
    main()
