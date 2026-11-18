#!/usr/bin/env python3
"""Brand poster (not tied to a session). Photo version: an excited man fanning cash (stock photo the
owner picked), headline «وفّر فلوس الدرس الخصوصي». No words are put in the man's mouth (he is not a customer).
Writes poster-photo-feed.html / poster-photo-story.html; render with: node render.js photo"""
import io, os, qrcode, qrcode.image.svg

here = os.path.dirname(os.path.abspath(__file__))
fonts = os.path.abspath(os.path.join(here, '../../../src/Dose.Web/node_modules/@fontsource/cairo/files'))
mark = open(os.path.join(here, '../../../src/Dose.Web/public/brand/dose-mark.svg'), encoding='utf-8').read()
URL = 'https://drwsdose.com/?src=brand-photo'
TEACHER = 'عبدالرحمن كامل'
PHOTO = 'file://' + os.path.join(here, '../prog-web-nov21/photo-money.jpg')


def qr_svg(url):
    q = qrcode.QRCode(error_correction=qrcode.constants.ERROR_CORRECT_M, border=0, image_factory=qrcode.image.svg.SvgPathImage)
    q.add_data(url)
    buf = io.BytesIO()
    q.make_image().save(buf)
    s = buf.getvalue().decode()
    return s[s.index('<svg'):]


def page(w, h, story):
    S = (lambda a, b: a if story else b)
    photo_h = S(1180, 820)
    return f'''<!doctype html><html lang="ar" dir="rtl"><head><meta charset="utf-8"><style>
@font-face{{font-family:Cairo;font-weight:700;src:url("file://{fonts}/cairo-arabic-700-normal.woff2")}}
@font-face{{font-family:Cairo;font-weight:800;src:url("file://{fonts}/cairo-arabic-800-normal.woff2")}}
@font-face{{font-family:Cairo;font-weight:900;src:url("file://{fonts}/cairo-arabic-900-normal.woff2")}}
@font-face{{font-family:Cairo;font-weight:800;src:url("file://{fonts}/cairo-latin-800-normal.woff2");unicode-range:U+0000-00FF}}
@font-face{{font-family:Cairo;font-weight:900;src:url("file://{fonts}/cairo-latin-900-normal.woff2");unicode-range:U+0000-00FF}}
*{{box-sizing:border-box;margin:0;padding:0}}
html,body{{width:{w}px;height:{h}px;overflow:hidden}}
body{{font-family:Cairo,sans-serif;color:#fff;position:relative;background:#0B1F6B}}
.ph{{position:absolute;left:0;right:0;top:0;height:{photo_h}px;background:url("{PHOTO}") {S('62% 30%','58% 26%')}/cover no-repeat}}
.ph::after{{content:"";position:absolute;inset:0;background:linear-gradient(180deg,rgba(11,31,107,.55) 0%,rgba(11,31,107,0) 26%,rgba(11,31,107,0) 50%,rgba(11,31,107,.85) 80%,#0B1F6B 100%)}}
.wrap{{position:absolute;inset:0;padding:{S('70px 56px 60px','40px 50px 40px')};display:flex;flex-direction:column}}
.top{{display:flex;align-items:center;justify-content:space-between}}
.brand{{display:flex;align-items:center;gap:12px;font-weight:900;font-size:36px;text-shadow:0 2px 10px rgba(0,0,0,.4)}}
.brand .m{{width:52px;height:52px;background:#fff;border-radius:14px;padding:6px}} .brand .m svg{{width:100%;height:100%;display:block}}
.tag{{background:#FFC30A;color:#0B1F6B;font-weight:900;font-size:{S('30px','26px')};padding:6px 22px;border-radius:999px}}
.h1{{font-weight:900;font-size:{S('84px','66px')};line-height:1.15;text-shadow:0 4px 24px rgba(0,0,0,.55)}}
.h1 .y{{display:inline-block;margin-top:10px;background:#FFC30A;color:#0B1F6B;padding:0 18px 10px;border-radius:18px;text-shadow:none;transform:rotate(-2deg)}}
.spacer{{flex:1}}
.sub{{margin-top:{S('26px','16px')};font-weight:800;font-size:{S('38px','30px')};line-height:1.3;color:#dbe6ff;text-shadow:0 3px 16px rgba(0,0,0,.6)}}
.sub b{{color:#FFC30A}}
.big{{display:grid;gap:{S('10px','6px')};justify-items:start}}
.big .no{{font-weight:900;font-size:{S('78px','62px')};color:#c9d6ff}}
.big .st{{text-decoration:line-through;text-decoration-color:#e5484d;text-decoration-thickness:10px}}
.big .yes{{font-weight:900;font-size:{S('132px','112px')};line-height:1.05;background:#FFC30A;color:#0B1F6B;padding:0 26px 14px;border-radius:26px;transform:rotate(-2deg);box-shadow:0 18px 40px rgba(0,0,0,.35)}}
.big i{{font-style:normal;display:inline-grid;place-items:center;width:1em;height:1em;border-radius:50%;font-size:.55em;vertical-align:middle;color:#fff}}
.big .no i{{background:#e5484d}} .big .yes i{{background:#22a85a}}
.slogan{{margin-top:{S('22px','12px')};display:flex;align-items:center;gap:14px;font-weight:900;font-size:{S('42px','34px')}}}
.slogan .no{{color:#9ec2ff;text-decoration:line-through;text-decoration-color:#e5484d;text-decoration-thickness:6px}}
.slogan .yes{{background:#fff;color:#0B1F6B;padding:0 16px 4px;border-radius:12px}}
.facts{{margin-top:{S('22px','14px')};display:flex;gap:10px;flex-wrap:wrap;font-weight:800;font-size:{S('31px','26px')}}}
.facts span{{background:rgba(255,255,255,.12);border:2px solid rgba(255,255,255,.22);border-radius:999px;padding:4px 18px}}
.facts b{{color:#FFC30A}}
.offer{{margin-top:{S('34px','24px')};display:flex;align-items:center;gap:22px;background:#fff;color:#0B1F6B;border-radius:26px;padding:{S('22px 26px','14px 20px')}}}
.offer .t{{flex:1;display:grid;gap:2px}}
.offer .l1{{font-weight:900;font-size:{S('48px','40px')};line-height:1.2}}
.offer .l1 em{{font-style:normal;background:#22a85a;color:#fff;padding:0 14px;border-radius:12px}}
.chip{{display:inline-block;direction:ltr;background:#0061FA;color:#fff;border-radius:10px;padding:0 12px;letter-spacing:2px}}
.offer .l2{{font-weight:800;font-size:{S('28px','24px')};color:#3c4a6e}}
.qrw{{flex:none;display:grid;justify-items:center;gap:2px}}
.qr{{width:{S('196px','148px')};height:{S('196px','148px')};padding:8px;border:5px solid #0061FA;border-radius:16px}} .qr svg{{width:100%;height:100%;display:block}} .qr svg path{{fill:#0B1F6B}}
.qrw span{{font-weight:900;font-size:21px;color:#0061FA}}
</style></head><body><div class="ph"></div><div class="wrap">
<div class="top"><div class="brand"><span class="m">{mark}</span>دروس Dose</div><div class="tag">إعدادي · ثانوي</div></div>
<div class="spacer"></div>
<div class="big"><div class="no"><span class="st">دروس خصوصية</span> ❌</div><div class="yes">دروس Dose <i>✓</i></div></div>
<div class="sub">جرعة فهم للجزء اللي واقف معاك.. <b>مش اشتراك شهر</b> 💊</div>
<div class="offer"><div class="t"><div class="l1">وفّر فلوس الدرس الخصوصي</div>
<div class="l2">اختار المادة والدرس، واحجز جرعتك على <span class="chip">drwsdose.com</span></div></div>
<div class="qrw"><div class="qr">{qr_svg(URL)}</div><span>امسح واحجز</span></div></div>
</div></body></html>'''


for name, w, h, story in [('poster-brand-feed', 1080, 1350, False), ('poster-brand-story', 1080, 1920, True)]:
    with open(os.path.join(here, name + '.html'), 'w', encoding='utf-8') as f:
        f.write(page(w, h, story))
print('ok')
