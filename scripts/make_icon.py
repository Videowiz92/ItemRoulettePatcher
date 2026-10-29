"""Generates thunderstore/icon.png (256x256) - a roulette wheel, same design as the in-game artifact icon.

Pure standard library (no Pillow). Run from the repo root:  python scripts/make_icon.py
"""
import math
import os
import struct
import zlib

SIZE = 256
POCKETS = 12
SS = 3  # supersampling per axis for smooth edges

BACKGROUND = (33, 26, 15)
RIM = (255, 209, 71)
RED = (199, 31, 31)
DARK = (18, 18, 18)


def sample(x, y):
    half = SIZE / 2
    dx = (x - half) / half
    dy = (y - half) / half
    r = math.hypot(dx, dy)
    if r < 0.15:
        return DARK
    if r < 0.30:
        return RIM
    if r < 0.40:
        return DARK
    if r < 0.82:
        angle = math.atan2(dy, dx) + math.pi
        pocket = int(angle / (2 * math.pi) * POCKETS) % POCKETS
        return RED if pocket % 2 == 0 else DARK
    if r < 0.97:
        return RIM
    return BACKGROUND


def pixel(px, py):
    total = [0, 0, 0]
    for sy in range(SS):
        for sx in range(SS):
            c = sample(px + (sx + 0.5) / SS, py + (sy + 0.5) / SS)
            for i in range(3):
                total[i] += c[i]
    n = SS * SS
    return bytes(round(v / n) for v in total)


def chunk(tag, data):
    body = tag + data
    return struct.pack(">I", len(data)) + body + struct.pack(">I", zlib.crc32(body) & 0xFFFFFFFF)


def main():
    rows = []
    for py in range(SIZE):
        rows.append(b"\x00" + b"".join(pixel(px, py) for px in range(SIZE)))
    png = (
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", struct.pack(">IIBBBBB", SIZE, SIZE, 8, 2, 0, 0, 0))
        + chunk(b"IDAT", zlib.compress(b"".join(rows), 9))
        + chunk(b"IEND", b"")
    )
    out = os.path.join(os.path.dirname(__file__), "..", "thunderstore", "icon.png")
    os.makedirs(os.path.dirname(out), exist_ok=True)
    with open(out, "wb") as f:
        f.write(png)
    print("wrote", os.path.abspath(out), len(png), "bytes")


if __name__ == "__main__":
    main()
