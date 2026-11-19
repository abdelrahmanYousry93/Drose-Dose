#!/usr/bin/env python3
"""Brand campaign «جرعة.. مش اشتراك» (Nov 2026), built on the owner's summit pitch: back to the old way — school is the base,
a lesson only for the part you didn't get; responsibility for the student; lighter on parents; share the dose with friends.
Three posters x (feed 1080x1350, story 1080x1920). Render: node render-any.js <name> for name in compare / zaman / friends."""
import io, os, qrcode, qrcode.image.svg

here = os.path.dirname(os.path.abspath(__file__))
fonts = os.path.abspath(os.path.join(here, '../../../src/Dose.Web/node_modules/@fontsource/cairo/files'))
mark = open(os.path.join(here, '../../../src/Dose.Web/public/brand/dose-mark.svg'), encoding='utf-8').read()


def qr_svg(url):
    q = qrcode.QRCode(error_correction=qrcode.constants.ERROR_CORRECT_M, border=0, image_factory=qrcode.image.svg.SvgPathImage)
    q.add_data(url)
    buf = io.BytesIO()
    q.make_image().save(buf)
    s = buf.getvalue().decode()
    return s[s.index('<svg'):]


def shell(w, h, story, body, src, bg):
    S = (lambda a, b: a if story else b)
    url = f'https://drwsdose.com/?src={src}'
    return f'''<!doctype html><html lang="ar" dir="rtl"><head><meta charset="utf-8"><style>
@font-face{{font-family:Cairo;font-weight:700;src:url("file://{fonts}/cairo-arabic-700-normal.woff2")}}
@font-face{{font-family:Cairo;font-weight:800;src:url("file://{fonts}/cairo-arabic-800-normal.woff2")}}
@font-face{{font-family:Cairo;font-weight:900;src:url("file://{fonts}/cairo-arabic-900-normal.woff2")}}
@font-face{{font-family:Cairo;font-weight:900;src:url("file://{fonts}/cairo-latin-900-normal.woff2");unicode-range:U+0000-00FF}}
*{{box-sizing:border-box;margin:0;padding:0}}
html,body{{width:{w}px;height:{h}px;overflow:hidden}}
body{{font-family:Cairo,sans-serif;color:#fff;position:relative;background:{bg}}}
.dots{{position:absolute;inset:0;background-image:radial-gradient(rgba(255,255,255,.07) 2px,transparent 2px);background-size:34px 34px}}
.wrap{{position:absolute;inset:0;padding:{S('80px 60px 66px','48px 56px 44px')};display:flex;flex-direction:column;justify-content:space-between;gap:{S('22px','14px')}}}
.top{{display:flex;align-items:center;justify-content:space-between}}
.brand{{display:flex;align-items:center;gap:12px;font-weight:900;font-size:38px}}
.brand .m{{width:56px;height:56px;background:#fff;border-radius:15px;padding:6px}} .brand .m svg{{width:100%;height:100%;display:block}}
.pill{{background:#FFC30A;color:#0B1F6B;font-weight:900;font-size:{S('30px','26px')};padding:6px 22px;border-radius:999px}}
.y{{color:#FFC30A}}
.cta{{display:flex;align-items:center;gap:22px;background:#fff;color:#0B1F6B;border-radius:26px;padding:{S('22px 28px','14px 22px')}}}
.cta .t{{flex:1;display:grid;gap:2px}}
.cta .l1{{font-weight:900;font-size:{S('46px','38px')};line-height:1.2}}
.cta .l2{{font-weight:800;font-size:{S('28px','24px')};color:#3c4a6e;direction:rtl}}
.qr{{flex:none;width:{S('180px','140px')};height:{S('180px','140px')};padding:8px;border:5px solid #0061FA;border-radius:16px}} .qr svg{{width:100%;height:100%;display:block}} .qr svg path{{fill:#0B1F6B}}
{body[0]}
</style></head><body><div class="dots"></div><div class="wrap">
<div class="top"><div class="brand"><span class="m">{mark}</span>دروس Dose</div><div class="pill">💊 جرعة.. مش اشتراك</div></div>
{body[1]}
<div class="cta"><div class="t"><div class="l1">احجز جرعتك 💊</div><div class="l2">drwsdose.com · امسح الكود</div></div><div class="qr">{qr_svg(url)}</div></div>
</div></body></html>'''


def compare(story):
    S = (lambda a, b: a if story else b)
    css = f'''.h1{{text-align:center;font-weight:900;font-size:{S('96px','80px')};line-height:1.1}}
.sub{{text-align:center;font-weight:800;font-size:{S('38px','32px')};color:#c9d6ff;margin-top:8px}}
.cols{{display:grid;gap:{S('24px','16px')}}}
.box{{border-radius:28px;padding:{S('28px 32px','18px 26px')}}}
.box h3{{font-weight:900;font-size:{S('44px','36px')};margin-bottom:{S('12px','6px')};display:flex;align-items:center;gap:12px}}
.box li{{list-style:none;font-weight:800;font-size:{S('34px','28px')};line-height:1.55;display:flex;gap:12px}}
.old{{background:rgba(255,255,255,.07);border:3px solid rgba(229,72,77,.6)}}
.old h3{{color:#ff8a8d}} .old li{{color:#b8c2de}} .old li::before{{content:"✕";color:#e5484d;font-weight:900}}
.new{{background:#0061FA;border:3px solid #6aa5ff;box-shadow:0 30px 60px rgba(0,0,0,.35)}}
.new li::before{{content:"✓";color:#FFC30A;font-weight:900}}'''
    html = '''<div><div class="h1">هو لازم <span class="y">دروس</span><br>في كل المواد طول السنة؟</div>
<div class="sub">الطالب فاهم أغلب المنهج.. وواقف في حتة</div></div>
<div class="cols">
<div class="box old"><h3>❌ المعتاد</h3><ul><li>اشتراك دروس في كل مادة</li><li>بتدفع كل شهر طول السنة</li><li>حصص في حاجات إنت فاهمها أصلًا</li></ul></div>
<div class="box new"><h3>✅ مع Dose</h3><ul><li>جرعة في الجزء اللي مافهمتوش بس</li><li>تدفع للجرعة.. مش اشتراك</li><li>والمدرسة ترجع هي الأساس</li></ul></div>
</div>'''
    return css, html


def zaman(story):
    S = (lambda a, b: a if story else b)
    css = f'''.kick{{text-align:center;font-weight:800;font-size:{S('40px','34px')};color:#ffd98a}}
.h1{{text-align:center;font-weight:900;font-size:{S('118px','96px')};line-height:1.05;margin-top:6px}}
.quote{{text-align:center;font-weight:900;font-size:{S('48px','40px')};line-height:1.45;background:rgba(255,255,255,.08);border:3px dashed rgba(255,195,10,.6);border-radius:28px;padding:{S('30px 26px','18px 22px')}}}
.quote b{{color:#FFC30A}}
.pts{{display:grid;gap:{S('18px','10px')}}}
.pt{{display:flex;align-items:center;gap:18px;background:#fff;color:#0B1F6B;border-radius:22px;padding:{S('18px 24px','12px 20px')};font-weight:900;font-size:{S('38px','31px')}}}
.pt i{{font-style:normal;flex:none;width:{S('70px','56px')};height:{S('70px','56px')};border-radius:18px;background:#FFF3C4;display:grid;place-items:center;font-size:{S('40px','32px')}}}
.pt small{{display:block;font-weight:800;font-size:.7em;color:#3c4a6e}}'''
    html = '''<div><div class="kick">للأهالي 🤍</div><div class="h1">رجّعنا <span class="y">زمان</span></div></div>
<div class="quote">المدرسة هي <b>الأساس</b>..<br>والدرس <b>لما تحتاجه بس</b></div>
<div class="pts">
<div class="pt"><i>🏫</i><div>بنقوّي دور المدرسة تاني</div></div>
<div class="pt"><i>💪</i><div>بنعلّم ابنك يتحمل مسؤولية مذاكرته<small>هو اللي يحدد الجزء اللي محتاجه</small></div></div>
<div class="pt"><i>🏠</i><div>بنخفف عن البيت<small>جرعة للجزء ده بس.. مش اشتراك طول السنة</small></div></div>
</div>'''
    return css, html


def friends(story):
    S = (lambda a, b: a if story else b)
    css = f'''.h1{{text-align:center;font-weight:900;font-size:{S('100px','82px')};line-height:1.1}}
.sub{{text-align:center;font-weight:800;font-size:{S('38px','32px')};color:#c9d6ff;margin-top:10px}}
.ladder{{display:grid;grid-template-columns:repeat(3,1fr);gap:{S('18px','14px')};align-items:end}}
.step{{border-radius:26px 26px 18px 18px;text-align:center;padding:{S('22px 10px','14px 8px')};display:grid;gap:4px;align-content:end}}
.step .who{{font-size:{S('60px','48px')};line-height:1}}
.step .n{{font-weight:800;font-size:{S('28px','24px')};opacity:.9}}
.step .p{{font-weight:900;font-size:{S('78px','64px')};line-height:1;direction:ltr}} .step .p small{{font-size:.36em;margin-left:6px}}
.s1{{background:rgba(255,255,255,.1);border:3px solid rgba(255,255,255,.25);height:{S('300px','240px')}}}
.s2{{background:#0061FA;height:{S('370px','290px')}}}
.s3{{background:#FFC30A;color:#0B1F6B;height:{S('450px','350px')};box-shadow:0 30px 60px rgba(0,0,0,.35)}}
.note{{text-align:center;font-weight:800;font-size:{S('34px','28px')};line-height:1.5}} .note b{{color:#FFC30A}}'''
    html = '''<div><div class="h1">هات صحابك..<br><span class="y">والجرعة ترخص</span> 👫</div>
<div class="sub">نفس الجرعة، نفس الشرح.. والسعر بينزل كل ما تكتروا</div></div>
<div class="ladder">
<div class="step s1"><div class="who">🧑</div><div class="n">لوحدك أو 2–4</div><div class="p">90<small>ج</small></div></div>
<div class="step s2"><div class="who">👫</div><div class="n">5–9 طلاب</div><div class="p">75<small>ج</small></div></div>
<div class="step s3"><div class="who">👨‍👩‍👧‍👦</div><div class="n">10 وأكتر</div><div class="p">60<small>ج</small></div></div>
</div>
<div class="note">ابعت لينك الجرعة لصحابك.. <b>واحجزوا مع بعض في نفس الجلسة</b></div>'''
    return css, html


THEMES = {
    'compare': (compare, 'radial-gradient(circle at 80% 10%,rgba(0,97,250,.55),transparent 45%),#0B1F6B', 'camp-compare'),
    'zaman': (zaman, 'radial-gradient(circle at 20% 15%,rgba(255,195,10,.25),transparent 40%),radial-gradient(circle at 85% 85%,rgba(0,97,250,.45),transparent 45%),#0a1a5c', 'camp-zaman'),
    'friends': (friends, 'radial-gradient(circle at 50% 30%,rgba(124,58,237,.5),transparent 50%),#0B1F6B', 'camp-friends'),
}
for name, (fn, bg, src) in THEMES.items():
    for kind, w, h, story in [('feed', 1080, 1350, False), ('story', 1080, 1920, True)]:
        with open(os.path.join(here, f'{name}-{kind}.html'), 'w', encoding='utf-8') as f:
            f.write(shell(w, h, story, fn(story), src, bg))
print('ok')
