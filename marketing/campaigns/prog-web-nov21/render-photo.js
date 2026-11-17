// node render-photo.js -> poster-photo-feed.png (1080x1350), poster-photo-story.png (1080x1920)
const { chromium } = require('playwright');
const path = require('path');
(async () => {
  const b = await chromium.launch({ args: ['--allow-file-access-from-files'] });
  for (const [name, w, h] of [['poster-photo-feed', 1080, 1350], ['poster-photo-story', 1080, 1920]]) {
    const p = await b.newPage({ viewport: { width: w, height: h } });
    await p.goto('file://' + path.join(__dirname, name + '.html'));
    await p.evaluate(() => document.fonts.ready);
    await p.waitForTimeout(300);
    await p.screenshot({ path: path.join(__dirname, name + '.png') });
  }
  await b.close();
})();
