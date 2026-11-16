#!/usr/bin/env python3
"""Bottle-shaped event cards (Social Education Summit 2026). Each card is a medicine bottle 'for' one basic lesson
students struggle with; its QR opens drwsdose.com/partners?src=ses26-<slug> so we know which bottle was scanned.
Run: python3 bottles.py  -> bottles.html (then render to PDF with render.js)."""
import io, os, qrcode, qrcode.image.svg, re
here = os.path.dirname(os.path.abspath(__file__))
fonts = os.path.abspath(os.path.join(here, '../../../src/Dose.Web/node_modules/@fontsource/cairo/files'))
logo = open(os.path.join(here, '../../../src/Dose.Web/public/brand/dose-mark.svg'), encoding='utf-8').read()

# slug, short name on the bottle, full condition, subject chip, cap colour
B = [
  ('longdiv', 'القسمة المطوّلة', 'عدم فهم القسمة المطوّلة', 'رياضيات', '#0061fa'),
  ('fractions', 'الكسور', 'عدم فهم جمع وطرح الكسور', 'رياضيات', '#0061fa'),
  ('density', 'الكثافة', 'عدم فهم الكثافة', 'علوم', '#07b24c'),
  ('speed', 'السرعة والعجلة', 'الخلط بين السرعة والعجلة', 'علوم', '#07b24c'),
  ('irab', 'الإعراب', 'الخوف من سؤال الإعراب', 'عربي', '#d97706'),
  ('perfect', 'Present Perfect', 'عدم فهم الـ Present Perfect', 'إنجليزي', '#d97706'),
  ('ohm', 'قانون أوم', 'عدم فهم قانون أوم', 'فيزياء', '#7c3aed'),
  ('mole', 'المول', 'عدم فهم المول', 'كيمياء', '#7c3aed'),
]

def qr_svg(url):
    q = qrcode.QRCode(error_correction=qrcode.constants.ERROR_CORRECT_M, border=1, image_factory=qrcode.image.svg.SvgPathImage)
    q.add_data(url); q.make(fit=True)
    buf = io.BytesIO(); q.make_image().save(buf)
    s = buf.getvalue().decode()
    s = re.sub(r'<\?xml[^>]*\?>', '', s)
    return re.sub(r'width="[^"]*" height="[^"]*"', 'width="100%" height="100%"', s, count=1)

def card(slug, name, cond, subj, color):
    url = f'https://drwsdose.com/partners?src=ses26-{slug}'
    big = 'big' if len(name) <= 8 else ''
    return f'''
<div class="card">
 <svg class="shape" viewBox="0 0 70 120" preserveAspectRatio="none">
  <path d="M22 1 h26 a3 3 0 0 1 3 3 v11 a2 2 0 0 1 -2 2 h-1 v4 c10 2 18 8 18 18 v74 a6 6 0 0 1 -6 6 h-50 a6 6 0 0 1 -6 -6 v-74 c0 -10 8 -16 18 -18 v-4 h-1 a2 2 0 0 1 -2 -2 v-11 a3 3 0 0 1 3 -3 z" class="cut"/>
  <rect x="20.5" y="1" width="29" height="14.5" rx="3" fill="{color}"/>
  <g stroke="rgba(255,255,255,.28)" stroke-width=".8">{''.join(f'<line x1="{x}" y1="2" x2="{x}" y2="14.5"/>' for x in range(23, 49, 3))}</g>
  <path d="M22 17 h26 v4 c10 2 18 8 18 18 v74 a6 6 0 0 1 -6 6 h-50 a6 6 0 0 1 -6 -6 v-74 c0 -10 8 -16 18 -18 z" fill="url(#amber)"/>
  <path d="M9 40 c0 -6 3 -10 8 -12" stroke="rgba(255,255,255,.35)" stroke-width="1.6" fill="none" stroke-linecap="round"/>
  <rect x="7" y="44" width="2" height="62" rx="1" fill="rgba(255,255,255,.18)"/>
 </svg>
 <div class="label" style="--c:{color}">
  <div class="top"><span class="mark">{logo}</span><b dir="ltr">Dose®</b><span class="chip">{subj}</span></div>
  <div class="name {big}"><bdi>{name}</bdi></div>
  <div class="row"><i>دواعي الاستعمال</i> {cond}</div>
  <div class="row"><i>الجرعة</i> جلسة شرح واحدة · 60 دقيقة · أونلاين</div>
  <div class="row side"><i>آثار جانبية</i> ثقة زيادة في الامتحان</div>
  <div class="qrbox"><div class="qr">{qr_svg(url)}</div><div class="cta">امسح<br/>واكتب جرعة<br/>لطالب</div></div>
  <div class="foot">لا يحتاج اشتراك شهري · drwsdose.com</div>
 </div>
</div>'''

pages = [B[i:i + 4] for i in range(0, len(B), 4)]
html = f'''<!doctype html><html lang="ar" dir="rtl"><head><meta charset="utf-8"><title>Dose bottles SES26</title><style>
@font-face{{font-family:Cairo;font-weight:400;src:url("file://{fonts}/cairo-arabic-400-normal.woff2")}}
@font-face{{font-family:Cairo;font-weight:700;src:url("file://{fonts}/cairo-arabic-700-normal.woff2")}}
@font-face{{font-family:Cairo;font-weight:800;src:url("file://{fonts}/cairo-arabic-800-normal.woff2")}}
@font-face{{font-family:Cairo;font-weight:900;src:url("file://{fonts}/cairo-arabic-900-normal.woff2")}}
@font-face{{font-family:Cairo;font-weight:800;src:url("file://{fonts}/cairo-latin-800-normal.woff2");unicode-range:U+0000-00FF}}
@page{{size:A4;margin:0}}
*{{box-sizing:border-box;margin:0}}
body{{font-family:Cairo,sans-serif;color:#0f1b3d;-webkit-print-color-adjust:exact;print-color-adjust:exact;background:#fff}}
.page{{width:210mm;height:297mm;display:grid;grid-template-columns:repeat(2,80mm);grid-auto-rows:136mm;gap:6mm 14mm;justify-content:center;align-content:center;page-break-after:always}}
.card{{position:relative;width:80mm;height:136mm}}
.shape{{position:absolute;inset:0;width:100%;height:100%;overflow:visible}}
.cut{{fill:none;stroke:#9aa3b5;stroke-width:.35;stroke-dasharray:1.4 1}}
.label{{position:absolute;left:5.5mm;right:5.5mm;top:47mm;height:73mm;background:#fff;border-radius:2.5mm;border-top:2.2mm solid var(--c);border-bottom:1.6mm solid #ffc30a;padding:2mm 2.6mm 1.4mm;display:flex;flex-direction:column;gap:.7mm;box-shadow:0 .4mm 1.2mm rgba(0,0,0,.25)}}
.top{{display:flex;align-items:center;gap:1.4mm;font-size:3.1mm;color:#0b1f6b}}
.mark svg{{width:4.4mm;height:4.4mm;display:block}}
.chip{{margin-inline-start:auto;background:var(--c);color:#fff;border-radius:9mm;padding:0 2mm;font-size:2.5mm;font-weight:800;line-height:1.7}}
.name{{font-weight:900;font-size:5.2mm;line-height:1.25;color:#0b1f6b}}
.name.big{{font-size:6.6mm}}
.row{{font-size:2.35mm;line-height:1.45;font-weight:700}}
.row i{{font-style:normal;display:block;font-size:1.95mm;font-weight:400;color:#5b6785}}
.side{{color:#07803a}}
.qrbox{{margin-top:auto;display:flex;align-items:center;gap:2mm}}
.qr{{width:19mm;height:19mm;flex:none}}
.qr svg path{{fill:#0b1f6b}}
.cta{{font-weight:900;font-size:3.1mm;line-height:1.3;color:var(--c)}}
.foot{{font-size:1.85mm;color:#5b6785;text-align:center;border-top:.2mm dashed #dbe5f7;padding-top:.6mm}}
</style></head><body>
<svg width="0" height="0" style="position:absolute"><defs><linearGradient id="amber" x1="0" x2="1"><stop offset="0" stop-color="#6b340a"/><stop offset=".38" stop-color="#b8621a"/><stop offset=".62" stop-color="#a65616"/><stop offset="1" stop-color="#5e2d08"/></linearGradient></defs></svg>
{''.join('<div class="page">' + ''.join(card(*b) for b in p) + '</div>' for p in pages)}
</body></html>'''
open(os.path.join(here, 'bottles.html'), 'w', encoding='utf-8').write(html)
print('ok', len(B))
