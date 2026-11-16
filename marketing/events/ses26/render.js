// node render.js -> dose-bottles-ses26.pdf + preview PNG of page 1
const { chromium } = require('playwright');
const path = require('path');
(async () => {
  const b = await chromium.launch();
  const p = await b.newPage({ viewport: { width: 794, height: 1123 }, deviceScaleFactor: 2 });
  await p.goto('file://' + path.join(__dirname, 'bottles.html'));
  await p.evaluate(() => document.fonts.ready);
  await p.pdf({ path: path.join(__dirname, 'dose-bottles-ses26.pdf'), format: 'A4', printBackground: true, preferCSSPageSize: true });
  await p.screenshot({ path: path.join(__dirname, 'preview.png'), clip: { x: 0, y: 1123, width: 794, height: 1123 }, fullPage: true });
  await b.close();
})();
