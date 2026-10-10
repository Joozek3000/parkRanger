#!/usr/bin/env python3
"""Previews for S06 terrain v02: shaded top-down (before / after) and eye-level views (terrain only, no trees).
Run after s06_terrain_v02.py.  Writes Design/Spikes/S06/terrain_v02_topdown.png and terrain_v02_views.png."""
import json, math, os
import numpy as np
from PIL import Image, ImageDraw

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SRC = os.path.join(ROOT, "Art", "Source", "terrain"); OUT = os.path.join(ROOT, "Design", "Spikes", "S06")
N = 513; CELL = 350 / 512; ORIGIN = -175.0
orig = np.fromfile(os.path.join(SRC, "S06_ground_v01_513.f32"), "<f4").reshape(N, N).astype(float)
new = np.fromfile(os.path.join(SRC, "S06_height_v02.r16"), "<u2").reshape(N, N) / 65535 * 42 - 12
rock = np.fromfile(os.path.join(SRC, "S06_rock_v02.r8"), np.uint8).reshape(N, N) / 255
geo = json.load(open(os.path.join(SRC, "S06_layout_v01.json")))

def mask(names):
    img = Image.new("L", (N, N), 0); dr = ImageDraw.Draw(img)
    for n in names:
        v = geo[n]["v"]
        for t in geo[n]["t"]: dr.polygon([((v[k][0] - ORIGIN) / CELL, (v[k][1] - ORIGIN) / CELL) for k in t], fill=255)
    return np.array(img) > 0
road = mask(["Road", "Pad_Parking", "Pad_StationYard"]); creek = mask(["Creek"])
trails = mask(["Trail_Blue_StationToTower", "Trail_Red_StationToCave", "Trail_Yellow_PicnicLoop"])
blds = mask(["RangerStation", "RangerStation_Garage", "Diner", "GasStation", "Town_House1", "Town_House2", "Cave_Mound", "FireTower", "Bridge_Railings"])

def shade(H):
    gz, gx = np.gradient(H, CELL)
    az, alt = math.radians(315), math.radians(42)
    lx, lz, ly = math.cos(alt) * math.sin(az), math.cos(alt) * math.cos(az), math.sin(alt)
    n = np.dstack([-gx, -gz, np.ones_like(H)]); n /= np.linalg.norm(n, axis=2, keepdims=True)
    return np.clip(n[..., 0] * lx + n[..., 1] * lz + n[..., 2] * ly, 0, 1)

def colours(H, rk):
    t = np.clip((H + 10) / 38, 0, 1)[..., None]
    c = (1 - t) * np.array([62, 92, 52]) + t * np.array([118, 128, 78])
    c = c * (1 - rk[..., None]) + np.array([128, 124, 116]) * rk[..., None]
    c[creek] = [70, 110, 150]; c[road] = [120, 120, 118]; c[trails] = [140, 110, 80]; c[blds] = [170, 150, 130]
    return c

def topdown(H, rk, title):
    s = shade(H)
    img = colours(H, rk) * (0.35 + 0.85 * s[..., None])
    for lvl in np.arange(-10, 30, 2.0):  # contour lines every 2 m
        band = (np.floor(H / 2.0) != np.floor(np.roll(H, 1, 0) / 2.0)) | (np.floor(H / 2.0) != np.floor(np.roll(H, 1, 1) / 2.0))
        img[band] *= 0.8; break
    im = Image.fromarray(np.clip(img, 0, 255).astype(np.uint8)[::-1]).resize((900, 900), Image.LANCZOS)
    d = ImageDraw.Draw(im); d.rectangle([0, 0, 900, 22], fill=(20, 20, 20)); d.text((8, 5), title, fill=(255, 255, 255))
    for name, (x, z) in {"STATION": (-125, -138), "DINER": (125, 160), "TOWER": (35, 15), "CAVE": (110, -60), "BRIDGE": (-89, -73), "FEED 3": (-150, 95)}.items():
        px, py = (x - ORIGIN) / 350 * 900, 900 - (z - ORIGIN) / 350 * 900
        d.text((px + 6, py - 6), name, fill=(255, 255, 255))
    return im

def eye(H, rk, cam, yaw, fov=75, W=800, Hh=420, above=1.7, horizon=0.5, far=420):
    cx, cz = cam
    ix, iz = int((cx - ORIGIN) / CELL), int((cz - ORIGIN) / CELL)
    cy = H[iz, ix] + above
    col = colours(H, rk) * (0.35 + 0.85 * shade(H)[..., None])
    img = np.zeros((Hh, W, 3)); sky = np.array([170, 190, 205])
    img[:] = sky; ybuf = np.full(W, Hh)
    fx, fz = math.sin(math.radians(yaw)), math.cos(math.radians(yaw)); rx, rz = fz, -fx
    focal = (W / 2) / math.tan(math.radians(fov / 2)); hz = Hh * horizon
    rows = np.arange(Hh)[:, None]; u = (np.arange(W) - W / 2) / focal
    z = 0.6
    while z < far:
        px = cx + fx * z + rx * z * u; pz = cz + fz * z + rz * z * u
        gi = (px - ORIGIN) / CELL; gj = (pz - ORIGIN) / CELL
        ok = (gi >= 0) & (gi < N - 1) & (gj >= 0) & (gj < N - 1)
        gi = np.clip(gi, 0, N - 1).astype(int); gj = np.clip(gj, 0, N - 1).astype(int)
        h = np.where(ok, H[gj, gi], -60)
        y = np.clip(hz + (cy - h) / z * focal, 0, Hh).astype(int)
        draw = (rows >= y[None, :]) & (rows < ybuf[None, :]) & ok[None, :]
        if draw.any():
            fog = min(1.0, (z / far) ** 1.4)
            c = col[gj, gi] * (1 - fog) + sky * fog
            img[draw] = np.broadcast_to(c[None], (Hh, W, 3))[draw]
            ybuf = np.where(ok, np.minimum(ybuf, y), ybuf)
        z += 0.15 + z * 0.006
    return Image.fromarray(np.clip(img, 0, 255).astype(np.uint8))

a = topdown(orig, np.zeros_like(rock), "BEFORE  (Blender greybox ground, -10..15 m)")
b = topdown(new, rock, "AFTER  v02  (-10..27 m, 2 m contours, grey = rock)")
td = Image.new("RGB", (1810, 900), (20, 20, 20)); td.paste(a, (0, 0)); td.paste(b, (910, 0))
td.save(os.path.join(OUT, "terrain_v02_topdown.png"))

views = [("Road by the bridge, looking NE to the tower hill", (-80, -55), 40, 1.7, 0.5),
         ("Diner parking, looking SW over the hill and ridge", (100, 150), 215, 1.7, 0.5),
         ("Red trail, looking E toward the cave and ridge", (20, -100), 75, 1.7, 0.5),
         ("Drone, SW corner at +40 m, looking NE", (-165, -165), 45, 40, 0.28)]
sheet = Image.new("RGB", (1610, 30 + len(views) * 450), (20, 20, 20)); d = ImageDraw.Draw(sheet)
d.text((10, 8), "Terrain only (no trees). Left: before. Right: after v02.", fill=(255, 255, 255))
for k, (title, cam, yaw, above, hz) in enumerate(views):
    y0 = 30 + k * 450
    d.text((10, y0 + 6), title, fill=(255, 255, 255))
    sheet.paste(eye(orig, np.zeros_like(rock), cam, yaw, above=above, horizon=hz), (0, y0 + 24))
    sheet.paste(eye(new, rock, cam, yaw, above=above, horizon=hz), (810, y0 + 24))
sheet.save(os.path.join(OUT, "terrain_v02_views.png"))
print("previews written")
