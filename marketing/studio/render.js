#!/usr/bin/env node
// Dose Studio renderer: turns a week spec (JSON) into ready-to-post PNGs and MP4 reels in the Dose brand.
//
//   node marketing/studio/render.js marketing/weeks/2026-W2/spec.json [outDir]
//
// Spec shape (see marketing/agency/designer.md for the full guide):
// {
//   "pieces": [
//     { "file": "w2-post.png", "size": "post"|"story"|"square", "bg": "light"|"blue"|"navy",
//       "tag": "sec"|"prep"|"both"|"parents"|null, "tagText": "ثانوي",
//       "logo": "top"|"center"|"none", "counter": "2 / 5",
//       "body": "<div class='h1' style='font-size:90px'>…</div>",   // HTML using the classes in CSS below
//       "footer": "اسحب ←", "sticker": "مكان ملصق الاستطلاع" },
//     { "file": "w2-reel.mp4", "reel": true, "secondsPerScene": 3,
//       "scenes": [ { "bg": "navy", "body": "<div class='h1'>…</div>" }, … ] }
//   ]
// }
// Requires Playwright (preinstalled) and ffmpeg for reels. Fonts: @fontsource/cairo (installed on first run).
const fs = require('fs');
const path = require('path');
const { execFileSync } = require('child_process');

const [specPath, outArg] = process.argv.slice(2);
if (!specPath) { console.error('usage: render.js spec.json [outDir]'); process.exit(1); }
const spec = JSON.parse(fs.readFileSync(specPath, 'utf8'));
const out = path.resolve(outArg || path.join(path.dirname(specPath), 'out'));
fs.mkdirSync(out, { recursive: true });

function fontDir() {
  const candidates = [
    path.join(__dirname, 'node_modules/@fontsource/cairo/files'),
    path.join(__dirname, '../../src/Dose.Web/node_modules/@fontsource/cairo/files'),
  ];
  for (const c of candidates) if (fs.existsSync(c)) return c;
  execFileSync('npm', ['install', '--no-audit', '--no-fund', '--silent'], { cwd: __dirname, stdio: 'inherit' });
  return candidates[0];
}
const FONTS = fontDir();
const font = (f) => fs.readFileSync(path.join(FONTS, f)).toString('base64');
const face = (w) => `
@font-face { font-family: Cairo; font-weight: ${w}; src: url(data:font/woff2;base64,${font(`cairo-arabic-${w}-normal.woff2`)}) format('woff2'); unicode-range: U+0600-06FF, U+0750-077F, U+FB50-FDFF, U+FE70-FEFF, U+200C-200F; }
@font-face { font-family: Cairo; font-weight: ${w}; src: url(data:font/woff2;base64,${font(`cairo-latin-${w}-normal.woff2`)}) format('woff2'); unicode-range: U+0000-00FF, U+2010-2027, U+2190-21FF, U+2713; }`;

// Brand: primary #0061FA, accent #FFC30A, navy #0B1F6B, light blue #EAF2FF, Cairo.
const CSS = `${face(600)}${face(700)}${face(800)}
* { margin: 0; box-sizing: border-box; }
body { font-family: Cairo, sans-serif; direction: rtl; color: #0B1F6B; }
.frame { position: relative; overflow: hidden; display: flex; flex-direction: column; gap: 40px; }
.bg-light { background: linear-gradient(160deg, #eaf2ff 0%, #ffffff 60%, #fff6d6 100%); }
.bg-blue { background: linear-gradient(160deg, #0061FA 0%, #0B3FB0 100%); color: #fff; }
.bg-navy { background: #0B1F6B; color: #fff; }
.bg-yellow { background: #FFC30A; color: #0B1F6B; }
.blob1 { position: absolute; width: 620px; height: 620px; border-radius: 50%; background: #0061FA; opacity: .07; top: -220px; left: -200px; }
.blob2 { position: absolute; width: 520px; height: 520px; border-radius: 50%; background: #FFC30A; opacity: .16; bottom: -220px; right: -160px; }
.bg-blue .blob1, .bg-navy .blob1 { background: #fff; opacity: .06; }
.top { display: flex; justify-content: space-between; align-items: center; position: relative; z-index: 1; }
.main { flex: 1; display: grid; align-content: center; gap: 34px; position: relative; z-index: 1; }
.center { justify-items: center; text-align: center; }
.tag { display: inline-flex; align-items: center; padding: 10px 26px; border-radius: 999px; font-weight: 700; font-size: 30px; }
.tag-sec { background: #0061FA; color: #fff; } .tag-prep { background: #FFC30A; color: #0B1F6B; }
.tag-both { background: #0B1F6B; color: #fff; } .tag-parents { background: #12B76A; color: #fff; }
.bg-navy .tag-both, .bg-blue .tag-both { background: #fff; color: #0B1F6B; }
.logo { display: flex; align-items: center; gap: 12px; direction: ltr; }
.latin { color: #0061FA; font-weight: 800; letter-spacing: -0.02em; } .latin .o { color: #FFC30A; }
.ar-word { color: #0B1F6B; font-weight: 800; }
.on-dark .latin, .on-dark .ar-word { color: #fff; }
.bg-yellow .latin, .bg-yellow .latin .o { color: #0B1F6B; }
.h1 { font-weight: 800; line-height: 1.3; font-size: 88px; }
.h2 { font-weight: 800; line-height: 1.35; font-size: 64px; }
.lead { font-weight: 700; font-size: 44px; line-height: 1.5; opacity: .9; }
.small { font-weight: 600; font-size: 32px; line-height: 1.5; opacity: .8; }
.accent { color: #0061FA; } .yellow { color: #FFC30A; } .muted { color: #475467; }
.bg-blue .accent, .bg-navy .accent { color: #FFC30A; }
.num { width: 120px; height: 120px; border-radius: 50%; background: #0061FA; color: #fff; display: grid; place-items: center; font-weight: 800; font-size: 64px; }
.checks { display: grid; gap: 22px; font-size: 44px; font-weight: 700; }
.pill { display: inline-block; background: #FFC30A; color: #0B1F6B; border-radius: 999px; padding: 22px 54px; font-weight: 800; font-size: 48px; }
.box { background: #fff; color: #0B1F6B; border-radius: 36px; padding: 36px 40px; box-shadow: 0 20px 50px rgba(11,31,107,.12); }
.bg-blue .box, .bg-navy .box { box-shadow: none; }
.row { display: flex; gap: 24px; align-items: center; }
.cols { display: grid; grid-template-columns: 1fr 1fr; gap: 24px; }
.sticker { border: 4px dashed rgba(11,31,107,.25); border-radius: 36px; display: grid; place-items: center; color: rgba(11,31,107,.45); font-weight: 700; font-size: 30px; min-height: 420px; position: relative; z-index: 1; }
.on-dark .sticker { border-color: rgba(255,255,255,.35); color: rgba(255,255,255,.6); }
.footer { font-weight: 700; font-size: 30px; color: #475467; position: relative; z-index: 1; }
.on-dark .footer { color: rgba(255,255,255,.8); }
.ltr { direction: ltr; unicode-bidi: isolate; display: inline-block; }
`;
const mark = (size, dark) => `<svg viewBox="0 0 64 64" width="${size}" height="${size}"><g transform="rotate(-38 32 38)"><rect x="10" y="28" width="44" height="20" rx="10" fill="#0061FA"/><path d="M32 28h12a10 10 0 0 1 0 20H32z" fill="#FFC30A"/><rect x="15" y="32" width="10" height="4" rx="2" fill="#fff" opacity=".55"/></g><path d="M14 15 33 7l19 8-19 8z" fill="${dark ? '#fff' : '#0B1F6B'}"/><path d="M22 18.5v6c0 2.5 5 4.5 11 4.5s11-2 11-4.5v-6l-11 4.6z" fill="${dark ? '#fff' : '#0B1F6B'}"/><path d="M50 16v8.5" stroke="${dark ? '#fff' : '#0B1F6B'}" stroke-width="2" stroke-linecap="round"/><circle cx="50" cy="26" r="2.2" fill="#FFC30A"/></svg>`;
const logo = (s, dark) => `<div class="logo">${mark(s * 1.25, dark)}<span class="latin" style="font-size:${s}px">D<span class="o">o</span>se</span><span class="ar-word" style="font-size:${s * 0.7}px">دروس</span></div>`;

const SIZES = { post: [1080, 1350], story: [1080, 1920], square: [1080, 1080] };
const TAGS = { sec: ['tag-sec', 'ثانوي'], prep: ['tag-prep', 'تالتة إعدادي'], both: ['tag-both', 'ثانوي · تالتة إعدادي'], parents: ['tag-parents', 'لأولياء الأمور'] };

function frame(p, [w, h]) {
  const bg = p.bg || 'light';
  const dark = bg === 'blue' || bg === 'navy';
  const pad = h > 1400 ? '150px 80px 140px' : '90px 80px';
  const logoPos = p.logo || 'top';
  const tag = p.tag && TAGS[p.tag] ? `<span class="tag ${TAGS[p.tag][0]}">${p.tagText || TAGS[p.tag][1]}</span>` : '';
  const right = p.counter ? `<span class="footer ltr">${p.counter}</span>` : tag;
  const top = logoPos === 'top' || right ? `<div class="top">${logoPos === 'top' ? logo(h > 1400 ? 56 : 50, dark) : '<span></span>'}${right}</div>` : '';
  const body = `<div class="main ${logoPos === 'center' || p.center ? 'center' : ''}">${logoPos === 'center' ? logo(80, dark) : ''}${p.body || ''}</div>`;
  const sticker = p.sticker ? `<div class="sticker">${p.sticker}</div>` : '';
  const footer = p.footer ? `<div class="footer">${p.footer}</div>` : '';
  return `<div class="frame bg-${bg} ${dark ? 'on-dark' : ''}" style="width:${w}px;height:${h}px;padding:${pad}">
    <div class="blob1"></div><div class="blob2"></div>${top}${body}${sticker}${footer}</div>`;
}

(async () => {
  const { chromium } = require('playwright');
  const browser = await chromium.launch();
  const page = await browser.newPage();
  const shot = async (html, [w, h], file) => {
    await page.setViewportSize({ width: w, height: h });
    await page.setContent(`<html lang="ar"><head><style>${CSS}</style></head><body>${html}</body></html>`);
    await page.evaluate(() => document.fonts.ready);
    await page.screenshot({ path: file });
  };
  const made = [];
  for (const p of spec.pieces) {
    if (p.reel) {
      const tmp = fs.mkdtempSync(path.join(out, '.reel-'));
      const frames = [];
      for (const [i, sc] of p.scenes.entries()) {
        const f = path.join(tmp, `s${i}.png`);
        await shot(frame({ logo: 'none', center: true, ...sc }, SIZES.story), SIZES.story, f);
        frames.push(f);
      }
      const d = p.secondsPerScene || 3, x = 0.4;
      const args = ['-y', '-loglevel', 'error'];
      for (const f of frames) args.push('-loop', '1', '-t', String(d + x), '-framerate', '30', '-i', f);
      let chain = '', last = '0';
      for (let i = 1; i < frames.length; i++) {
        const label = i === frames.length - 1 ? 'v' : `a${i}`;
        chain += `[${last}][${i}]xfade=transition=${i === frames.length - 1 ? 'fade' : 'slideleft'}:duration=${x}:offset=${(d * i).toFixed(2)}${i === frames.length - 1 ? ',format=yuv420p' : ''}[${label}];`;
        last = label;
      }
      if (frames.length === 1) chain = '[0]format=yuv420p[v];';
      args.push('-filter_complex', chain.replace(/;$/, ''), '-map', '[v]', '-t', String(d * frames.length),
        '-c:v', 'libx264', '-preset', 'veryfast', '-crf', '20', '-movflags', '+faststart', path.join(out, p.file));
      execFileSync('ffmpeg', args, { stdio: 'inherit' });
      fs.rmSync(tmp, { recursive: true, force: true });
    } else {
      const size = SIZES[p.size || 'post'];
      await shot(frame(p, size), size, path.join(out, p.file));
    }
    made.push(path.join(out, p.file));
  }
  await browser.close();
  console.log(made.join('\n'));
})().catch((e) => { console.error(e); process.exit(1); });
