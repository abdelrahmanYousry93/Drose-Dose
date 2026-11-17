"""Die-cut files: each bottle PNG (no background) + a CutContour spot-colour line on its exact outline."""
import glob, os
import numpy as np
from PIL import Image
from scipy import ndimage
from skimage import measure
from reportlab.pdfgen import canvas
from reportlab.lib.colors import CMYKColorSep

HERE = os.path.dirname(os.path.abspath(__file__))
DPI = 300                       # PNGs are exported at 300 dpi (3.125 x 96)
PT = 72 / DPI                   # px -> pt
CUT = CMYKColorSep(0, 1, 0, 0, spotName='CutContour', density=1)
out = os.path.join(HERE, 'diecut')
book = canvas.Canvas(os.path.join(out, 'dose-bottles-diecut-all.pdf'))

for png in sorted(glob.glob(os.path.join(HERE, 'png', 'bottle-*.png'))):
    name = os.path.basename(png)[:-4]
    im = Image.open(png); w, h = im.size
    mask = ndimage.binary_fill_holes(np.array(im.getchannel('A')) > 127)
    c = max(measure.find_contours(np.pad(mask, 1).astype(float), 0.5), key=len) - 1
    c = measure.approximate_polygon(c, tolerance=0.6)
    pts = [(x * PT, (h - y) * PT) for y, x in c]          # flip y for PDF
    svg_d = 'M' + ' L'.join(f'{x:.2f},{y:.2f}' for y, x in c) + ' Z'
    with open(os.path.join(out, name + '-cutline.svg'), 'w') as f:
        f.write(f'<svg xmlns="http://www.w3.org/2000/svg" width="{w*25.4/DPI:.2f}mm" height="{h*25.4/DPI:.2f}mm" viewBox="0 0 {w} {h}">'
                f'<path id="CutContour" d="{svg_d}" fill="none" stroke="#ec008c" stroke-width="1"/></svg>')
    for cv in (canvas.Canvas(os.path.join(out, name + '-diecut.pdf'), pagesize=(w * PT, h * PT)), book):
        cv.setPageSize((w * PT, h * PT))
        cv.drawImage(png, 0, 0, w * PT, h * PT, mask='auto')
        cv.setStrokeColor(CUT); cv.setLineWidth(0.25); cv.setFillOverprint(True); cv.setStrokeOverprint(True)
        p = cv.beginPath(); p.moveTo(*pts[0])
        for q in pts[1:]: p.lineTo(*q)
        p.close(); cv.drawPath(p, stroke=1, fill=0)
        cv.showPage()
        if cv is not book: cv.save()
    print(name, f'{w*25.4/DPI:.0f}x{h*25.4/DPI:.0f}mm', len(pts), 'pts')
book.save()
