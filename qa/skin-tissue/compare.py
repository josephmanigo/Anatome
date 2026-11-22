"""Lay out the matched Unity captures at their original aspect ratio."""
from pathlib import Path
import json
from PIL import Image, ImageChops, ImageDraw, ImageFont, ImageStat


folder = Path(__file__).resolve().parent
faces = (("Skin", "FMA7163"), ("Subcutaneous fat", "REC_fat"), ("Thoracic fascia", "REC_fascia"), ("Pectoralis major", "FMA79979"))
font_dir = Path("C:/Windows/Fonts")
head = ImageFont.truetype(str(font_dir / "segoeuib.ttf"), 44)
title = ImageFont.truetype(str(font_dir / "segoeuib.ttf"), 27)
body = ImageFont.truetype(str(font_dir / "segoeui.ttf"), 21)
width, height, gap, left = 420, 263, 12, 38
canvas = Image.new("RGB", (2 * left + 4 * width + 3 * gap, 1570), "#f2f3f0")
draw = ImageDraw.Draw(canvas)
draw.text((left, 15), "Thoracic skin and tissue", font=head, fill="#142b2a")
draw.text((left, 80), "Unity renders of the same meshes and native cut fragments under matched lighting.", font=body, fill="#334743")
for col, label in enumerate(("Intact / before", "Intact / updated", "Cut surface / before", "Cut surface / updated")):
    draw.text((left + col*(width+gap), 130), label, font=body, fill="#385954")
for row, (name, key) in enumerate(faces):
    y = 177 + row * 339
    draw.text((left,y), name, font=title, fill="#142b2a")
    for col, (phase, suffix) in enumerate((("before",""), ("after",""), ("before","-cut"), ("after","-cut"))):
        with Image.open(folder / phase / (key + suffix + ".png")) as image:
            canvas.paste(image.convert("RGB").resize((width,height),Image.Resampling.LANCZOS), (left+col*(width+gap),y+48))
draw.text((left,1530), "Fat and fascia use reconstructed teaching envelopes. Colours and surface detail are illustrative.",font=body,fill="#334743")
canvas.save(folder/"comparison.jpg",quality=94,subsampling=0)

before = json.loads((folder/"before/report.json").read_text(encoding="utf-8"))
after = json.loads((folder/"after/report.json").read_text(encoding="utf-8"))
def geometry(report):
    return {v["id"]: (v["sourceTriangles"], v["exposedTriangles"]) for v in report["views"]}

if geometry(before) != geometry(after):
    raise ValueError("The matched reference views contain different triangle counts.")
differences = {}
for path in sorted((folder/"before").glob("*.png")):
    with Image.open(path) as old, Image.open(folder/"after"/path.name) as new:
        if old.size != new.size:
            raise ValueError("Camera resolution changed for " + path.name)
        delta = ImageChops.difference(old.convert("RGB"), new.convert("RGB"))
        if not delta.getbbox():
            raise ValueError("Expected a changed surface in " + path.name)
        differences[path.name] = [round(n, 3) for n in ImageStat.Stat(delta).mean]
with Image.open(folder/"after/case-detail.png") as normal, Image.open(folder/"after/case-detail-unshadowed.png") as control:
    crop = (215, 230, 680, 337)
    no_self_shadow = not ImageChops.difference(normal.crop(crop).convert("RGB"), control.crop(crop).convert("RGB")).getbbox()
if not no_self_shadow:
    raise ValueError("The isolated silicone opening still casts a shadow on itself.")
report = {"before": before["appearanceVersion"], "after": after["appearanceVersion"], "geometry": geometry(after),
          "meanAbsoluteChannelChange_0_255": differences, "isolatedOpeningMatchesNoShadowControl": no_self_shadow}
(folder/"render-comparison.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
