from PIL import Image, ImageDraw
import math, os

OUT = "assets/hora.ico"
BG = (15, 18, 32, 255)
RING = (38, 43, 69, 255)
SEGS = [(124, 140, 248, 255), (61, 220, 132, 255), (255, 176, 32, 255)]
WHITE = (255, 255, 255, 255)

def draw(size):
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    c = size / 2
    r = size * 0.44
    d.ellipse([c - r, c - r, c + r, c + r], fill=BG, outline=RING, width=max(2, size // 42))
    ro, ri = r * 0.86, r * 0.57
    arcs = [(270, 30), (30, 150), (150, 270)]
    for (a0, a1), col in zip(arcs, SEGS):
        d.pieslice([c - ro, c - ro, c + ro, c + ro], start=a0, end=a1, fill=col)
        d.pieslice([c - ri, c - ri, c + ri, c + ri], start=a0, end=a1, fill=BG)
    hr = r * 0.30
    d.ellipse([c - hr, c - hr, c + hr, c + hr], fill=BG)
    w = max(2, size // 26)
    d.line([c, c, c, c - hr * 0.75], fill=WHITE, width=w)
    d.line([c, c, c + hr * 0.60, c + hr * 0.35], fill=WHITE, width=w)
    return img

os.makedirs("assets", exist_ok=True)
base = draw(256)
base.save(OUT, sizes=[(16, 16), (32, 32), (48, 48), (256, 256)])
print("wrote", OUT, os.path.getsize(OUT))
