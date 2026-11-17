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

# Second set of 8
B2 = [
  ('percent', 'النسبة المئوية', 'رياضيات', '#0061fa',
   '<b>النسبة = الجزء ÷ الكل × 100</b><br/>خصم <span class="ltr">20%</span> على 150 جنيه = 30 جنيه<br/>يعني تدفع 120 بس.'),
  ('pythagoras', 'فيثاغورس', 'رياضيات', '#0061fa',
   'في المثلث القائم الزاوية:<br/><b class="ltr">a² + b² = c²</b><br/>الضلعين 3 و 4 ← الوتر 5'),
  ('photosynthesis', 'البناء الضوئي', 'علوم', '#07b24c',
   'بياخد: <span class="ltr">CO₂</span> + ميه + ضوء الشمس<br/>ويطلّع: <b>جلوكوز + أكسجين</b><br/>يعني النبات بيعمل أكله بنفسه.'),
  ('cell', 'الخلية', 'علوم', '#07b24c',
   'الخلية النباتية فيها حاجتين زيادة:<br/><b>جدار خلوي + بلاستيدات خضراء</b><br/>والحيوانية مفيهاش الاتنين.'),
  ('kana', 'كان وإنّ', 'عربي', '#d97706',
   '<b>كان</b>: الاسم مرفوع والخبر منصوب<br/><b>كان الجوُّ جميلاً</b><br/><b>إنّ</b> العكس: <b>إنّ الجوَّ جميلٌ</b>'),
  ('passive', 'Passive Voice', 'إنجليزي', '#d97706',
   '<b class="ltr">be + V3</b><br/>المهم الحدث، مش مين عمله:<br/><b class="ltr">The window was broken.</b>'),
  ('newton', 'قانون نيوتن التاني', 'فيزياء', '#7c3aed',
   '<b class="ltr">F = m × a</b><br/>القوة = الكتلة × العجلة<br/>نفس الزقة ← الأخف يسرّع أكتر'),
  ('ph', 'الأحماض والقواعد', 'كيمياء', '#7c3aed',
   'على مقياس <span class="ltr">pH</span>:<br/>أقل من 7 ← <b>حمض</b> (الليمون)<br/>أكتر من 7 ← <b>قاعدة</b> (الصابون)'),
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
 <div class="neck"></div>
 <div class="body">
  <div class="label">
   <div class="band"><span class="mark">{logo}</span><b>دروس <bdi>Dose</bdi></b><span class="chip">{subj}</span></div>
   <div class="in">
    <div class="name"><bdi>{name}</bdi></div>
    <div class="fact"><span class="k">💡 معلومة في 10 ثواني</span>{fact}</div>
    <div class="scan">
     <div class="tab">📱 امسح الكود</div>
     <div class="qr">{qr_svg(url)}<span class="logo">{logo}</span></div>
     <div class="why">واعرف إزاي جهتك تدّي طالب الجرعة دي</div>
    </div>
   </div>
   <div class="band2">جرعة فهم · مش اشتراك شهر</div>
  </div>
 </div>
</div>'''

def build(items, out):
    pages = [items[i:i + 4] for i in range(0, len(items), 4)]
    html = f'''<!doctype html><html lang="ar" dir="rtl"><head><meta charset="utf-8"><title>Dose bottles SES26</title><style>
@font-face{{font-family:Cairo;font-weight:400;src:url("file://{fonts}/cairo-arabic-400-normal.woff2")}}
@font-face{{font-family:Cairo;font-weight:700;src:url("file://{fonts}/cairo-arabic-700-normal.woff2")}}
@font-face{{font-family:Cairo;font-weight:800;src:url("file://{fonts}/cairo-arabic-800-normal.woff2")}}
@font-face{{font-family:Cairo;font-weight:900;src:url("file://{fonts}/cairo-arabic-900-normal.woff2")}}
@font-face{{font-family:Cairo;font-weight:800;src:url("file://{fonts}/cairo-latin-800-normal.woff2");unicode-range:U+0000-00FF}}
@page{{size:A4;margin:0}}
*{{box-sizing:border-box;margin:0}}
body{{font-family:Cairo,sans-serif;color:#0f1b3d;-webkit-print-color-adjust:exact;print-color-adjust:exact;background:#fff}}
.page{{width:210mm;height:297mm;display:grid;grid-template-columns:repeat(2,94mm);grid-auto-rows:143mm;gap:0;justify-content:center;align-content:center;page-break-after:always}}
.card{{display:flex;flex-direction:column;align-items:center;padding:3mm 0 3.5mm;outline:.25mm dashed #9aa3b5;background:radial-gradient(circle at 50% 45%,color-mix(in srgb,var(--c) 70%,#fff) 0%,var(--c) 45%,color-mix(in srgb,var(--c) 55%,#0B1F6B) 100%);-webkit-print-color-adjust:exact}}
.cap{{box-shadow:0 1.5mm 3mm rgba(0,0,0,.3);width:58mm;height:13mm;border:.35mm solid #b9c0cc;border-radius:2.5mm 2.5mm 1.2mm 1.2mm;background:repeating-linear-gradient(90deg,#f4f6f9 0 2.2mm,#d9dee6 2.2mm 2.8mm),#fff;box-shadow:inset 0 -2.2mm 0 #e3e7ee}}
.neck{{width:52mm;height:3mm;border-inline:.35mm solid #b9c0cc;background:linear-gradient(90deg,#dfe3ea,#fff 35%,#fff 65%,#dfe3ea)}}
.body{{width:78mm;flex:1;box-shadow:0 2mm 4mm rgba(0,0,0,.35);border:.35mm solid #b9c0cc;border-radius:9mm 9mm 5mm 5mm;background:linear-gradient(90deg,#dde2ea,#fbfcfd 22%,#fff 50%,#f4f6f9 78%,#d5dbe4);padding:8mm 2.6mm 6mm}}
.label{{height:100%;background:#fff;border-radius:1.5mm;overflow:hidden;display:flex;flex-direction:column;box-shadow:0 0 0 .3mm #e3e7ee}}
.band{{background:var(--c);color:#fff;display:flex;align-items:center;gap:1.6mm;padding:1.6mm 3mm;font-size:3.8mm}}
.band .mark{{background:#fff;border-radius:1.4mm;padding:.5mm}}
.mark svg{{width:4.4mm;height:4.4mm;display:block}}
.chip{{margin-inline-start:auto;background:#fff;color:var(--c);border-radius:9mm;padding:0 2.4mm;font-size:3.1mm;font-weight:900;line-height:1.7}}
.in{{flex:1;display:flex;flex-direction:column;gap:1.6mm;padding:2.2mm 3mm 3mm}}
.band2{{background:var(--c);color:#fff;font-weight:800;font-size:2.9mm;text-align:center;padding:.8mm}}
.name{{font-weight:900;font-size:7.4mm;line-height:1.2;color:#0b1f6b}}
.fact{{flex:none;background:#f5f8ff;border-radius:2.2mm;padding:2mm 2.4mm;font-size:3.5mm;line-height:1.55;font-weight:700;color:#0f1b3d}}
.fact .k{{display:block;font-size:2.8mm;font-weight:800;color:var(--c);margin-bottom:.4mm}}
.fact b{{font-weight:900;color:#0b1f6b}}
.ltr{{direction:ltr;unicode-bidi:isolate;display:inline-block}}
.scan{{margin-top:auto;position:relative;border:.8mm solid var(--c);border-radius:3mm;padding:4.4mm 2mm 1.8mm;display:flex;flex-direction:column;align-items:center;gap:1.2mm}}
.tab{{position:absolute;top:-3.6mm;background:var(--c);color:#fff;font-weight:900;font-size:3.6mm;padding:.2mm 3.4mm;border-radius:9mm;white-space:nowrap}}
.qr{{position:relative;width:34mm;height:34mm}}
.qr svg path{{fill:#0b1f6b}}
.qr .logo{{position:absolute;inset:0;margin:auto;width:8.5mm;height:8.5mm;background:#fff;border-radius:1.6mm;padding:.8mm}}
.qr .logo svg{{width:100%;height:100%;display:block}}
.why{{font-size:3.1mm;font-weight:800;line-height:1.35;text-align:center;color:#0b1f6b}}
</style></head><body>
{''.join('<div class="page">' + ''.join(card(*b) for b in p) + '</div>' for p in pages)}
</body></html>'''
    open(os.path.join(here, out), 'w', encoding='utf-8').write(html)

build(B, 'bottles.html')
build(B2, 'bottles-2.html')
print('ok', len(B), len(B2))
