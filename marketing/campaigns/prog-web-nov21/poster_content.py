#!/usr/bin/env python3
"""Programming session poster focused on the session content (no people): Client ⇄ Server flow with Request / Response,
plus what the student will understand. Writes poster-content-feed.html / poster-content-story.html; render:
node render-any.js poster-content"""
import io, os, qrcode, qrcode.image.svg

here = os.path.dirname(os.path.abspath(__file__))
fonts = os.path.abspath(os.path.join(here, '../../../src/Dose.Web/node_modules/@fontsource/cairo/files'))
mark = open(os.path.join(here, '../../../src/Dose.Web/public/brand/dose-mark.svg'), encoding='utf-8').read()
URL = 'https://drwsdose.com/sessions/01a1f3c0-c4e3-7a20-8000-000000002021?src=prog-poster'
TEACHER = 'عبدالرحمن كامل'

LEARN = ['يعني إيه Client و Server', 'رحلة الـ Request والـ Response', 'الـ HTTP: يعني إيه GET و POST', 'الـ API بتربط الموقع بالسيرفر']


def qr_svg(url):
    q = qrcode.QRCode(error_correction=qrcode.constants.ERROR_CORRECT_M, border=0, image_factory=qrcode.image.svg.SvgPathImage)
    q.add_data(url)
    buf = io.BytesIO()
    q.make_image().save(buf)
    s = buf.getvalue().decode()
    return s[s.index('<svg'):]


def page(w, h, story):
    S = (lambda a, b: a if story else b)
    learn = ''.join(f'<li><i>✓</i><span>{x}</span></li>' for x in LEARN)
    return f'''<!doctype html><html lang="ar" dir="rtl"><head><meta charset="utf-8"><style>
@font-face{{font-family:Cairo;font-weight:700;src:url("file://{fonts}/cairo-arabic-700-normal.woff2")}}
@font-face{{font-family:Cairo;font-weight:800;src:url("file://{fonts}/cairo-arabic-800-normal.woff2")}}
@font-face{{font-family:Cairo;font-weight:900;src:url("file://{fonts}/cairo-arabic-900-normal.woff2")}}
@font-face{{font-family:Cairo;font-weight:800;src:url("file://{fonts}/cairo-latin-800-normal.woff2");unicode-range:U+0000-00FF,U+2190-21FF}}
@font-face{{font-family:Cairo;font-weight:900;src:url("file://{fonts}/cairo-latin-900-normal.woff2");unicode-range:U+0000-00FF,U+2190-21FF}}
*{{box-sizing:border-box;margin:0;padding:0}}
html,body{{width:{w}px;height:{h}px;overflow:hidden}}
body{{font-family:Cairo,sans-serif;color:#fff;position:relative;background:radial-gradient(circle at 20% 12%,rgba(124,58,237,.55),transparent 40%),radial-gradient(circle at 85% 70%,rgba(0,97,250,.5),transparent 45%),#070b1f}}
.grid{{position:absolute;inset:0;background-image:linear-gradient(rgba(255,255,255,.045) 2px,transparent 2px),linear-gradient(90deg,rgba(255,255,255,.045) 2px,transparent 2px);background-size:60px 60px}}
.wrap{{position:absolute;inset:0;padding:{S('76px 58px 64px','44px 52px 42px')};display:flex;flex-direction:column;justify-content:space-between}}
.top{{display:flex;align-items:center;justify-content:space-between}}
.brand{{display:flex;align-items:center;gap:12px;font-weight:900;font-size:36px}}
.brand .m{{width:52px;height:52px;background:#fff;border-radius:14px;padding:6px}} .brand .m svg{{width:100%;height:100%;display:block}}
.tag{{background:rgba(255,255,255,.1);border:2px solid rgba(255,255,255,.25);font-weight:900;font-size:{S('29px','25px')};padding:6px 20px;border-radius:999px}} .tag b{{color:#2fe08a}}
.kick{{font-weight:800;font-size:{S('36px','30px')};color:#9ec2ff;text-align:center}}
.h1{{text-align:center;font-weight:900;font-size:{S('110px','92px')};line-height:1.05;margin-top:-10px}}
.h1 .y{{color:#FFC30A}}
/* flow diagram */
.flow{{direction:ltr;display:grid;grid-template-columns:1fr 1.25fr 1fr;align-items:center;gap:10px;margin:{S('14px 0','4px 0')}}}
.node{{background:#0d1330;border:3px solid rgba(158,194,255,.4);border-radius:26px;padding:{S('22px 10px','14px 8px')};text-align:center;box-shadow:0 20px 50px rgba(0,0,0,.4)}}
.node .ic{{font-size:{S('86px','70px')};line-height:1}}
.node b{{display:block;font-weight:900;font-size:{S('40px','34px')};margin-top:6px}}
.node span{{display:block;font-weight:800;font-size:{S('26px','22px')};color:#9ec2ff;direction:rtl}}
.node.c{{border-color:#7c3aed}} .node.s{{border-color:#0061FA}}
.arrows{{display:grid;gap:{S('22px','14px')}}}
.ar{{position:relative;height:{S('58px','50px')};display:flex;align-items:center;justify-content:center;font-weight:900;font-size:{S('28px','24px')};border-radius:999px}}
.ar.req{{background:#7c3aed}} .ar.res{{background:#2fb36b}}
.ar.req::after{{content:"";position:absolute;right:-22px;border:16px solid transparent;border-left:24px solid #7c3aed}}
.ar.res::after{{content:"";position:absolute;left:-22px;border:16px solid transparent;border-right:24px solid #2fb36b}}
.ar small{{font-weight:800;opacity:.85;margin-left:8px;font-size:.8em}}
.learn{{background:rgba(255,255,255,.06);border:2px solid rgba(255,255,255,.14);border-radius:26px;padding:{S('22px 28px','14px 24px')}}}
.learn h3{{font-weight:900;font-size:{S('36px','30px')};color:#FFC30A;margin-bottom:{S('8px','4px')}}}
.learn ul{{list-style:none;display:grid;grid-template-columns:{S('1fr','1fr 1fr')};gap:{S('10px','6px 18px')}}}
.learn li{{display:flex;align-items:center;gap:12px;font-weight:800;font-size:{S('32px','25px')}}}
.learn li i{{font-style:normal;flex:none;width:{S('40px','32px')};height:{S('40px','32px')};border-radius:50%;background:#2fb36b;display:grid;place-items:center;font-size:.8em}}
.slogan{{display:flex;justify-content:center;align-items:center;gap:16px;font-weight:900;font-size:{S('46px','40px')}}}
.slogan .no{{color:#9ec2ff;text-decoration:line-through;text-decoration-color:#e5484d;text-decoration-thickness:6px}}
.slogan .yes{{background:#FFC30A;color:#0B1F6B;padding:0 18px 4px;border-radius:14px}}
.facts{{display:flex;justify-content:center;gap:10px;flex-wrap:wrap;font-weight:800;font-size:{S('31px','26px')}}}
.facts span{{background:rgba(255,255,255,.1);border:2px solid rgba(255,255,255,.2);border-radius:999px;padding:4px 18px}} .facts b{{color:#FFC30A}}
.offer{{display:flex;align-items:center;gap:22px;background:#fff;color:#0B1F6B;border-radius:26px;padding:{S('22px 26px','14px 20px')}}}
.offer .t{{flex:1;display:grid;gap:2px}}
.offer .l1{{font-weight:900;font-size:{S('48px','40px')};line-height:1.2}}
.offer .l1 em{{font-style:normal;background:#2fb36b;color:#fff;padding:0 14px;border-radius:12px}}
.chip{{display:inline-block;direction:ltr;background:#7c3aed;color:#fff;border-radius:10px;padding:0 12px;letter-spacing:2px}}
.offer .l2{{font-weight:800;font-size:{S('28px','24px')};color:#3c4a6e}}
.qrw{{flex:none;display:grid;justify-items:center;gap:2px}}
.qr{{width:{S('196px','148px')};height:{S('196px','148px')};padding:8px;border:5px solid #7c3aed;border-radius:16px}} .qr svg{{width:100%;height:100%;display:block}} .qr svg path{{fill:#0B1F6B}}
.qrw span{{font-weight:900;font-size:21px;color:#7c3aed}}
</style></head><body><div class="grid"></div><div class="wrap">
<div class="top"><div class="brand"><span class="m">{mark}</span>دروس Dose</div><div class="tag">البرمجة · تانية ثانوي <b>بكالوريا</b></div></div>
<div><div class="kick">تطبيقات الويب في حصة واحدة</div>
<div class="h1">الموقع بيشتغل <span class="y">إزاي؟</span></div></div>
<div class="flow">
  <div class="node c"><div class="ic">📱</div><b>Client</b><span>الموبايل / المتصفح</span></div>
  <div class="arrows"><div class="ar req">Request <small>GET</small></div><div class="ar res">Response <small>200 OK</small></div></div>
  <div class="node s"><div class="ic">🖥️</div><b>Server</b><span>السيرفر</span></div>
</div>
<div class="learn"><h3>هتخرج من الحصة فاهم:</h3><ul>{learn}</ul></div>
<div class="slogan"><span class="no">دروس خصوصية</span><span>❌</span><span class="yes">دروس Dose ✅</span></div>
<div class="facts"><span>📅 <b>السبت 21 نوفمبر</b></span><span>⏰ <b>4 العصر</b></span><span>💻 أونلاين · ساعة ونص</span></div>
<div class="offer"><div class="t"><div class="l1"><em>ببلاش</em> لأول 15 طالب</div>
<div class="l2">مع {TEACHER} · بالكود <span class="chip">CODE15</span> · بعدهم 50 جنيه</div></div>
<div class="qrw"><div class="qr">{qr_svg(URL)}</div><span>امسح واحجز</span></div></div>
</div></body></html>'''


for name, w, h, story in [('poster-content-feed', 1080, 1350, False), ('poster-content-story', 1080, 1920, True)]:
    with open(os.path.join(here, name + '.html'), 'w', encoding='utf-8') as f:
        f.write(page(w, h, story))
print('ok')
