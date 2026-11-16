#!/usr/bin/env python3
"""Bottle-shaped event cards (Social Education Summit 2026). Each card is a medicine bottle 'for' one basic lesson
students struggle with; its QR opens drwsdose.com/partners?src=ses26-<slug> so we know which bottle was scanned.
Run: python3 bottles.py  -> bottles.html (then render to PDF with render.js)."""
import io, os, qrcode, qrcode.image.svg, re
here = os.path.dirname(os.path.abspath(__file__))
fonts = os.path.abspath(os.path.join(here, '../../../src/Dose.Web/node_modules/@fontsource/cairo/files'))
logo = open(os.path.join(here, '../../../src/Dose.Web/public/brand/dose-mark.svg'), encoding='utf-8').read()

# slug, name on the bottle, subject chip, colour, quick fact (one idea, tested wording)
B = [
  ('longdiv', 'القسمة المطوّلة', 'رياضيات', '#0061fa',
   'هي 4 خطوات بتتكرر وبس:<br/><b>اقسم ← اضرب ← اطرح ← نزّل</b><br/>وكرّرها لحد ما الأرقام تخلص.'),
  ('fractions', 'الكسور', 'رياضيات', '#0061fa',
   'مينفعش تجمع كسرين غير لما المقامات تبقى واحدة:<br/><b class="ltr">1/2 + 1/3 = 3/6 + 2/6 = 5/6</b>'),
  ('density', 'الكثافة', 'علوم', '#07b24c',
   '<b>الكثافة = الكتلة ÷ الحجم</b><br/>علشان كده الخشب بيطفو على الميه، والحديد بيغرق.'),
  ('speed', 'السرعة والعجلة', 'علوم', '#07b24c',
   '<b>السرعة</b>: بتقطع قد إيه في الثانية.<br/><b>العجلة</b>: سرعتك بتتغير قد إيه كل ثانية.<br/>عربية ماشية ثابتة على 60 = عجلتها صفر.'),
  ('irab', 'الإعراب', 'عربي', '#d97706',
   '<b>الفاعل</b>: عمل الفعل ← مرفوع<br/><b>المفعول به</b>: وقع عليه الفعل ← منصوب<br/><b>كتبَ الطالبُ الدرسَ</b>'),
  ('perfect', 'Present Perfect', 'إنجليزي', '#d97706',
   '<b class="ltr">have / has + V3</b><br/>حاجة حصلت ولسه أثرها موجود.<br/><b class="ltr">I have lost my keys.</b>'),
  ('ohm', 'قانون أوم', 'فيزياء', '#7c3aed',
   '<b class="ltr">V = I × R</b><br/>الجهد = التيار × المقاومة<br/>المقاومة تزيد ← التيار يقل'),
  ('mole', 'المول', 'كيمياء', '#7c3aed',
   'المول عدد، زي الدستة بالظبط:<br/>الدستة = 12<br/><b>المول = <span class="ltr">6.02 × 10²³</span></b>'),
]

def qr_svg(url):
    q = qrcode.QRCode(error_correction=qrcode.constants.ERROR_CORRECT_H, border=0, image_factory=qrcode.image.svg.SvgPathImage)
    q.add_data(url); q.make(fit=True)
    buf = io.BytesIO(); q.make_image().save(buf)
    s = buf.getvalue().decode()
    s = re.sub(r'<\?xml[^>]*\?>', '', s)
    return re.sub(r'width="[^"]*" height="[^"]*"', 'width="100%" height="100%"', s, count=1)

def card(slug, name, subj, color, fact):
    url = f'https://drwsdose.com/partners?src=ses26-{slug}'
    return f'''
<div class="card" style="--c:{color}">
 <div class="cap"></div>
 <div class="body">
  <div class="label">
   <div class="top"><span class="mark">{logo}</span><b dir="ltr">Dose®</b><span class="chip">{subj}</span></div>
   <div class="name"><bdi>{name}</bdi></div>
   <div class="fact"><span class="k">💡 معلومة في 10 ثواني</span>{fact}</div>
   <div class="scan">
    <div class="tab">📱 امسح الكود</div>
    <div class="qr">{qr_svg(url)}<span class="logo">{logo}</span></div>
    <div class="why">واعرف إزاي جهتك تدّي<br/>طالب الجرعة دي</div>
   </div>
  </div>
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
.page{{width:210mm;height:297mm;display:grid;grid-template-columns:repeat(2,82mm);grid-auto-rows:140mm;gap:5mm 12mm;justify-content:center;align-content:center;page-break-after:always}}
.card{{display:flex;flex-direction:column;align-items:center}}
.cap{{width:38mm;height:11mm;background:var(--c);border-radius:2mm 2mm 0 0;background-image:repeating-linear-gradient(90deg,transparent 0 3mm,rgba(255,255,255,.3) 3mm 3.6mm)}}
.body{{width:82mm;flex:1;background:linear-gradient(90deg,#7a3e0c,#b8621a 45%,#8a4b12);border-radius:5mm;padding:4mm 3.5mm;outline:.3mm dashed #9aa3b5;outline-offset:.8mm}}
.label{{height:100%;background:#fff;border-radius:3mm;border-top:2.4mm solid var(--c);border-bottom:2mm solid #ffc30a;padding:2.4mm 3mm 3mm;display:flex;flex-direction:column;gap:1.6mm}}
.top{{display:flex;align-items:center;gap:1.4mm;font-size:3.6mm;color:#0b1f6b}}
.mark svg{{width:5mm;height:5mm;display:block}}
.chip{{margin-inline-start:auto;background:var(--c);color:#fff;border-radius:9mm;padding:0 2.4mm;font-size:3.1mm;font-weight:800;line-height:1.7}}
.name{{font-weight:900;font-size:7.4mm;line-height:1.2;color:#0b1f6b}}
.fact{{flex:none;background:#f5f8ff;border-radius:2.2mm;padding:2mm 2.4mm;font-size:3.6mm;line-height:1.55;font-weight:700;color:#0f1b3d}}
.fact .k{{display:block;font-size:2.8mm;font-weight:800;color:var(--c);margin-bottom:.4mm}}
.fact b{{font-weight:900;color:#0b1f6b}}
.ltr{{direction:ltr;unicode-bidi:isolate;display:inline-block}}
.scan{{margin-top:auto;position:relative;border:.8mm solid var(--c);border-radius:3mm;padding:4.4mm 2mm 2mm;display:flex;flex-direction:column;align-items:center;gap:1.4mm}}
.tab{{position:absolute;top:-3.6mm;background:var(--c);color:#fff;font-weight:900;font-size:3.6mm;padding:.2mm 3.4mm;border-radius:9mm;white-space:nowrap}}
.qr{{position:relative;width:38mm;height:38mm}}
.qr svg path{{fill:#0b1f6b}}
.qr .logo{{position:absolute;inset:0;margin:auto;width:9mm;height:9mm;background:#fff;border-radius:1.6mm;padding:.8mm}}
.qr .logo svg{{width:100%;height:100%;display:block}}
.why{{font-size:3.2mm;font-weight:800;line-height:1.35;text-align:center;color:#0b1f6b}}
</style></head><body>
{''.join('<div class="page">' + ''.join(card(*b) for b in p) + '</div>' for p in pages)}
</body></html>'''
open(os.path.join(here, 'bottles.html'), 'w', encoding='utf-8').write(html)
print('ok', len(B))
