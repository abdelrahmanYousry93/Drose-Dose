#!/usr/bin/env python3
"""Programming & AI (2nd secondary, baccalaureate) — Sat 21 Nov 2026, 4 pm. A private-lesson receipt where every line is
crossed out except the one dose you need, stamped «ببلاش». Slogan «دروس خصوصية ❌ دروس Dose ✅».
Writes poster-feed.html (1080x1350) and poster-story.html (1080x1920). Render: node render.js
(older versions: poster_code.py — code editor; art.py — cartoon parent, not used)."""
import io, os, qrcode, qrcode.image.svg

here = os.path.dirname(os.path.abspath(__file__))
fonts = os.path.abspath(os.path.join(here, '../../../src/Dose.Web/node_modules/@fontsource/cairo/files'))
mark = open(os.path.join(here, '../../../src/Dose.Web/public/brand/dose-mark.svg'), encoding='utf-8').read()
URL = 'https://drwsdose.com/sessions/01a1f3c0-c4e3-7a20-8000-000000002021?src=prog-poster'
TEACHER = 'عبدالرحمن كامل'

# Receipt lines: (label, mark). Crossed lines are what a private-lesson month makes you pay for.
LINES = [
    ('اشتراك شهر كامل', 'x'),
    ('حصص في أجزاء إنت فاهمها أصلًا', 'x'),
    ('مشوار رايح جاي للسنتر', 'x'),
    ('حصة واحدة للجزء اللي واقف معاك', 'ok'),
]


def qr_svg(url):
    q = qrcode.QRCode(error_correction=qrcode.constants.ERROR_CORRECT_M, border=0, image_factory=qrcode.image.svg.SvgPathImage)
    q.add_data(url)
    buf = io.BytesIO()
    q.make_image().save(buf)
    s = buf.getvalue().decode()
    return s[s.index('<svg'):]


def lines_html():
    out = []
    for label, m in LINES:
        cls = 'ln ok' if m == 'ok' else 'ln no'
        mark_ = '<i class="m ok">✓</i>' if m == 'ok' else '<i class="m no">✕</i>'
        out.append(f'<div class="{cls}"><span class="lb">{label}</span><span class="dots"></span>{mark_}</div>')
    return ''.join(out)


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
body{{font-family:Cairo,sans-serif;color:#fff;position:relative;background:radial-gradient(circle at 50% 42%,#1d3fa8 0%,#0B1F6B 45%,#050d33 100%)}}
.grain{{position:absolute;inset:0;background-image:radial-gradient(rgba(255,255,255,.06) 1.5px,transparent 1.5px);background-size:28px 28px}}
.wrap{{position:absolute;inset:0;padding:{S('80px 60px 66px','46px 54px 44px')};display:flex;flex-direction:column;gap:{S('26px','16px')}}}
.top{{display:flex;align-items:center;justify-content:space-between}}
.brand{{display:flex;align-items:center;gap:12px;font-weight:900;font-size:36px}}
.brand .m{{width:52px;height:52px;background:#fff;border-radius:14px;padding:6px}} .brand .m svg{{width:100%;height:100%;display:block}}
.tag{{background:#FFC30A;color:#0B1F6B;font-weight:900;font-size:{S('30px','26px')};padding:6px 22px;border-radius:999px}}
.h1{{text-align:center;font-weight:900;font-size:{S('86px','74px')};line-height:1.12}}
.h1 .y{{color:#FFC30A}}
.sub{{text-align:center;font-weight:800;font-size:{S('40px','34px')};color:#c9d6ff;margin-top:{S('-6px','-10px')}}}
/* receipt */
.stage{{flex:1;min-height:0;display:flex;align-items:center;justify-content:center;position:relative}}
.rc{{position:relative;width:{S('860px','820px')};background:#fffdf6;color:#1c2340;padding:{S('30px 44px 28px','22px 40px 20px')};transform:rotate(-2.5deg);
  box-shadow:0 40px 80px rgba(0,0,0,.5);
  -webkit-mask:linear-gradient(#000,#000) content-box,conic-gradient(from -45deg at bottom,#0000,#000 1deg 89deg,#0000 90deg) 50%/34px 100%,conic-gradient(from 135deg at top,#0000,#000 1deg 89deg,#0000 90deg) 50%/34px 100%;
  -webkit-mask-composite:source-over}}
.rc::before,.rc::after{{content:"";position:absolute;left:0;right:0;height:18px;background:radial-gradient(circle at 17px -2px,transparent 12px,#fffdf6 13px) 0 0/34px 18px repeat-x}}
.rc::before{{top:-17px;transform:rotate(180deg)}} .rc::after{{bottom:-17px}}
.rh{{display:flex;justify-content:space-between;align-items:baseline;border-bottom:3px dashed #c3c8d8;padding-bottom:{S('16px','10px')};margin-bottom:{S('14px','8px')}}}
.rh b{{font-weight:900;font-size:{S('46px','34px')}}} .rh span{{font-weight:800;font-size:{S('26px','22px')};color:#8a90a8}}
.ln{{display:flex;align-items:center;gap:14px;font-weight:800;font-size:{S('34px','29px')};padding:{S('8px 0','3px 0')}}}
.ln .dots{{flex:1;border-bottom:4px dotted #c3c8d8;transform:translateY(-8px)}}
.ln.no .lb{{color:#9aa0b6;text-decoration:line-through;text-decoration-color:#e5484d;text-decoration-thickness:6px}}
.ln.ok .lb{{color:#0B1F6B;font-weight:900;background:#fff3c4;padding:0 10px;border-radius:8px}}
.m{{font-style:normal;width:{S('52px','40px')};height:{S('52px','40px')};border-radius:50%;display:grid;place-items:center;font-weight:900;font-size:{S('30px','26px')};flex:none;color:#fff}}
.m.no{{background:#e5484d}} .m.ok{{background:#22a85a}}
.tot{{display:flex;justify-content:space-between;align-items:center;border-top:3px dashed #c3c8d8;margin-top:{S('16px','10px')};padding-top:{S('18px','12px')}}}
.tot .k{{font-weight:900;font-size:{S('44px','38px')}}}
.tot .v{{font-weight:900;font-size:{S('96px','72px')};line-height:1;color:#0B1F6B;direction:ltr}} .tot .v small{{font-size:.4em;margin-left:8px}}
.stamp{{position:absolute;left:{S('300px','270px')};bottom:{S('22px','10px')};transform:rotate(-10deg);border:9px solid #22a85a;color:#22a85a;border-radius:20px;padding:4px 26px 10px;text-align:center;
  font-weight:900;font-size:{S('70px','54px')};line-height:1.05;background:rgba(255,255,255,.85);box-shadow:0 0 0 4px rgba(34,168,90,.25) inset;opacity:.95}}
.stamp small{{display:block;font-size:.36em;font-weight:900}}
.slogan{{display:flex;justify-content:center;align-items:center;gap:16px;font-weight:900;font-size:{S('46px','38px')}}}
.slogan .no{{color:#9ec2ff;text-decoration:line-through;text-decoration-color:#e5484d;text-decoration-thickness:6px}}
.slogan .yes{{background:#FFC30A;color:#0B1F6B;padding:0 18px 4px;border-radius:14px}}
.facts{{display:flex;justify-content:center;gap:10px;flex-wrap:wrap;font-weight:800;font-size:{S('32px','27px')}}}
.facts span{{background:rgba(255,255,255,.1);border:2px solid rgba(255,255,255,.2);border-radius:999px;padding:4px 20px}}
.facts b{{color:#FFC30A}}
.offer{{display:flex;align-items:center;gap:22px;background:#fff;color:#0B1F6B;border-radius:28px;padding:{S('22px 26px','14px 20px')}}}
.offer .t{{flex:1;display:grid;gap:2px}}
.offer .l1{{font-weight:900;font-size:{S('48px','40px')};line-height:1.2}}
.offer .l1 em{{font-style:normal;background:#22a85a;color:#fff;padding:0 14px;border-radius:12px}}
.chip{{display:inline-block;direction:ltr;background:#0061FA;color:#fff;border-radius:10px;padding:0 12px;letter-spacing:2px}}
.offer .l2{{font-weight:800;font-size:{S('28px','24px')};color:#3c4a6e}}
.qrw{{flex:none;display:grid;justify-items:center;gap:2px}}
.qr{{width:{S('196px','148px')};height:{S('196px','148px')};padding:8px;border:5px solid #0061FA;border-radius:16px}} .qr svg{{width:100%;height:100%;display:block}} .qr svg path{{fill:#0B1F6B}}
.qrw span{{font-weight:900;font-size:21px;color:#0061FA}}
</style></head><body><div class="grain"></div><div class="wrap">
<div class="top"><div class="brand"><span class="m">{mark}</span>دروس Dose</div><div class="tag">البرمجة · تانية ثانوي بكالوريا</div></div>
<div class="h1">فاتورة <span class="y">الدرس الخصوصي</span><br>اتلغت 🧾</div>
{S('<div class="sub">ادفع بس للجزء اللي واقف معاك</div>','')}
<div class="stage"><div class="rc">
  <div class="rh"><b>🧾 حساب مادة البرمجة</b></div>
  {lines_html()}
  <div class="tot"><span class="k">الإجمالي</span><span class="v">0<small>جنيه</small></span></div>
  <div class="stamp">ببلاش<small>لأول 15 طالب</small></div>
</div></div>
<div class="slogan"><span class="no">دروس خصوصية</span><span>❌</span><span class="yes">دروس Dose ✅</span></div>
<div class="facts"><span>📅 <b>السبت 21 نوفمبر</b></span><span>⏰ <b>4 العصر</b></span><span>💻 أونلاين</span>{S('<span>👨‍💻 مع ' + TEACHER + '</span>','')}</div>
<div class="offer"><div class="t"><div class="l1"><em>ببلاش</em> لأول 15 طالب</div>
<div class="l2">{S('«تطبيقات الويب» · ','مع ' + TEACHER + ' · ')}بالكود <span class="chip">CODE15</span> · بعدهم 50 جنيه</div></div>
<div class="qrw"><div class="qr">{qr_svg(URL)}</div><span>امسح واحجز</span></div></div>
</div></body></html>'''


for name, w, h, story in [('poster-feed', 1080, 1350, False), ('poster-story', 1080, 1920, True)]:
    with open(os.path.join(here, name + '.html'), 'w', encoding='utf-8') as f:
        f.write(page(w, h, story))
print('ok')
