// node motion/render-motion.js motion/parents-ad.html out.mp4 [seconds=30] [fps=30]
const { chromium } = require('playwright'); const fs = require('fs'); const path = require('path'); const { execFileSync } = require('child_process');
const [src, out, secs = '30', fps = '30'] = process.argv.slice(2);
const fontDir = path.join(__dirname, '../node_modules/@fontsource/cairo/files');
const font = (f) => fs.readFileSync(path.join(fontDir, f)).toString('base64');
const faces = [400, 700, 800, 900].map(w => `@font-face{font-family:Cairo;font-weight:${w};src:url(data:font/woff2;base64,${font(`cairo-arabic-${w}-normal.woff2`)}) format('woff2');unicode-range:U+0600-06FF,U+0750-077F,U+FB50-FDFF,U+FE70-FEFF,U+200C-200F;}
@font-face{font-family:Cairo;font-weight:${w};src:url(data:font/woff2;base64,${font(`cairo-latin-${w}-normal.woff2`)}) format('woff2');unicode-range:U+0000-00FF,U+2010-2027,U+2190-21FF,U+2713,U+27F5;}`).join('\n');
(async () => {
  const html = fs.readFileSync(src, 'utf8').replace('/*FONTS*/', faces);
  const dir = fs.mkdtempSync(path.join(process.env.TMPDIR || '/tmp', 'motion-'));
  const b = await chromium.launch(); const p = await b.newPage({ viewport: { width: 1080, height: 1920 } });
  await p.setContent(html, { waitUntil: 'load' }); await p.evaluate(() => document.fonts.ready);
  const n = Math.round(+secs * +fps);
  for (let i = 0; i < n; i++) {
    await p.evaluate((t) => window.seek(t), i / +fps);
    await p.screenshot({ path: path.join(dir, `f${String(i).padStart(5, '0')}.jpg`), type: 'jpeg', quality: 92 });
  }
  await b.close();
  execFileSync('ffmpeg', ['-loglevel', 'error', '-y', '-framerate', fps, '-i', path.join(dir, 'f%05d.jpg'), '-c:v', 'libx264', '-preset', 'veryfast', '-crf', '20', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', out]);
  fs.rmSync(dir, { recursive: true, force: true });
  console.log('wrote', out, n, 'frames');
})();
