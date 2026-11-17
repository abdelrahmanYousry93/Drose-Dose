#!/usr/bin/env python3
"""Programming & AI (2nd secondary, baccalaureate) — Sat 21 Nov 2026, 4 pm. Poster as a code editor + the slogan
«دروس ~خصوصية~ Dose». Writes poster-feed.html (1080x1350) and poster-story.html (1080x1920). Render: node render.js"""
import io, os, qrcode, qrcode.image.svg

here = os.path.dirname(os.path.abspath(__file__))
fonts = os.path.abspath(os.path.join(here, '../../../src/Dose.Web/node_modules/@fontsource/cairo/files'))
mark = open(os.path.join(here, '../../../src/Dose.Web/public/brand/dose-mark.svg'), encoding='utf-8').read()
URL = 'https://drwsdose.com/sessions/01a1f3c0-c4e3-7a20-8000-000000002021?src=prog-poster'


def qr_svg(url):
    q = qrcode.QRCode(error_correction=qrcode.constants.ERROR_CORRECT_M, border=0, image_factory=qrcode.image.svg.SvgPathImage)
    q.add_data(url)
    buf = io.BytesIO()
    q.make_image().save(buf)
    s = buf.getvalue().decode()
    return s[s.index('<svg'):]


# Code lines: (indent, html). Arabic only inside comments / strings so the LTR code reads cleanly.
CODE = [
    (0, '<c># <ar>مادة جديدة ومحدش فاهمها؟</ar> 😬</c>'),
    (0, '<k>if</k> student.<f>stuck_on</f>(<sq>"Web Apps"</sq>):'),
    (1, 'private_tutor = <k>None</k> <c># <ar>من غير درس خصوصي</ar></c>'),
    (1, 'price = <n>0</n>  <c># <ar>ببلاش لأول 15 طالب</ar></c>'),
    (1, 'dose.<f>book</f>(<sq>"<ar>السبت 4 العصر</ar>"</sq>)'),
    (0, ''),
    (0, '<pr>&gt;&gt;&gt;</pr> <o>✓ <ar>فهمتها!</ar></o><cur></cur>'),
]


def code_html():
    rows = []
    for i, (ind, line) in enumerate(CODE, 1):
        rows.append(f'<div class="ln"><span class="no">{i}</span><span class="tx" style="padding-left:{ind * 1.6}em">{line}</span></div>')
    return ''.join(rows)


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
body{{font-family:Cairo,sans-serif;color:#fff;position:relative;
  background:radial-gradient(circle at 15% 10%,rgba(124,58,237,.55),transparent 42%),radial-gradient(circle at 90% 85%,rgba(0,97,250,.5),transparent 45%),#070b1f}}
.dots{{position:absolute;inset:0;background-image:radial-gradient(rgba(255,255,255,.07) 2px,transparent 2px);background-size:36px 36px}}
.wrap{{position:absolute;inset:0;padding:{S('80px 64px 70px','48px 56px 46px')};display:flex;flex-direction:column;gap:{S('34px','20px')}}}
.top{{display:flex;align-items:center;justify-content:space-between}}
.brand{{display:flex;align-items:center;gap:14px;font-weight:900;font-size:38px}}
.brand .m{{width:54px;height:54px;background:#fff;border-radius:14px;padding:6px}} .brand .m svg{{width:100%;height:100%;display:block}}
.tag{{background:rgba(255,255,255,.1);border:2px solid rgba(255,255,255,.25);font-weight:900;font-size:{S('32px','28px')};padding:6px 22px;border-radius:999px}}
.tag b{{color:#2fe08a}}
/* slogan lockup */
.slogan{{text-align:center;line-height:1}}
.slogan .l{{font-weight:900;font-size:{S('104px','100px')};display:inline-flex;align-items:center;gap:18px}}
.strike{{position:relative;color:#7f8bb3}}
.strike::after{{content:"";position:absolute;left:-10px;right:-10px;top:52%;height:{S('14px','12px')};background:#ff4d5e;border-radius:8px;transform:rotate(-7deg);box-shadow:0 0 24px rgba(255,77,94,.6)}}
.dose{{position:relative;color:#FFC30A;direction:ltr;font-size:1.05em;text-shadow:0 0 40px rgba(255,195,10,.45)}}
.dose::before{{content:"✓";position:absolute;top:-.42em;right:-.3em;font-size:.38em;color:#2fe08a}}
.slogan .s{{font-weight:800;font-size:{S('42px','36px')};color:#c9d6ff;margin-top:{S('22px','12px')}}}
.slogan .s b{{color:#fff}}
/* editor */
.ed{{direction:ltr;background:#0d1330;border:2px solid rgba(158,194,255,.25);border-radius:24px;overflow:hidden;box-shadow:0 30px 70px rgba(0,0,0,.45)}}
.bar{{display:flex;align-items:center;gap:10px;padding:{S('16px 22px','12px 20px')};background:#141b3f;border-bottom:2px solid rgba(158,194,255,.15)}}
.bar i{{width:16px;height:16px;border-radius:50%;display:block}}
.bar .file{{margin-left:16px;font:700 {S('26px','22px')} "DejaVu Sans Mono",monospace;color:#9ec2ff;background:#0d1330;padding:4px 16px;border-radius:10px 10px 0 0}}
.code{{padding:{S('26px 22px 30px','14px 18px 18px')};font:600 {S('34px','30px')}/{S('1.85','1.6')} "DejaVu Sans Mono",Cairo,monospace;color:#e6ecff}}
.ln{{display:flex;gap:20px;white-space:pre}} .no{{color:#4b5689;width:1.2em;text-align:right;flex:none}}
.tx{{unicode-bidi:isolate;direction:ltr}}
c{{color:#6f7fb8;font-family:Cairo;font-weight:700}} k{{color:#ff79c6}} f{{color:#50fa7b}} sq{{color:#ffd166}} ar{{font-family:Cairo;font-weight:800;unicode-bidi:isolate;direction:rtl;display:inline-block}} n{{color:#bd93f9}} pr{{color:#2fe08a}}
o{{color:#2fe08a;font-family:Cairo;font-weight:800}}
cur{{display:inline-block;width:.55em;height:1em;background:#FFC30A;vertical-align:-.15em;margin-left:6px}}
/* facts */
.facts{{display:grid;grid-template-columns:repeat(3,1fr);gap:14px}}
.facts div{{background:rgba(255,255,255,.07);border:2px solid rgba(255,255,255,.14);border-radius:20px;padding:{S('16px 8px','10px 8px')};text-align:center}}
.facts b{{display:block;font-size:{S('42px','36px')};font-weight:900;color:#FFC30A;line-height:1.25}}
.facts span{{font-size:{S('26px','23px')};font-weight:700;color:#dbe6ff}}
.topics{{display:flex;flex-wrap:wrap;gap:14px;justify-content:center;align-items:center;font-weight:800;font-size:30px;color:#c9d6ff}} .topics b{{direction:ltr;background:rgba(124,58,237,.3);border:2px solid #7c3aed;color:#fff;border-radius:14px;padding:4px 18px}}
.who{{display:flex;align-items:center;justify-content:center;gap:12px;font-weight:800;font-size:{S('32px','28px')};color:#dbe6ff}}
.who b{{color:#fff}}
.offer{{display:flex;align-items:center;gap:24px;background:#fff;color:#0B1F6B;border-radius:28px;padding:{S('26px 28px','16px 22px')};margin-top:auto}}
.offer .t{{flex:1;display:grid;gap:4px}}
.offer .l1{{font-weight:900;font-size:{S('50px','42px')};line-height:1.2}}
.offer .l1 em{{font-style:normal;background:#FFC30A;padding:0 12px;border-radius:12px}}
.code-chip{{display:inline-block;direction:ltr;background:#7c3aed;color:#fff;border-radius:12px;padding:0 14px;letter-spacing:2px}}
.offer .l2{{font-weight:800;font-size:{S('30px','26px')};color:#3c4a6e}}
.qrw{{flex:none;display:grid;justify-items:center;gap:4px}}
.qr{{width:{S('210px','168px')};height:{S('210px','168px')};padding:10px;border:5px solid #7c3aed;border-radius:18px}} .qr svg{{width:100%;height:100%;display:block}} .qr svg path{{fill:#0B1F6B}}
.qrw span{{font-weight:900;font-size:22px;color:#7c3aed}}
</style></head><body><div class="dots"></div><div class="wrap">
<div class="top"><div class="brand"><span class="m">{mark}</span>دروس Dose</div><div class="tag">تانية ثانوي · <b>بكالوريا</b></div></div>
<div class="slogan"><div class="l"><span>دروس</span><span class="strike">خصوصية</span><span class="dose">Dose</span></div>
<div class="s">من غير درس خصوصي.. <b>ومن غير فلوس</b> 🎁</div></div>
<div class="ed"><div class="bar"><i style="background:#ff5f57"></i><i style="background:#febc2e"></i><i style="background:#28c840"></i><span class="file">web_apps.py</span></div>
<div class="code">{code_html()}</div></div>
{S('<div class="topics"><span>اللي هتفهمه:</span><b>Client / Server</b><b>HTTP</b><b>Request → Response</b><b>APIs</b></div>','')}
<div class="facts"><div><b>السبت</b><span>21 نوفمبر</span></div><div><b>4 العصر</b><span>ساعة ونص</span></div><div><b>أونلاين</b><span>من البيت</span></div></div>
<div class="who">👨‍💻 مع <b>م. عبدالرحمن يسري</b> — مطوّر بيبرمج المواقع دي فعلًا</div>
<div class="offer"><div class="t"><div class="l1"><em>ببلاش</em> لأول 15 طالب</div>
<div class="l2">بالكود <span class="code-chip">CODE15</span> · بعدهم 50 جنيه بس</div></div>
<div class="qrw"><div class="qr">{qr_svg(URL)}</div><span>امسح واحجز</span></div></div>
</div></body></html>'''


for name, w, h, story in [('poster-feed', 1080, 1350, False), ('poster-story', 1080, 1920, True)]:
    with open(os.path.join(here, name + '.html'), 'w', encoding='utf-8') as f:
        f.write(page(w, h, story))
print('ok')
