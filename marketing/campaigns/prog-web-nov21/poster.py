#!/usr/bin/env python3
"""Programming & AI (2nd secondary, baccalaureate) — Sat 21 Nov 2026, 4 pm. A delighted parent fanning the money he saved
on private lessons; slogan «دروس خصوصية ❌ دروس Dose ✅». Writes poster-feed.html (1080x1350) and poster-story.html
(1080x1920). Render: node render.js (the code-editor version is poster_code.py)."""
import io, os, sys, qrcode, qrcode.image.svg

here = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, here)
from art import art  # noqa: E402

fonts = os.path.abspath(os.path.join(here, '../../../src/Dose.Web/node_modules/@fontsource/cairo/files'))
mark = open(os.path.join(here, '../../../src/Dose.Web/public/brand/dose-mark.svg'), encoding='utf-8').read()
URL = 'https://drwsdose.com/sessions/01a1f3c0-c4e3-7a20-8000-000000002021?src=prog-poster'
TEACHER = 'عبدالرحمن كامل'


def qr_svg(url):
    q = qrcode.QRCode(error_correction=qrcode.constants.ERROR_CORRECT_M, border=0, image_factory=qrcode.image.svg.SvgPathImage)
    q.add_data(url)
    buf = io.BytesIO()
    q.make_image().save(buf)
    s = buf.getvalue().decode()
    return s[s.index('<svg'):]


def page(w, h, story):
    S = (lambda a, b: a if story else b)
    return f'''<!doctype html><html lang="ar" dir="rtl"><head><meta charset="utf-8"><style>
@font-face{{font-family:Cairo;font-weight:700;src:url("file://{fonts}/cairo-arabic-700-normal.woff2")}}
@font-face{{font-family:Cairo;font-weight:800;src:url("file://{fonts}/cairo-arabic-800-normal.woff2")}}
@font-face{{font-family:Cairo;font-weight:900;src:url("file://{fonts}/cairo-arabic-900-normal.woff2")}}
@font-face{{font-family:Cairo;font-weight:800;src:url("file://{fonts}/cairo-latin-800-normal.woff2");unicode-range:U+0000-00FF}}
@font-face{{font-family:Cairo;font-weight:900;src:url("file://{fonts}/cairo-latin-900-normal.woff2");unicode-range:U+0000-00FF}}
*{{box-sizing:border-box;margin:0;padding:0}}
html,body{{width:{w}px;height:{h}px;overflow:hidden}}
body{{font-family:Cairo,sans-serif;color:#0B1F6B;position:relative;background:#FFD43B}}
.sun{{position:absolute;left:50%;top:{S('47%','44%')};width:2400px;height:2400px;margin:-1200px 0 0 -1200px;
  background:repeating-conic-gradient(from 0deg,rgba(255,255,255,.28) 0 8deg,transparent 8deg 20deg);
  -webkit-mask:radial-gradient(circle,#000 0,#000 30%,transparent 62%);mask:radial-gradient(circle,#000 0,#000 30%,transparent 62%)}}
.wrap{{position:absolute;inset:0;padding:{S('76px 60px 66px','44px 54px 44px')};display:flex;flex-direction:column;gap:{S('22px','12px')}}}
.top{{display:flex;align-items:center;justify-content:space-between}}
.brand{{display:flex;align-items:center;gap:12px;font-weight:900;font-size:36px}}
.brand .m{{width:52px;height:52px;background:#fff;border-radius:14px;padding:6px;box-shadow:0 4px 0 #0B1F6B}} .brand .m svg{{width:100%;height:100%;display:block}}
.tag{{background:#0B1F6B;color:#fff;font-weight:900;font-size:{S('30px','26px')};padding:6px 22px;border-radius:999px}}
.tag b{{color:#FFD43B}}
.h1{{text-align:center;font-weight:900;font-size:{S('104px','92px')};line-height:1.08;letter-spacing:-1px}}
.h1 .w{{display:inline-block;background:#0B1F6B;color:#FFD43B;padding:0 22px 20px;border-radius:22px;transform:rotate(-2deg)}}
.sub{{text-align:center;font-weight:800;font-size:{S('42px','36px')};margin-top:{S('6px','0')}}}
.sub b{{background:#fff;padding:0 12px;border-radius:10px}}
.art{{flex:1;min-height:0;display:flex;align-items:center;justify-content:center;margin:{S('0','-10px 0 -16px')}}}
.art svg{{width:100%;height:100%;max-height:{S('740px','470px')}}}
.slogan{{display:flex;justify-content:center;align-items:center;gap:18px;font-weight:900;font-size:{S('46px','38px')}}}
.slogan .no{{position:relative;color:#8a6d00}}
.slogan .no::after{{content:"";position:absolute;left:-6px;right:-6px;top:52%;height:7px;background:#e5484d;border-radius:4px;transform:rotate(-6deg)}}
.slogan .yes{{background:#fff;padding:2px 20px;border-radius:14px;box-shadow:0 5px 0 #0B1F6B}}
.facts{{display:grid;grid-template-columns:repeat(3,1fr);gap:12px}}
.facts div{{background:#0B1F6B;color:#fff;border-radius:20px;padding:{S('14px 6px','8px 6px')};text-align:center}}
.facts b{{display:block;font-size:{S('40px','33px')};font-weight:900;color:#FFD43B;line-height:1.25}}
.facts span{{font-size:{S('25px','22px')};font-weight:700}}
.who{{text-align:center;font-weight:800;font-size:{S('31px','26px')}}}
.offer{{display:flex;align-items:center;gap:22px;background:#fff;border-radius:28px;padding:{S('22px 26px','14px 20px')};box-shadow:0 8px 0 #0B1F6B;border:4px solid #0B1F6B}}
.offer .t{{flex:1;display:grid;gap:2px}}
.offer .l1{{font-weight:900;font-size:{S('50px','42px')};line-height:1.2}}
.offer .l1 em{{font-style:normal;background:#2fb36b;color:#fff;padding:0 14px;border-radius:12px}}
.chip{{display:inline-block;direction:ltr;background:#0061FA;color:#fff;border-radius:10px;padding:0 12px;letter-spacing:2px}}
.offer .l2{{font-weight:800;font-size:{S('30px','25px')};color:#3c4a6e}}
.qrw{{flex:none;display:grid;justify-items:center;gap:2px}}
.qr{{width:{S('200px','150px')};height:{S('200px','150px')};padding:8px;border:5px solid #0061FA;border-radius:16px}} .qr svg{{width:100%;height:100%;display:block}} .qr svg path{{fill:#0B1F6B}}
.qrw span{{font-weight:900;font-size:21px;color:#0061FA}}
</style></head><body><div class="sun"></div><div class="wrap">
<div class="top"><div class="brand"><span class="m">{mark}</span>دروس Dose</div><div class="tag">البرمجة · تانية ثانوي <b>بكالوريا</b></div></div>
<div class="h1">وفّر فلوس<br><span class="w">الدرس الخصوصي</span>{S('',' 💸')}</div>
<div class="sub">وابنك يفهم <b>مادة البرمجة الجديدة</b> في حصة واحدة</div>
<div class="art">{art()}</div>
<div class="slogan"><span class="no">دروس خصوصية</span><span>❌</span><span class="yes">دروس Dose ✅</span></div>
<div class="facts"><div><b>السبت</b><span>21 نوفمبر</span></div><div><b>4 العصر</b><span>ساعة ونص</span></div><div><b>أونلاين</b><span>من البيت</span></div></div>
<div class="who">«تطبيقات الويب: الموقع بيشتغل إزاي؟» · مع {TEACHER}</div>
<div class="offer"><div class="t"><div class="l1"><em>ببلاش</em> لأول 15 طالب</div>
<div class="l2">بالكود <span class="chip">CODE15</span> · بعدهم 50 جنيه بس</div></div>
<div class="qrw"><div class="qr">{qr_svg(URL)}</div><span>امسح واحجز</span></div></div>
</div></body></html>'''


for name, w, h, story in [('poster-feed', 1080, 1350, False), ('poster-story', 1080, 1920, True)]:
    with open(os.path.join(here, name + '.html'), 'w', encoding='utf-8') as f:
        f.write(page(w, h, story))
print('ok')
