import hashlib
import json
import shutil
import zipfile
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[2]
EXPORTS = ROOT / "output" / "branding"
ARCHIVE = ROOT / "Marketing" / "Release-2026-10-21"
GROUPS = {
    "Icons": ["chef-hat-logo-1024.png", "chef-hat-logo-512.png"],
    "Sources": ["chef-hat-logo.svg"],
    "Promotion": [
        "kitchen-club-store-cover-2560x1440.png",
        "kitchen-club-no-logo-landscape-2560x1440.png",
        "kitchen-club-no-logo-portrait-1440x2560.png",
    ],
    "Wallpaper": ["kitchen-club-library-wallpaper-3840x1240.png"],
}

for folder, filenames in GROUPS.items():
    destination = ARCHIVE / folder
    destination.mkdir(parents=True, exist_ok=True)
    for filename in filenames:
        shutil.copy2(EXPORTS / filename, destination / filename)
shutil.copytree(EXPORTS / "taptap-covers", ARCHIVE / "TapTapCovers", dirs_exist_ok=True)

entries = []
for filepath in sorted(ARCHIVE.rglob("*")):
    if not filepath.is_file() or filepath.name == "manifest.json":
        continue
    contents = filepath.read_bytes()
    entry = {
        "file": filepath.relative_to(ARCHIVE).as_posix(),
        "bytes": len(contents),
        "sha256": hashlib.sha256(contents).hexdigest(),
    }
    if filepath.suffix.lower() == ".png":
        with Image.open(filepath) as image:
            image.verify()
        with Image.open(filepath) as image:
            entry.update(width=image.width, height=image.height, mode=image.mode)
            assert image.format == "PNG" and image.mode == "RGB", filepath
            limit = 10 if filepath.parent.name == "Wallpaper" else 4
            assert len(contents) < limit * 1024 * 1024, filepath
    entries.append(entry)
(ARCHIVE / "manifest.json").write_text(json.dumps({
    "title": "KITCHEN CLUB",
    "archive_date": "2026-10-21",
    "files": entries,
}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

package = ROOT / "Marketing" / "kitchen-club-release-2026-10-21.zip"
with zipfile.ZipFile(package, "w", compression=zipfile.ZIP_DEFLATED) as bundle:
    for filepath in sorted(ARCHIVE.rglob("*")):
        if filepath.is_file():
            bundle.write(filepath, filepath.relative_to(ARCHIVE.parent))
with zipfile.ZipFile(package) as bundle:
    assert bundle.testzip() is None
print(f"Archived {len(entries)} files: {ARCHIVE}")
print(f"ZIP verified: {package} ({package.stat().st_size} bytes)")
