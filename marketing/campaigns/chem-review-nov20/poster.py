#!/usr/bin/env python3
"""Chemistry review poster (Fri 20 Nov 2026, 6 pm). Writes poster-feed.html (1080x1350) and poster-story.html (1080x1920).
Render with: node render.js"""
import os
here = os.path.dirname(os.path.abspath(__file__))
fonts = os.path.abspath(os.path.join(here, '../../../src/Dose.Web/node_modules/@fontsource/cairo/files'))
qr = open(os.path.join(here, 'qr.svg'), encoding='utf-8').read()
mark = open(os.path.join(here, '../../../src/Dose.Web/public/brand/dose-mark.svg'), encoding='utf-8').read()

# Test tubes: the real colours of the hydroxide precipitates students must memorise in chapter 2.
TUBES = [('#2f7df6', 'Cu²⁺', 'أزرق'), ('#9a4a1c', 'Fe³⁺', 'بني محمر'), ('#cfe6b8', 'Fe²⁺', 'أبيض مخضر'), ('#f4f6fb', 'Al³⁺', 'أبيض جيلاتيني')]

def tubes():
    out = []
    for i, (c, ion, name) in enumerate(TUBES):
        out.append(f'''<div class="tube" style="--c:{c};--d:{i}">
  <div class="glass"><div class="liq"></div><div class="bub b1"></div><div class="bub b2"></div></div>
  <b dir="ltr">{ion}</b><span>{name}</span></div>''')
    return ''.join(out)

def page(w, h, story):
    return f'''<!doctype html><html lang="ar" dir="rtl"><head><meta charset="utf-8"><style>
@font-face{{font-family:Cairo;font-weight:400;src:url("file://{fonts}/cairo-arabic-400-normal.woff2")}}
@font-face{{font-family:Cairo;font-weight:700;src:url("file://{fonts}/cairo-arabic-700-normal.woff2")}}
@font-face{{font-family:Cairo;font-weight:800;src:url("file://{fonts}/cairo-arabic-800-normal.woff2")}}
@font-face{{font-family:Cairo;font-weight:900;src:url("file://{fonts}/cairo-arabic-900-normal.woff2")}}
@font-face{{font-family:Cairo;font-weight:800;src:url("file://{fonts}/cairo-latin-800-normal.woff2");unicode-range:U+0000-00FF,U+2070-209F,U+00B2-00B3}}
@font-face{{font-family:Cairo;font-weight:900;src:url("file://{fonts}/cairo-latin-900-normal.woff2");unicode-range:U+0000-00FF,U+2070-209F,U+00B2-00B3}}
*{{box-sizing:border-box;margin:0;padding:0}}
html,body{{width:{w}px;height:{h}px;overflow:hidden}}
body{{font-family:Cairo,sans-serif;color:#fff;background:radial-gradient(circle at 80% 18%,#1d3fa8 0%,#0B1F6B 38%,#060f3a 100%);position:relative}}
.grid{{position:absolute;inset:0;background-image:linear-gradient(rgba(255,255,255,.05) 1px,transparent 1px),linear-gradient(90deg,rgba(255,255,255,.05) 1px,transparent 1px);background-size:54px 54px}}
.wrap{{position:absolute;inset:0;padding:{'90px 70px 80px' if story else '56px 64px 52px'};display:flex;flex-direction:column;gap:{'34px' if story else '22px'}}}
.top{{display:flex;align-items:center;justify-content:space-between}}
.brand{{display:flex;align-items:center;gap:14px;font-weight:900;font-size:40px}}
.brand .m{{width:56px;height:56px;background:#fff;border-radius:14px;padding:6px}} .brand .m svg{{width:100%;height:100%;display:block}}
.free{{background:#FFC30A;color:#0B1F6B;font-weight:900;font-size:34px;padding:10px 26px;border-radius:999px;transform:rotate(-3deg);box-shadow:0 10px 30px rgba(255,195,10,.35)}}
.grade{{background:rgba(255,255,255,.12);border:2px solid rgba(255,255,255,.25);font-weight:900;font-size:34px;padding:8px 26px;border-radius:999px}}
.h2{{font-weight:900;font-size:{'72px' if story else '62px'};line-height:1.15;color:#fff;margin-top:-10px}}
.ribbon{{align-self:stretch;text-align:center;background:#FFC30A;color:#0B1F6B;font-weight:900;font-size:{'74px' if story else '62px'};line-height:1.25;padding:{'18px 10px' if story else '12px 10px'};border-radius:26px;transform:rotate(-1.5deg);box-shadow:0 14px 40px rgba(255,195,10,.4)}}
.kicker{{font-weight:800;font-size:36px;color:#9ec2ff}}
.h1{{font-weight:900;font-size:{'132px' if story else '112px'};line-height:1.1}}
.h1 .y{{color:#FFC30A}}
.sub{{font-weight:700;font-size:{'35px' if story else '35px'};line-height:1.5;color:#dbe6ff}}
.sub b{{color:#fff}}
.tubes{{display:flex;gap:26px;justify-content:center;margin:{'10px 0' if story else '0'}}}
.tube{{display:flex;flex-direction:column;align-items:center;gap:6px}}
.glass{{position:relative;width:{'96px' if story else '78px'};height:{'230px' if story else '120px'};border:5px solid rgba(255,255,255,.75);border-top:none;border-radius:0 0 50px 50px;overflow:hidden;background:rgba(255,255,255,.06)}}
.glass:before{{content:"";position:absolute;top:0;left:-9px;right:-9px;height:10px;background:rgba(255,255,255,.75);border-radius:6px}}
.liq{{position:absolute;inset:auto 0 0 0;height:58%;background:var(--c);box-shadow:0 0 40px var(--c)}}
.bub{{position:absolute;width:12px;height:12px;border-radius:50%;background:rgba(255,255,255,.55)}} .b1{{left:22%;bottom:30%}} .b2{{left:58%;bottom:15%;width:8px;height:8px}}
.tube b{{font-size:{'34px' if story else '28px'};font-weight:900;line-height:1.2}} .tube span{{font-size:{'24px' if story else '21px'};font-weight:700;color:#9ec2ff}}
.when{{display:grid;grid-template-columns:repeat(3,1fr);gap:16px}}
.when div{{background:rgba(255,255,255,.08);border:2px solid rgba(255,255,255,.16);border-radius:22px;padding:16px 10px;text-align:center}}
.when b{{display:block;font-size:{'44px' if story else '38px'};font-weight:900;color:#FFC30A;line-height:1.2}}
.when span{{font-size:26px;font-weight:700;color:#dbe6ff}}
.offer{{display:flex;align-items:center;gap:26px;background:#fff;color:#0B1F6B;border-radius:30px;padding:{'26px 30px' if story else '20px 26px'};margin-top:auto}}
.offer .t{{flex:1;display:grid;gap:6px}}
.offer .l1{{font-weight:900;font-size:{'44px' if story else '38px'};line-height:1.25}}
.code{{display:inline-block;direction:ltr;background:#0061FA;color:#fff;border-radius:12px;padding:0 16px;letter-spacing:2px}}
.offer .l2{{font-weight:800;font-size:28px;color:#3c4a6e}}
.offer .l3{{font-weight:900;font-size:30px;color:#07803a}}
.qrw{{flex:none;display:grid;justify-items:center;gap:6px}}
.qr{{width:{'210px' if story else '178px'};height:{'210px' if story else '178px'};padding:10px;border:5px solid #0061FA;border-radius:18px}} .qr svg path{{fill:#0B1F6B}}
.qrw span{{font-weight:900;font-size:24px;color:#0061FA}}
.foot{{display:flex;justify-content:space-between;align-items:center;font-weight:800;font-size:28px;color:#9ec2ff}}
.foot b{{color:#fff;direction:ltr}}
</style></head><body><div class="grid"></div><div class="wrap">
<div class="top"><div class="brand"><span class="m">{mark}</span>دروس Dose</div><div class="grade">تالتة ثانوي 🧪</div></div>
<div class="h1"><span class="y">مراجعة</span> كيمياء</div>
<div class="h2">الباب الأول والتاني</div>
<div class="ribbon">🎁 مجانًا لأول 20 طالب</div>
<div class="sub">العناصر الانتقالية · <b>الكشف عن الشقوق</b> · <b>مسائل المعايرة</b></div>
<div class="tubes">{tubes()}</div>
<div class="when"><div><b>الجمعة</b><span>20 نوفمبر</span></div><div><b>6 مساءً</b><span>ساعتين مراجعة</span></div><div><b>أونلاين</b><span>من بيتك</span></div></div>
<div class="offer"><div class="t">
  <div class="l1">احجز بالكود <span class="code">CHEM20</span><br/>والمراجعة عليك ببلاش</div>
  <div class="l2">لأول 20 بس، وبعدهم 100 جنيه للجلسة</div>
  <div class="l3">💊 جرعة مراجعة واحدة، مش اشتراك شهر</div>
</div><div class="qrw"><div class="qr">{qr}</div><span>امسح واحجز</span></div></div>
<div class="foot" style="display:{'flex' if story else 'none'}"><span>الأماكن المجانية بتخلص بالترتيب</span><b>drwsdose.com</b></div>
</div></body></html>'''

open(os.path.join(here, 'poster-feed.html'), 'w', encoding='utf-8').write(page(1080, 1350, False))
open(os.path.join(here, 'poster-story.html'), 'w', encoding='utf-8').write(page(1080, 1920, True))
print('ok')
