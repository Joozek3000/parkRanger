#!/usr/bin/env python3
"""
S06 terrain v02 (D36): a more natural heightmap on the S06 greybox layout. numpy + Pillow only.
Run from anywhere:  python3 Tools/terrain/s06_terrain_v02.py

In   Art/Source/terrain/S06_ground_v01_513.f32  Blender ground sampled on the Unity heightmap grid
                                                (513 x 513 float32 LE, world Y in m, row = z, col = x,
                                                x, z = -175 + 350 * index / 512).
     Art/Source/terrain/S06_layout_v01.json     world meshes of the objects on the ground (Blender x/y = Unity x/z).
Out  Art/Source/terrain/S06_height_v02.r16      Unity RAW: 513 x 513 uint16 LE, row = z (row 0 = z -175),
                                                0..65535 = Y -12..+30 m (terrain origin y -12, height 42).
     Art/Source/terrain/S06_rock_v02.r8         513 x 513 uint8 rock weight, same grid.
     Design/Spikes/S06/terrain_v02_*.png        previews.

Rules that keep S06Terrain.cs Re-snap happy:
 - Road, Pad_*, Bridge_*, Creek, DeadZone_* are never moved by Re-snap: ground under them unchanged (D = 0).
 - Buildings and the station and town areas are kept at D = 0 too (they sit beside the fixed pads).
 - Everything else moves by the ground change at its pivot, and Re-snap flags vertices whose ground moved
   >0.2 m differently, so D is constant over each object's footprint.
D = new height - Blender height.
"""
import json, math, os, random, sys
import numpy as np
from PIL import Image, ImageDraw

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SRC = os.path.join(ROOT, "Art", "Source", "terrain")
PREV = os.path.join(ROOT, "Design", "Spikes", "S06")
N = 513; SIZE = 350.0; ORIGIN = -175.0; CELL = SIZE / (N - 1)
YMIN, YMAX = -12.0, 30.0
SEED = 6

coords = ORIGIN + CELL * np.arange(N)
X, Z = np.meshgrid(coords, coords)  # [row = z, col = x]

def log(*a): print(*a, flush=True)

def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)

def box_blur(a, r):
    if r < 1: return a
    for axis in (0, 1):
        pad = [(r + 1, r) if ax == axis else (0, 0) for ax in (0, 1)]
        c = np.cumsum(np.pad(a, pad, mode="edge"), axis=axis)
        a = ((c[2 * r + 1:] - c[:-2 * r - 1]) if axis == 0 else (c[:, 2 * r + 1:] - c[:, :-2 * r - 1])) / (2 * r + 1)
    return a

def blur(a, sigma_m, cell=CELL):
    s = sigma_m / cell
    r = int(round((math.sqrt(4 * s * s + 1) - 1) / 2))
    for _ in range(3): a = box_blur(a, r)
    return a

def _edt1(f):
    n = len(f); v = [0] * n; z = [0.0] * (n + 1); d = [0.0] * n
    k = 0; z[0] = -1e20; z[1] = 1e20
    for q in range(1, n):
        fq = f[q] + q * q
        while True:
            vk = v[k]
            s = (fq - (f[vk] + vk * vk)) / (2 * q - 2 * vk)
            if s <= z[k]: k -= 1
            else: break
        k += 1; v[k] = q; z[k] = s; z[k + 1] = 1e20
    k = 0
    for q in range(n):
        while z[k + 1] < q: k += 1
        d[q] = (q - v[k]) ** 2 + f[v[k]]
    return d

def edt(mask):
    """Euclidean distance in metres to the nearest True cell."""
    f = np.where(mask, 0.0, 1e12)
    g = np.array([_edt1(col) for col in f.T.tolist()]).T
    g = np.array([_edt1(row) for row in g.tolist()])
    return np.sqrt(g) * CELL

# ---------- noise ----------
_rng = np.random.default_rng(SEED)
PERM = _rng.permutation(1024); VALS = _rng.random(1024) * 2 - 1

def vnoise(x, z):
    xi = np.floor(x).astype(np.int64); zi = np.floor(z).astype(np.int64)
    fx = x - xi; fz = z - zi
    ux = fx * fx * fx * (fx * (fx * 6 - 15) + 10); uz = fz * fz * fz * (fz * (fz * 6 - 15) + 10)
    h = lambda a, b: VALS[PERM[(PERM[a & 1023] + b) & 1023]]
    a = h(xi, zi) + (h(xi + 1, zi) - h(xi, zi)) * ux
    b = h(xi, zi + 1) + (h(xi + 1, zi + 1) - h(xi, zi + 1)) * ux
    return a + (b - a) * uz

def fbm(x, z, wl, octaves, so, gain=0.5, lac=2.0):
    tot = 0.0; amp = 1.0; norm = 0.0; f = 1.0 / wl
    for o in range(octaves):
        tot = tot + amp * vnoise(x * f + so * 17.31 + o * 31.7, z * f - so * 11.13 + o * 7.9)
        norm += amp; amp *= gain; f *= lac
    return tot / norm

def ridged(x, z, wl, octaves, so):
    tot = 0.0; amp = 1.0; norm = 0.0; f = 1.0 / wl
    for o in range(octaves):
        n = 1 - np.abs(vnoise(x * f + so * 17.31 + o * 31.7, z * f - so * 11.13 + o * 7.9))
        tot = tot + amp * n * n; norm += amp; amp *= 0.5; f *= 2.0
    return tot / norm

def seg_dist(px, pz, pts):
    """Distance to a polyline and normalised position along it (0..1)."""
    best = np.full(px.shape, 1e9); along = np.zeros(px.shape)
    lens = [math.hypot(b[0] - a[0], b[1] - a[1]) for a, b in zip(pts, pts[1:])]
    total = sum(lens); acc = 0.0
    for (a, b), L in zip(zip(pts, pts[1:]), lens):
        ex, ez = (b[0] - a[0]) / L, (b[1] - a[1]) / L
        t = np.clip((px - a[0]) * ex + (pz - a[1]) * ez, 0, L)
        d = np.hypot(px - (a[0] + ex * t), pz - (a[1] + ez * t))
        m = d < best; best[m] = d[m]; along[m] = (acc + t[m]) / total
        acc += L
    return best, along

def smax(a, b, k):
    h = np.clip(0.5 + 0.5 * (a - b) / k, 0, 1)
    return b + (a - b) * h + k * h * (1 - h)

# ---------- hydraulic erosion (droplets) ----------
def erode(H, drops, seed, life=55, inertia=0.05, capf=4.0, mincap=0.01, er=0.3, dep=0.3, evap=0.02, grav=4.0):
    n = H.shape[0]; hm = H.ravel().tolist(); rnd = random.Random(seed)
    for _ in range(drops):
        x = rnd.uniform(1, n - 2); y = rnd.uniform(1, n - 2)
        dx = dy = 0.0; sp = 1.0; w = 1.0; sed = 0.0
        for _ in range(life):
            ix = int(x); iy = int(y); u = x - ix; v = y - iy; k = iy * n + ix
            a = hm[k]; b = hm[k + 1]; c = hm[k + n]; d = hm[k + n + 1]
            gx = (b - a) * (1 - v) + (d - c) * v; gy = (c - a) * (1 - u) + (d - b) * u
            h = a * (1 - u) * (1 - v) + b * u * (1 - v) + c * (1 - u) * v + d * u * v
            dx = dx * inertia - gx * (1 - inertia); dy = dy * inertia - gy * (1 - inertia)
            L = math.hypot(dx, dy)
            if L < 1e-9: break
            dx /= L; dy /= L; x += dx; y += dy
            if x < 1 or y < 1 or x >= n - 2 or y >= n - 2: break
            jx = int(x); jy = int(y); uu = x - jx; vv = y - jy; kk = jy * n + jx
            nh = (hm[kk] * (1 - uu) * (1 - vv) + hm[kk + 1] * uu * (1 - vv) + hm[kk + n] * (1 - uu) * vv + hm[kk + n + 1] * uu * vv)
            dh = nh - h
            cap = max(-dh * sp * w * capf, mincap)
            if sed > cap or dh > 0:
                amt = min(dh, sed) if dh > 0 else (sed - cap) * dep
                sed -= amt
            else:
                amt = -min((cap - sed) * er, -dh)
                sed -= amt
            hm[k] += amt * (1 - u) * (1 - v); hm[k + 1] += amt * u * (1 - v)
            hm[k + n] += amt * (1 - u) * v; hm[k + n + 1] += amt * u * v
            sp = math.sqrt(max(0.0, sp * sp - dh * grav)); w *= (1 - evap)
    return np.array(hm).reshape(n, n)

def thermal(H, angle_deg, iters, cell, rate=0.25):
    t = math.tan(math.radians(angle_deg)) * cell
    H = H.copy()
    for _ in range(iters):
        d = H[:, :-1] - H[:, 1:]; m = np.sign(d) * np.maximum(np.abs(d) - t, 0) * rate
        H[:, :-1] -= m; H[:, 1:] += m
        d = H[:-1] - H[1:]; m = np.sign(d) * np.maximum(np.abs(d) - t, 0) * rate
        H[:-1] -= m; H[1:] += m
    return H

def bilinear(A, x, z):
    fx = (np.asarray(x) - ORIGIN) / CELL; fz = (np.asarray(z) - ORIGIN) / CELL
    ix = np.clip(np.floor(fx).astype(int), 0, N - 2); iz = np.clip(np.floor(fz).astype(int), 0, N - 2)
    u = fx - ix; v = fz - iz
    return (A[iz, ix] * (1 - u) * (1 - v) + A[iz, ix + 1] * u * (1 - v) + A[iz + 1, ix] * (1 - u) * v + A[iz + 1, ix + 1] * u * v)

def slope_deg(H):
    gz, gx = np.gradient(H, CELL)
    return np.degrees(np.arctan(np.hypot(gx, gz)))

# ---------- inputs ----------
orig = np.fromfile(os.path.join(SRC, "S06_ground_v01_513.f32"), dtype="<f4").reshape(N, N).astype(np.float64)
geo = json.load(open(os.path.join(SRC, "S06_layout_v01.json")))

def raster(names, rect=False):
    img = Image.new("L", (N, N), 0); dr = ImageDraw.Draw(img)
    for nme in names:
        v = geo[nme]["v"]
        if rect:
            xs = [p[0] for p in v]; ys = [p[1] for p in v]
            dr.rectangle([((min(xs) - ORIGIN) / CELL, (min(ys) - ORIGIN) / CELL), ((max(xs) - ORIGIN) / CELL, (max(ys) - ORIGIN) / CELL)], fill=255)
            continue
        for t in geo[nme]["t"]:
            dr.polygon([((v[k][0] - ORIGIN) / CELL, (v[k][1] - ORIGIN) / CELL) for k in t], fill=255)
    return np.array(img) > 0

FIXED = ["Road", "Pad_Parking", "Pad_StationYard", "Bridge_Railings", "Creek"]
BUILDINGS = ["RangerStation", "RangerStation_Garage", "RadioMast", "Diner", "DinerSign", "GasStation", "GasStation_Canopy", "Town_House1", "Town_House2"]
TRAILS = ["Trail_Blue_StationToTower", "Trail_Red_StationToCave", "Trail_Yellow_PicnicLoop"]
CONST = ["FireTower", "Cave_Mound", "Cave_Entrance"] + [k for k in geo if k.startswith(("FeedingStation_", "TrashSpot_", "Rock_"))]

log("distances...")
road = raster(["Road"]); creek = raster(["Creek"])
d_road = edt(road); d_creek = edt(creek)
d_pads = edt(raster(["Pad_Parking", "Pad_StationYard"]))
d_bridge = edt(raster(["Bridge_Railings"]))
# DeadZone_Outline: CC moves it from Re-snap's fixed list to the conformed list (like trails), so no constraint here.
d_bld = edt(raster(BUILDINGS + ["Pad_Parking", "Pad_StationYard"])) - 6.0   # 6 m apron around buildings and pads
d_trail = edt(raster(TRAILS))

# ---------- macro landforms ----------
log("landforms...")
wx = X + 22 * fbm(X, Z, 110, 3, 1); wz = Z + 22 * fbm(X, Z, 110, 3, 2)

# Fire tower hill: bigger, steeper on the north-east, gentle on the blue-trail side (south-west).
TX, TZ = 35.0, 15.0
r = np.hypot(wx - TX, wz - TZ); th = np.arctan2(wz - TZ, wx - TX)
hill = 11.0 * np.exp(-((r * (1 + 0.45 * np.cos(th - math.pi / 4))) / 62.0) ** 2)

# Ridge from the hill east to the map edge, north of the cave, with a saddle.
RIDGE = [(TX, TZ), (80, -6), (128, -20), (182, -26)]
d_ridge, s_ridge = seg_dist(wx, wz, RIDGE)
crest = np.interp(s_ridge, [0, 0.12, 0.3, 0.48, 0.7, 1.0], [8.5, 8.0, 6.5, 3.8, 7.5, 10.5])
crest = crest + 1.8 * fbm(X, Z, 40, 3, 3)
ridge = crest * (1 - smoothstep(0, 34, d_ridge + 6 * fbm(X, Z, 30, 2, 4))) ** 1.6

# Edge hills: a rim that hides the map edge.
de = np.minimum.reduce([X - ORIGIN, -ORIGIN - X, Z - ORIGIN, -ORIGIN - Z]) + 14 * fbm(X, Z, 55, 3, 5)
rim_h = np.clip(9 + 6 * fbm(X, Z, 90, 3, 6), 3, 15) * (0.35 + 0.65 * smoothstep(8, 50, d_creek)) * (0.3 + 0.7 * smoothstep(10, 70, d_bld))
rim = rim_h * (1 - smoothstep(0, 62, de)) ** 1.5

# Creek valley: banks rise away from the creek, then blend back.
valley = 3.2 * smoothstep(4, 34, d_creek) * (1 - smoothstep(85, 140, d_creek))

rolling = 2.6 * fbm(wx, wz, 80, 4, 7)
D0 = smax(smax(hill, ridge, 3.0) * (0.3 + 0.7 * smoothstep(4, 48, d_road)), rim, 4.0) + valley + rolling

# ---------- erosion at half resolution ----------
log("erosion...")
H0 = orig + D0
half = blur(H0, 0.7)[::2, ::2]; hc = CELL * 2
er = erode(half / hc, drops=int(sys.argv[1]) if len(sys.argv) > 1 else 26000, seed=SEED) * hc
er = thermal(er, 36, 40, hc)
dE = er - half
dE = blur(dE, 1.4, hc)
# back to full resolution (bilinear)
idx = np.arange(N) / 2.0; i0 = np.clip(np.floor(idx).astype(int), 0, dE.shape[0] - 2); t = idx - i0
dE = dE[i0] * (1 - t)[:, None] + dE[i0 + 1] * t[:, None]
dE = dE[:, i0] * (1 - t)[None, :] + dE[:, i0 + 1] * t[None, :]
log(f"  erosion delta: min {dE.min():.2f} max {dE.max():.2f} m")
H1 = H0 + dE

# ---------- detail and rock outcrops ----------
detail = 0.45 * fbm(X, Z, 15, 3, 8) + 0.15 * fbm(X, Z, 5, 2, 9)
rock_region = np.maximum.reduce([
    1 - smoothstep(18, 50, np.hypot(X - TX, Z - TZ)),                     # hilltop
    (1 - smoothstep(4, 16, d_ridge)) * smoothstep(0.08, 0.2, s_ridge),    # ridge crest
    1 - smoothstep(10, 38, np.hypot(X - 128, Z + 58)),                   # behind the cave
])
outcrop = rock_region * smoothstep(4, 12, d_trail) * np.clip((ridged(X, Z, 24, 3, 10) - 0.52) / 0.28, 0, 1) ** 1.4 * 3.2
H2 = H1 + detail + outcrop
D = H2 - orig

# ---------- protect what Re-snap expects ----------
log("protection...")
Ds = blur(D, 4.5)
wt = 1 - smoothstep(2.0, 10.0, d_trail)
D = D * (1 - wt) + Ds * wt                      # trails: smoother ground

corr = 1 - smoothstep(4.0, 14.0, d_trail)   # trail corridor: wider, gentler banks where trails cross

def fixed_weight(k):
    return np.maximum.reduce([
        1 - smoothstep(1.5, 1.5 + 16 * k, d_road),
        1 - smoothstep(1.0, 1.0 + 13 * k, d_creek),
        1 - smoothstep(1.0, 1.0 + 11 * k, d_pads),
        1 - smoothstep(3.0, 3.0 + 10 * k, d_bridge),
        1 - smoothstep(2.0, 2.0 + 18 * k, d_bld),
    ])
wF = fixed_weight(1.0) * (1 - corr) + fixed_weight(2.4) * corr
D = D * (1 - wF)
hard = np.maximum.reduce([d_road <= 1.5, d_creek <= 1.0, d_pads <= 1.0, d_bridge <= 3.0, d_bld <= 2.0])
D[hard] = 0.0

def relax_slopes(H, angle_deg, iters, region, movable, rate=0.25):
    """Cap slopes between neighbour cells inside `region`. Only `movable` cells change; a pinned cell's
    neighbour takes the whole correction."""
    t = math.tan(math.radians(angle_deg)) * CELL
    H = H.copy(); mv_ = movable.astype(float)
    for axis in (0, 1):
        pass
    for _ in range(iters):
        for axis in (0, 1):
            a = (slice(None), slice(None, -1)) if axis == 1 else (slice(None, -1), slice(None))
            b = (slice(None), slice(1, None)) if axis == 1 else (slice(1, None), slice(None))
            inside = region[a] & region[b]
            d = H[a] - H[b]; ex = np.where(inside, np.sign(d) * np.maximum(np.abs(d) - t, 0), 0)
            ma, mb = mv_[a], mv_[b]; tot = np.maximum(ma + mb, 1e-9)
            H[a] -= ex * rate * 2 * ma / tot * (ma > 0)
            H[b] += ex * rate * 2 * mb / tot * (mb > 0)
    return H

def round_dist(name, grow):
    """Distance to a circle around the object's footprint (round plateaus look less artificial than boxes)."""
    v = np.array(geo[name]["v"])[:, :2]; c = (v.min(0) + v.max(0)) / 2; rad = np.hypot(*(v - c).T).max() + grow
    return np.maximum(np.hypot(X - c[0], Z - c[1]) - rad, 0)

# Constant offset under each snapped object: strongest weight wins where rings overlap.
groups = [["FireTower"], ["Cave_Mound", "Cave_Entrance"]] + [[k] for k in CONST if k not in ("FireTower", "Cave_Mound", "Cave_Entrance")]
def _box(g, grow):
    v = np.concatenate([np.array(geo[n]["v"])[:, :2] for n in g]); return v.min(0) - grow, v.max(0) + grow
merged = True
while merged:  # groups whose footprints touch share one offset
    merged = False
    for a in range(len(groups)):
        for b in range(a + 1, len(groups)):
            (a0, a1), (b0, b1) = _box(groups[a], 1.5), _box(groups[b], 1.5)
            if (a0 < b1).all() and (b0 < a1).all():
                groups[a] += groups.pop(b); merged = True; break
        if merged: break
wmax = np.zeros_like(D); bsum = np.zeros_like(D); dsum = np.zeros_like(D); Dsoft = blur(D, 5.0)
for g in groups:
    dist = np.minimum.reduce([round_dist(n, 0.6) for n in g])
    w = 1 - smoothstep(0, 15, dist)
    p = geo[g[0]]["pivot"]
    Dc = 0.0 if (hard & (dist < 1.0)).any() else float(bilinear(Dsoft, p[0], p[1]))
    b = w / (1.0001 - w)  # inside a footprint (w = 1) this object's offset dominates
    wmax = np.maximum(wmax, w); bsum += b; dsum += b * Dc
dcon = np.where(bsum > 0, dsum / np.maximum(bsum, 1e-12), 0)
D = D * (1 - wmax) + dcon * wmax
D[hard] = 0.0
# Trails: cap the slope in a 10 m corridor; fixed ground and object footprints stay put.
D = relax_slopes(orig + D, 24, 150, d_trail < 10.0, ~hard & (wmax < 0.999)) - orig
H = np.clip(orig + D, YMIN + 0.3, YMAX - 0.3)
D = H - orig

# ---------- checks ----------
log("checks...")
ok = True
for nme in FIXED + BUILDINGS:
    v = np.array(geo[nme]["v"]); m = np.abs(bilinear(D, v[:, 0], v[:, 1])).max()
    if m > 0.05: ok = False; log(f"  FIXED {nme}: ground moved {m:.2f} m")
for g in groups:
    p = geo[g[0]]["pivot"]; dp = float(bilinear(D, p[0], p[1]))
    for nme in g:
        v = np.array(geo[nme]["v"]); m = np.abs(bilinear(D, v[:, 0], v[:, 1]) - dp).max()
        if m > 0.15: ok = False; log(f"  CONST {nme}: uneven by {m:.2f} m")
sl = slope_deg(H); sl0 = slope_deg(orig)
trail_m = d_trail < 1.0
log(f"  height  before {orig.min():.1f}..{orig.max():.1f}  after {H.min():.1f}..{H.max():.1f} m")
log(f"  slope   >30: {100*(sl>30).mean():.1f}%  >40: {100*(sl>40).mean():.1f}%  >45: {100*(sl>45).mean():.2f}%   (before >30: {100*(sl0>30).mean():.1f}%)")
log(f"  trails  slope p95 {np.percentile(sl[trail_m],95):.1f}  max {sl[trail_m].max():.1f} deg")
for nme in TRAILS:
    m = raster([nme]); log(f"    {nme}: p95 {np.percentile(sl[m],95):.1f} max {sl[m].max():.1f}")
log("  tower ground", f"{float(bilinear(H, TX, TZ)):.1f} m (was {float(bilinear(orig, TX, TZ)):.1f})")
log("  constraints OK" if ok else "  constraints FAILED")

# ---------- outputs ----------
rock = np.clip((sl - 30) / 10, 0, 1)
rock = np.maximum(rock, np.clip(outcrop * (D != 0) / 1.2, 0, 1))
rock[hard] = 0
rock = np.clip(blur(rock, 0.8), 0, 1)
((np.clip((H - YMIN) / (YMAX - YMIN), 0, 1) * 65535).round().astype("<u2")).tofile(os.path.join(SRC, "S06_height_v02.r16"))
(rock * 255).round().astype(np.uint8).tofile(os.path.join(SRC, "S06_rock_v02.r8"))
np.save(os.path.join(ROOT, "Art", "Source", "_terrain_work", "v02_H.npy"), H)
np.save(os.path.join(ROOT, "Art", "Source", "_terrain_work", "v02_rock.npy"), rock)
log("written")
