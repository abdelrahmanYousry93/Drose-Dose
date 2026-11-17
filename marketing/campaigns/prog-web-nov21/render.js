// node render.js -> poster-feed.png (1080x1350), poster-story.png (1080x1920)
const { chromium } = require('playwright');
const path = require('path');
(async () => {
  const b = await chromium.launch();
  for (const [name, w, h] of [['poster-feed', 1080, 1350], ['poster-story', 1080, 1920]]) {
    const p = await b.newPage({ viewport: { width: w, height: h } });
    await p.goto('file://' + path.join(__dirname, name + '.html'));
    await p.evaluate(() => document.fonts.ready);
    await p.screenshot({ path: path.join(__dirname, name + '.png') });
  }
  await b.close();
})();
