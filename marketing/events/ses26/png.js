// node png.js -> png/<slug>.png : each bottle alone, transparent background, ~300 dpi
const { chromium } = require('playwright'); const fs = require('fs'); const path = require('path');
(async () => {
  const b = await chromium.launch();
  for (const f of ['bottles.html', 'bottles-2.html']) {
    const p = await b.newPage({ viewport: { width: 900, height: 1300 }, deviceScaleFactor: 3.125 });
    let html = fs.readFileSync(path.join(__dirname, f), 'utf8').replace('</style>',
      '.card{background:none!important;outline:none!important;padding:0!important}body{background:transparent!important}.page{gap:10mm!important}</style>');
    const tmp = path.join(__dirname, '_tmp.html'); fs.writeFileSync(tmp, html);
    await p.goto('file://' + tmp); await p.evaluate(() => document.fonts.ready);
    const cards = await p.$$('.card');
    for (const c of cards) {
      const slug = await c.evaluate((el) => (el.querySelector('svg[class], .qr svg') ? '' : '') + (el.querySelector('.name')?.textContent.trim() ?? ''));
      const i = cards.indexOf(c);
      await c.screenshot({ path: path.join(__dirname, 'png', `${f === 'bottles.html' ? '1' : '2'}-${String(i + 1).padStart(2, '0')}.png`), omitBackground: true });
    }
    fs.unlinkSync(tmp);
  }
  await b.close();
})();
