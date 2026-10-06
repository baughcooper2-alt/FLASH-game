"""Paints the CSI Barry textures from the layer data written by Tools/Blender/build_csi_barry.py.

    python3 Tools/csi_textures.py <work folder> <reference T-pose image>

Every layer is rasterised in its own UV layout, so each texel knows its rest-pose position and normal; colour
is then computed per texel. The face is the user's Meshy reference warped onto the base face with a
thin-plate spline through matching landmarks (eye corners, brows, nose, mouth, chin, jaw, hairline, ears),
with the render's one-sided lighting evened out. Skin, hair, denim, plaid and shoe colours were sampled from the
same reference; the fabrics are generated so they stay clean at any distance.
"""
import json, os, sys
import numpy as np
from PIL import Image, ImageFilter

WORK, REF = sys.argv[1], sys.argv[2]

def smooth(t): t = np.clip(t, 0, 1); return t * t * (3 - 2 * t)
def mix(a, b, t): t = np.asarray(t)[..., None]; return a * (1 - t) + b * t
def rgb(*c): return np.array(c, np.float32)

# --- Rasterising --------------------------------------------------------------------------------
def raster(name, size):
    d = np.load(os.path.join(WORK, name + ".npz"))
    uv, pos, nrm = d["uv"], d["pos"], d["nrm"]
    part = d["part"] if "part" in d else np.zeros(len(uv), int)
    P = np.zeros((size, size, 3), np.float32); N = np.zeros_like(P); M = np.zeros((size, size), bool); PART = np.zeros((size, size), np.int8)
    px = uv * size - .5
    for t in range(len(px)):
        a, b, c = px[t]
        x0, x1 = int(max(0, np.floor(min(a[0], b[0], c[0])))), int(min(size - 1, np.ceil(max(a[0], b[0], c[0]))))
        y0, y1 = int(max(0, np.floor(min(a[1], b[1], c[1])))), int(min(size - 1, np.ceil(max(a[1], b[1], c[1]))))
        if x1 < x0 or y1 < y0: continue
        xs, ys = np.meshgrid(np.arange(x0, x1 + 1), np.arange(y0, y1 + 1))
        den = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
        if abs(den) < 1e-12: continue
        w0 = ((b[1] - c[1]) * (xs - c[0]) + (c[0] - b[0]) * (ys - c[1])) / den
        w1 = ((c[1] - a[1]) * (xs - c[0]) + (a[0] - c[0]) * (ys - c[1])) / den
        w2 = 1 - w0 - w1
        inside = (w0 >= -.02) & (w1 >= -.02) & (w2 >= -.02)
        if not inside.any(): continue
        w = np.stack([w0, w1, w2], -1)[inside]
        yy, xx = ys[inside], xs[inside]
        P[yy, xx] = w @ pos[t]; N[yy, xx] = w @ nrm[t]; M[yy, xx] = True; PART[yy, xx] = part[t]
    N /= np.maximum(np.linalg.norm(N, axis=-1, keepdims=True), 1e-6)
    raster.part = PART
    return P, N, M

def pad(img, mask, steps=12):
    """Bleeds colour outward from the UV islands so texture filtering never pulls in background."""
    img = np.nan_to_num(img.copy()); img[~mask] = 0; filled = mask.copy()
    for _ in range(steps):
        acc = np.zeros_like(img); cnt = np.zeros(mask.shape, np.float32)
        for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0)):
            f = np.roll(filled, (dy, dx), (0, 1)); acc += np.roll(img, (dy, dx), (0, 1)) * f[..., None]; cnt += f
        grow = (~filled) & (cnt > 0)
        img[grow] = acc[grow] / cnt[grow][:, None]; filled |= grow
    return img

def save(name, img, mask):
    img = pad(img, mask)
    Image.fromarray((np.clip(img[::-1], 0, 1) * 255 + .5).astype(np.uint8)).save(os.path.join(WORK, name + ".png"))

# --- Noise ------------------------------------------------------------------------------------
def hash3(i, j, k): return (np.sin(i * 127.1 + j * 311.7 + k * 74.7) * 43758.5453) % 1.0
def vnoise(p):
    """Smooth 3-D value noise in [0, 1] (p: ...x3)."""
    f = np.floor(p); t = p - f; t = t * t * (3 - 2 * t)
    i, j, k = f[..., 0], f[..., 1], f[..., 2]
    out = 0
    for dx in (0, 1):
        for dy in (0, 1):
            for dz in (0, 1):
                w = (t[..., 0] if dx else 1 - t[..., 0]) * (t[..., 1] if dy else 1 - t[..., 1]) * (t[..., 2] if dz else 1 - t[..., 2])
                out = out + w * hash3(i + dx, j + dy, k + dz)
    return out
def fbm(p, octaves=3):
    s, a, tot = 0, 1, 0
    for o in range(octaves): s = s + a * vnoise(p * 2 ** o); tot += a; a *= .5
    return s / tot

# --- Reference face ---------------------------------------------------------------------------
ref = np.asarray(Image.open(REF).convert("RGB")).astype(np.float32) / 255
# Model face landmarks (x, z in metres, from the base body) and the same points in the reference (pixels).
PAIRS = [
    ((-.049, 1.713), (1234, 117)), ((-.0145, 1.712), (1263, 118)), ((.0145, 1.712), (1290, 117)), ((.049, 1.714), (1320, 115)),
    ((-.029, 1.714), (1249, 117)), ((.029, 1.714), (1306, 116)),
    ((-.030, 1.729), (1249, 102)), ((.030, 1.729), (1307, 102)), ((-.053, 1.726), (1232, 104)), ((.053, 1.726), (1325, 104)),
    ((0, 1.663), (1278, 153)), ((-.013, 1.665), (1263, 150)), ((.013, 1.665), (1290, 150)),
    ((-.031, 1.645), (1256, 170)), ((.031, 1.645), (1305, 170)), ((0, 1.6415), (1280, 169)), ((0, 1.650), (1280, 163)), ((0, 1.634), (1280, 178)),
    ((0, 1.594), (1278, 207)), ((-.050, 1.616), (1240, 190)), ((.050, 1.616), (1318, 190)),
    ((-.086, 1.687), (1224, 135)), ((.086, 1.687), (1336, 135)),
    ((0, 1.772), (1279, 63)), ((-.072, 1.748), (1226, 92)), ((.072, 1.748), (1332, 92)),
    ((-.087, 1.700), (1217, 128)), ((.087, 1.700), (1342, 128)),
    ((-.045, 1.565), (1252, 228)), ((.045, 1.565), (1306, 228)),
]
src = np.array([p[0] for p in PAIRS], np.float64); dst = np.array([p[1] for p in PAIRS], np.float64)
def U(r2): return np.where(r2 > 0, r2 * np.log(r2 + 1e-12) * .5, 0)
n = len(src)
K = U(((src[:, None] - src[None]) ** 2).sum(-1)); Pm = np.hstack([np.ones((n, 1)), src])
A = np.zeros((n + 3, n + 3)); A[:n, :n] = K + np.eye(n) * 1e-9; A[:n, n:] = Pm; A[n:, :n] = Pm.T
coef = np.linalg.solve(A, np.vstack([dst, np.zeros((3, 2))]))
def warp(q):
    """Model (x, z) -> reference pixel (x, y)."""
    shape = q.shape[:-1]; q = q.reshape(-1, 2).astype(np.float64)
    out = U(((q[:, None] - src[None]) ** 2).sum(-1)) @ coef[:n] + np.hstack([np.ones((len(q), 1)), q]) @ coef[n:]
    return out.reshape(*shape, 2)

FACE_BOX = (1170, 0, 1390, 260)
crop = ref[FACE_BOX[1]:FACE_BOX[3], FACE_BOX[0]:FACE_BOX[2]]
lum = crop @ np.array([.3, .59, .11], np.float32)
# Flat-field: remove the render's broad light and shadow (the game lights the face itself), keeping the detail.
# The light estimate only averages skin (not hair, brows or background), so dark hair can't brighten the forehead.
skin_px = ((crop[..., 0] > crop[..., 2] + .06) & (lum > .2)).astype(np.float32)
def gblur(a, r):
    """Separable Gaussian blur (sigma r pixels) with edge clamping."""
    k = np.exp(-.5 * (np.arange(-3 * r, 3 * r + 1) / r) ** 2); k /= k.sum()
    pad = len(k) // 2
    a = np.pad(a, pad, mode="edge")
    a = np.apply_along_axis(lambda m: np.convolve(m, k, mode="valid"), 0, a)
    return np.apply_along_axis(lambda m: np.convolve(m, k, mode="valid"), 1, a).astype(np.float32)
light = gblur(lum * skin_px, 14) / np.maximum(gblur(skin_px, 14), 1e-3)
light = np.where(gblur(skin_px, 14) > .05, light, lum.mean())
face_light = light[70:210, 50:170][skin_px[70:210, 50:170] > 0].mean()
face = crop * np.clip((face_light / np.maximum(light, .05)) ** .9, .6, 1.8)[..., None]
def sample(img, xy, box):
    x = xy[..., 0] - box[0]; y = xy[..., 1] - box[1]
    h, w = img.shape[:2]
    x = np.clip(x, 0, w - 1.001); y = np.clip(y, 0, h - 1.001)
    x0, y0 = np.floor(x).astype(int), np.floor(y).astype(int); fx, fy = (x - x0)[..., None], (y - y0)[..., None]
    return (img[y0, x0] * (1 - fx) * (1 - fy) + img[y0, x0 + 1] * fx * (1 - fy) + img[y0 + 1, x0] * (1 - fx) * fy + img[y0 + 1, x0 + 1] * fx * fy)
# Skin tone: the render's warm key light reads as pink; settle on a natural light tan and shift the face to it
# (keeping its detail), so the face, neck and arms match.
cheeks = np.concatenate([sample(face, warp(np.array([[s * .055, 1.675]])), FACE_BOX) for s in (-1, 1)]).mean(0)
SKIN = rgb(.71, .5, .41)
print("reference cheeks (sRGB)", np.round(cheeks, 3), "-> skin", SKIN)
HAIR = rgb(.17, .13, .12)
info = json.load(open(os.path.join(WORK, "layers.json")))["hair"]
def hairline(x, y, z):
    """Same hairline as the hair mesh (build_csi_barry.py)."""
    t = (y - info["front"]) / (info["back"] - info["front"])
    side = smooth((np.abs(x) - .04) / .035)
    front = 1.772 + .006 * (np.abs(x) / .04) ** 2
    sides = info["ear_top"] + .004 - .03 * smooth((.2 - t) / .12) - .105 * smooth((t - .55) / .4)
    ear = smooth((np.abs(x) - .06) / .015) * np.exp(-((y - info["ear_y"]) / .03) ** 4)
    return front * (1 - side) + sides * side + .006 * ear

# --- Body: skin, face, tee, jeans --------------------------------------------------------------
P, N, M = raster("Body", 2048)
x, y, z = P[..., 0], P[..., 1], P[..., 2]
grain = fbm(P * 140)[..., None]
img = SKIN * (.95 + .1 * grain) * np.ones_like(P)
img = mix(img, img * rgb(1.04, .93, .92), smooth((np.abs(x) - .7) / .1))           # warmer fingers
img = mix(img, img * rgb(1.02, .9, .9), smooth((np.abs(x) - .079) / .01) * (z > 1.65))  # ears
# Face: the warped reference, faded out toward the sides of the head and below the chin.
facing = smooth((-N[..., 1] - .3) / .35)                                             # no smearing at grazing angles
oval = smooth((1 - ((x / .078) ** 2 + ((z - 1.69) / .12) ** 2)) / .3) * smooth((z - 1.588) / .015)
w = facing * oval * (z > 1.5) * (1 - smooth((z - 1.75) / .02))
proj = sample(face, warp(np.stack([x, z], -1)), FACE_BOX)
# Rebuild the face on the skin colour so there is no seam: keep the reference's shading as a luminance ratio
# (highlights soft-clipped so they cannot wash out) and a damped share of its colour variation (lips, brows,
# stubble). Scaling each channel to the skin instead lifted green and blue 1.6x and bleached the highlights.
sel = w > .5
L = proj @ np.array([.3, .59, .11], np.float32)
d = L / L[sel].mean()
d = np.where(d > 1, 1 + .45 * np.tanh((d - 1) / .45), d ** .9)[..., None]
tint = (proj / np.maximum(L, 1e-3)[..., None]) / (proj[sel].mean(0) / L[sel].mean())
proj = np.clip(SKIN * d * tint ** .6, 0, 1)
img = mix(img, proj, w)
# Eyes: clean whites, blue-grey irises with a dark rim, black pupils, centred on each eyeball's front.
eyes = json.load(open(os.path.join(WORK, "layers.json")))["eyes"]
eyeball = raster.part == 1
for c in eyes.values():
    d = np.hypot(x - c[0], z - c[2])
    near = eyeball & (np.abs(x - c[0]) < .016)
    iris = smooth((.0058 - d) / .0008)
    lid = smooth((z - c[2] - .004) / .005)[..., None]                                    # upper-lid shadow
    col = mix(rgb(.8, .78, .76), rgb(.3, .39, .48) * (.75 + .4 * vnoise(P * 4000)[..., None]), iris) * (1 - .35 * lid)
    col = mix(col, rgb(.12, .15, .2), np.exp(-((d - .0056) / .0006) ** 2) * .8)
    col = mix(col, rgb(.02, .02, .025), smooth((.0024 - d) / .0004))
    img[near] = col[near]
# Scalp under the hair, fading out just below the hairline so the hair edge reads soft.
under = smooth((z - hairline(x, y, z) + .008) / .012) * (z > 1.6) * (np.abs(x) < .11)
img = mix(img, HAIR * (.85 + .3 * fbm(P * 600)[..., None]), under * .92)
# White crew-neck tee on the torso and upper arms.
neckline = 1.535 - .022 * smooth(-N[..., 1])
tee = (z > .93) & (z < neckline) & (np.abs(x) < .34)
tee_col = rgb(.9, .9, .88) * (.94 + .1 * fbm(P * 220)[..., None])
img[tee] = tee_col[tee]
img[tee & (z > neckline - .012)] *= .93                                              # ribbed collar
# Jeans: dark indigo denim, faded on the thigh fronts and knees, darker in the creases.
legs = (z <= .93) & (z > .07)
twill = vnoise(np.stack([(x + z) * 900, (y + z) * 900, z * 300], -1))
fade = smooth(-N[..., 1]) * (smooth((z - .45) / .1) * (1 - smooth((z - .88) / .06)) * .55 + .25 * np.exp(-((z - .5) / .05) ** 2))
crease = smooth(N[..., 1]) * np.exp(-((z - .48) / .04) ** 2)                         # behind the knees
denim = rgb(.09, .15, .27) * (.85 + .3 * twill[..., None]) * (.9 + .2 * fbm(P * 40)[..., None])
denim = mix(denim, rgb(.2, .3, .46), fade * .8)
denim *= (1 - .25 * crease)[..., None]
waistband = (z > .895) & legs
denim[waistband] *= .85
img[legs] = denim[legs]
img[z <= .07] = rgb(.06, .06, .07)                                                     # socks, under the shoes
save("Body", img, M)

# --- Shirt: blue-and-red flannel plaid ----------------------------------------------------------
SETT = [(.00, rgb(.38, .15, .21)), (.28, rgb(.06, .03, .19)), (.31, rgb(.86, .87, .95)), (.34, rgb(.15, .11, .40)),
        (.57, rgb(.49, .47, .77)), (.61, rgb(.15, .11, .40)), (.80, rgb(.86, .87, .95)), (.83, rgb(.06, .03, .19)),
        (.86, rgb(.24, .09, .15)), (1.0, None)]
PERIOD = .074
def stripes(t):
    f = (t / PERIOD + .5) % 1.0
    out = np.zeros(t.shape + (3,), np.float32)
    for (a, c), (b, _) in zip(SETT[:-1], SETT[1:]):
        out[(f >= a) & (f < b)] = c
    return out
def plaid(u, v): return .5 * (stripes(u) + stripes(v))
P, N, M = raster("Shirt", 2048)
x, y, z = P[..., 0], P[..., 1], P[..., 2]
wts = np.abs(N) ** 6; wts /= wts.sum(-1, keepdims=True)
col = plaid(x, z) * wts[..., 1:2] + plaid(y, z) * wts[..., 0:1] + plaid(x, y) * wts[..., 2:3]
weave = vnoise(np.stack([(x - z) * 1100, (y + x) * 1100, z * 500], -1))[..., None]
col = col * (.88 + .22 * weave) * (.9 + .18 * fbm(P * 25)[..., None])
save("Shirt", col, M)

# --- Hair: dark ash brown, swept up and back -----------------------------------------------------
P, N, M = raster("Hair", 1024)
x, y, z = P[..., 0], P[..., 1], P[..., 2]
streak = fbm(np.stack([x * 700, y * 45, z * 260], -1), 4)[..., None]
base = HAIR * (.65 + .7 * streak)
sheen = smooth((N[..., 2] - .4) / .5)[..., None] * .25
save("Hair", base * (1 + sheen), M)

# --- Shoes: black canvas low-tops, white rubber soles and toe caps, white laces -------------------
P, N, M = raster("Shoes", 1024)
x, y, z = P[..., 0], P[..., 1], P[..., 2]
col = rgb(.07, .07, .08) * (.8 + .4 * vnoise(P * 900)[..., None]) * np.ones_like(P)
front = np.zeros_like(x); cxs = np.zeros_like(x)
for s in (1, -1):
    sel = (x * s > 0) & M
    if sel.any():
        front[x * s > 0] = y[sel].min(); cxs[x * s > 0] = np.median(x[sel])
sole = z < .004
toe_cap = (y < front + .045) & (z < .035)
laces = (np.abs(x - cxs) < .02) & (y > front + .05) & (y < front + .14) & (N[..., 2] > .35) & (np.sin((y - front) * 260) > -.2)
col[sole | toe_cap] = rgb(.9, .9, .88)
col[sole & (z < -.012)] = rgb(.75, .74, .72)
col[(np.abs(z - .004) < .002)] = rgb(.15, .15, .17)                                     # sole line
col[laces] = rgb(.92, .92, .9)
save("Shoes", col, M)

# --- Watch: brown leather strap, gold case, white dial ------------------------------------------
P, N, M = raster("Watch", 256)
x, y, z = P[..., 0], P[..., 1], P[..., 2]
col = rgb(.32, .2, .12) * np.ones_like(P)
face_y = y[M].min()
dial = y < face_y + .004
r = np.hypot(x - np.median(x[dial & M]), z - np.median(z[dial & M]))
col[dial] = rgb(.78, .62, .36)
col[dial & (r < .0155) & (N[..., 1] < -.7)] = rgb(.9, .9, .86)
save("Watch", col, M)
print("textures written to", WORK)
