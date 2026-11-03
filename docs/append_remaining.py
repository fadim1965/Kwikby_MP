# -*- coding: utf-8 -*-
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parent
GUIDE_IMG = "images/guide"
ALL = sorted(p.stem for p in (ROOT/"images"/"guide").glob("*.png"))

GUIDES = [
    ("ADMIN_GUIDE_ILLUSTRATED.md", "admin-"),
    ("VENDOR_GUIDE_ILLUSTRATED.md", "seller-"),
    ("USER_GUIDE_ILLUSTRATED.md", "user-"),
]

for gname, prefix in GUIDES:
    path = ROOT / gname
    txt = path.read_text(encoding="utf-8")
    used = set(re.findall(r'images/guide/([a-z0-9-]+)\.png', txt))
    remaining = [n for n in ALL if n.startswith(prefix) and n not in used]
    if not remaining:
        continue
    out = txt.rstrip() + "\n\n## Additional screens\n\n"
    for name in remaining:
        cap = name.replace("-", " ").title()
        out += f"![{cap}]({GUIDE_IMG}/{name}.png)\n\n"
    path.write_text(out, encoding="utf-8")
    print(gname, "appended", len(remaining))
