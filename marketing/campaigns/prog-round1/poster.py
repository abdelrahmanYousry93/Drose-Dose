#!/usr/bin/env python3
"""Programming session Sat 28 Nov 2026, 4 pm Cairo: 10 seats only, free for those 10 with DOFA1 (no "round" wording, owner 24 Nov). No teacher face (owner's choice).
Sells the outcome, the small group and the follow-up. Writes round1-feed.html / round1-story.html; render: node render-any.js round1"""
import io, os, qrcode, qrcode.image.svg

here = os.path.dirname(os.path.abspath(__file__))
fonts = os.path.abspath(os.path.join(here, '../../../src/Dose.Web/node_modules/@fontsource/cairo/files'))
mark = open(os.path.join(here, '../../../src/Dose.Web/public/brand/dose-mark.svg'), encoding='utf-8').read()
URL = 'https://drwsdose.com/sessions/01a1f3c0-c4e3-7a20-8000-000000002801?src=prog10-poster'

FEATS = [('👥', '10 طلاب بس', 'كل طالب يسأل ويتسمع'), ('💻', 'شرح على الكود', 'تشوف الموقع بيشتغل قدامك'),
         ('📝', 'سؤال امتحان', 'في آخر الحصة وتحلّه'), ('🎯', 'جرعة مركّزة', 'ساعة ونص في الدرس ده بس')]


def qr_svg(url):
    q = qrcode.QRCode(error_correction=qrcode.constants.ERROR_CORRECT_M, border=0, image_factory=qrcode.image.svg.SvgPathImage)
    q.add_data(url)
    buf = io.BytesIO()
    q.make_image().save(buf)
    s = buf.getvalue().decode()
    return s[s.index('<svg'):]


def page(w, h, story):
    S = (lambda a, b: a if story else b)
    feats = ''.join(f'<div class="f"><i>{ic}</i><b>{t}</b><span>{d}</span></div>' for ic, t, d in FEATS)
    return f'''<!doctype html><html lang="ar" dir="rtl"><head><meta charset="utf-8"><style>
@font-face{{font-family:Cairo;font-weight:700;src:url("file://{fonts}/cairo-arabic-700-normal.woff2")}}
@font-face{{font-family:Cairo;font-weight:800;src:url("file://{fonts}/cairo-arabic-800-normal.woff2")}}
@font-face{{font-family:Cairo;font-weight:900;src:url("file://{fonts}/cairo-arabic-900-normal.woff2")}}
@font-face{{font-family:Cairo;font-weight:900;src:url("file://{fonts}/cairo-latin-900-normal.woff2");unicode-range:U+0000-00FF}}
*{{box-sizing:border-box;margin:0;padding:0}}
html,body{{width:{w}px;height:{h}px;overflow:hidden}}
body{{font-family:Cairo,sans-serif;color:#fff;position:relative;background:radial-gradient(circle at 85% 8%,rgba(255,195,10,.28),transparent 38%),radial-gradient(circle at 10% 90%,rgba(0,97,250,.55),transparent 45%),#08133d}}
.grid{{position:absolute;inset:0;background-image:linear-gradient(rgba(255,255,255,.04) 2px,transparent 2px),linear-gradient(90deg,rgba(255,255,255,.04) 2px,transparent 2px);background-size:60px 60px}}
.wrap{{position:absolute;inset:0;padding:{S('78px 58px 64px','46px 52px 42px')};display:flex;flex-direction:column;justify-content:space-between}}
.top{{display:flex;align-items:center;justify-content:space-between}}
.brand{{display:flex;align-items:center;gap:12px;font-weight:900;font-size:36px}}
.brand .m{{width:52px;height:52px;background:#fff;border-radius:14px;padding:6px}} .brand .m svg{{width:100%;height:100%;display:block}}
.tag{{background:rgba(255,255,255,.1);border:2px solid rgba(255,255,255,.25);font-weight:900;font-size:{S('28px','24px')};padding:6px 20px;border-radius:999px}}

.subj{{text-align:center;display:grid;gap:6px;justify-items:center}}
.subj .s1{{font-weight:900;font-size:{S('54px','46px')};background:linear-gradient(90deg,#7c3aed,#0061FA);padding:4px 30px 10px;border-radius:20px}}
.subj .s2{{font-weight:900;font-size:{S('36px','30px')};color:#08133d;background:#FFC30A;padding:2px 22px 6px;border-radius:999px}}
.urg{{display:block;text-align:center;line-height:1.5;background:rgba(229,72,77,.15);border:3px dashed #e5484d;border-radius:20px;padding:{S('14px 18px','10px 16px')};font-weight:900;font-size:{S('32px','27px')};color:#ffd3d4}}
.urg b{{color:#fff;background:#e5484d;border-radius:12px;padding:0 12px}}
.seats{{display:flex;align-items:center;gap:10px;background:#e5484d;color:#fff;font-weight:900;font-size:{S('34px','30px')};padding:4px 24px 8px;border-radius:999px;box-shadow:0 12px 30px rgba(229,72,77,.45)}}
.h1{{text-align:center;font-weight:900;font-size:{S('84px','60px')};line-height:1.15}}
.h1 .y{{color:#FFC30A}}
.sub{{text-align:center;font-weight:800;font-size:{S('36px','30px')};color:#c9d6ff;margin-top:6px}}
.feats{{display:grid;grid-template-columns:1fr 1fr;gap:{S('18px','14px')}}}
.f{{background:rgba(255,255,255,.07);border:2px solid rgba(255,255,255,.14);border-radius:24px;padding:{S('18px','10px 16px')};display:grid;gap:0}}
.f i{{font-style:normal;font-size:{S('44px','30px')};line-height:1}}
.f b{{font-weight:900;font-size:{S('34px','29px')};color:#FFC30A}}
.f span{{font-weight:800;font-size:{S('26px','22px')};color:#dbe6ff}}
.facts{{display:flex;justify-content:center;gap:10px;flex-wrap:wrap;font-weight:800;font-size:{S('31px','26px')}}}
.facts span{{background:rgba(255,255,255,.1);border:2px solid rgba(255,255,255,.2);border-radius:999px;padding:4px 18px}} .facts b{{color:#FFC30A}}
.offer{{display:flex;align-items:center;gap:22px;background:#fff;color:#0B1F6B;border-radius:26px;padding:{S('22px 26px','14px 20px')}}}
.offer .t{{flex:1;display:grid;gap:2px}}
.offer .l1{{font-weight:900;font-size:{S('48px','40px')};line-height:1.2}}
.offer .l1 em{{font-style:normal;background:#22a85a;color:#fff;padding:0 14px;border-radius:12px}}
.offer .l2{{font-weight:800;font-size:{S('28px','24px')};color:#3c4a6e}}
.chip{{display:inline-block;direction:ltr;background:#0061FA;color:#fff;border-radius:10px;padding:0 12px;letter-spacing:2px}}
.qr{{flex:none;width:{S('190px','146px')};height:{S('190px','146px')};padding:8px;border:5px solid #0061FA;border-radius:16px}} .qr svg{{width:100%;height:100%;display:block}} .qr svg path{{fill:#0B1F6B}}
</style></head><body><div class="grid"></div><div class="wrap">
<div class="top"><div class="brand"><span class="m">{mark}</span>دروس Dose</div><div class="seats">🔥 10 مقاعد بس</div></div>
<div class="subj"><div class="s1">💻 البرمجة والذكاء الاصطناعي</div><div class="s2">🎓 تانية ثانوي بكالوريا</div></div>
<div><div class="h1">في ساعة ونص<br><span class="y">هتفهم الموقع بيشتغل إزاي</span></div>
<div class="sub">Client · Server · HTTP · API — وتحل أي سؤال عليهم</div></div>
<div class="feats">{feats}</div>
<div class="facts"><span>📅 <b>السبت 28 نوفمبر</b></span><span>⏰ <b>4 العصر</b></span><span>💻 أونلاين</span></div>
<div class="urg">⏳ الحجز بيقفل أول ما <b>10 طلاب</b> يحجزوا.. ومفيش مقاعد زيادة</div>
<div class="offer"><div class="t"><div class="l1"><em>ببلاش</em> لأول 10 طلاب بس</div>
<div class="l2">احجز بالكود <span class="chip">DOFA1</span> · امسح الكود أو ابعتلنا رسالة</div></div>
<div class="qr">{qr_svg(URL)}</div></div>
</div></body></html>'''


for name, w, h, story in [('round1-feed', 1080, 1350, False), ('round1-story', 1080, 1920, True)]:
    with open(os.path.join(here, name + '.html'), 'w', encoding='utf-8') as f:
        f.write(page(w, h, story))
print('ok')
