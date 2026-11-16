// node render.js <html> <pdf> [previewPage]
const { chromium } = require('playwright');
const path = require('path');
const [html = 'bottles.html', pdf = 'dose-bottles-ses26.pdf', page = '0'] = process.argv.slice(2);
(async () => {
  const b = await chromium.launch();
  const p = await b.newPage({ viewport: { width: 794, height: 1123 }, deviceScaleFactor: 2 });
  await p.goto('file://' + path.join(__dirname, html));
  await p.evaluate(() => document.fonts.ready);
  await p.pdf({ path: path.join(__dirname, pdf), format: 'A4', printBackground: true, preferCSSPageSize: true });
  await p.screenshot({ path: path.join(__dirname, 'preview.png'), fullPage: true, clip: { x: 0, y: 1123 * +page, width: 794, height: 1123 } });
  await b.close();
})();
