from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "assets" / "janus-icons"
SIZES = (16, 24, 32, 48, 64, 128, 256)

for source in sorted(ASSETS.glob("*.png")):
    if source.name.endswith("-source.png"):
        continue
    image = Image.open(source).convert("RGBA")
    image.save(ASSETS / (source.stem + ".ico"), format="ICO", sizes=[(n, n) for n in SIZES])
    print(f"Creado: {source.stem}.ico")
