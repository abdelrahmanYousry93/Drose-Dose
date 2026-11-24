// node render-any.js <prefix>  -> <prefix>-feed.png (1080x1350), <prefix>-story.png (1080x1920)
const { chromium } = require('playwright');
const path = require('path');
const prefix = process.argv[2];
(async () => {
  const b = await chromium.launch();
  for (const [name, w, h] of [[prefix + '-feed', 1080, 1350], [prefix + '-story', 1080, 1920]]) {
    const p = await b.newPage({ viewport: { width: w, height: h } });
    await p.goto('file://' + path.join(__dirname, name + '.html'));
    await p.evaluate(() => document.fonts.ready);
    await p.waitForTimeout(300);
    await p.screenshot({ path: path.join(__dirname, name + '.png') });
  }
  await b.close();
})();
