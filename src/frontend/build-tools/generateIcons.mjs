import { chromium } from '../node_modules/@playwright/test/index.mjs';
import { writeFile } from 'node:fs/promises';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { execSync } from 'node:child_process';

const __dirname = dirname(fileURLToPath(import.meta.url));
const staticDir = join(__dirname, '../static');
const tempDir = join(__dirname, '../.icon-temp');

execSync(`mkdir -p "${tempDir}"`);

function getStandaloneSvg(shape = 'rounded') {
  const radius = shape === 'rounded' ? 112 : 0;

  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512" role="img" aria-label="Culina">
  <!--
    Culina's c-period monogram: an open, continuous gesture that can read as a
    page turning or a spoon moving through a bowl, without drawing either one.
    The period connects the app icon to the punctuation in the wordmark.

    Literal colours are intentional. This file is fetched as an image and does
    not inherit the app stylesheet. Clay moves the identity away from the green
    circular seals common to recipe sites; warm paper keeps the mark at home in
    Culina's interface.
  -->
  <defs>
    <linearGradient id="culina-ground" x1="88" y1="48" x2="430" y2="472" gradientUnits="userSpaceOnUse">
      <stop stop-color="#d77a55" />
      <stop offset="0.52" stop-color="#bf5a3a" />
      <stop offset="1" stop-color="#963c28" />
    </linearGradient>
    <radialGradient id="culina-light" cx="0" cy="0" r="1" gradientTransform="translate(174 112) rotate(51) scale(366 350)" gradientUnits="userSpaceOnUse">
      <stop stop-color="#ffffff" stop-opacity="0.18" />
      <stop offset="0.55" stop-color="#ffffff" stop-opacity="0" />
      <stop offset="1" stop-color="#46180d" stop-opacity="0.18" />
    </radialGradient>
    <filter id="culina-shadow" x="56" y="67" width="404" height="392" filterUnits="userSpaceOnUse" color-interpolation-filters="sRGB">
      <feDropShadow dx="0" dy="8" stdDeviation="8" flood-color="#521d10" flood-opacity="0.18" />
    </filter>
  </defs>

  <rect width="512" height="512" rx="${radius}" fill="url(#culina-ground)" />
  <rect width="512" height="512" rx="${radius}" fill="url(#culina-light)" />
  <path
    d="M348 164C321 132 286 112 245 112C163 112 103 176 103 256C103 336 163 400 245 400C282 400 316 385 340 360"
    fill="none"
    stroke="#fffaf2"
    stroke-width="76"
    stroke-linecap="round"
    filter="url(#culina-shadow)"
  />
  <circle cx="410" cy="367" r="27" fill="#fffaf2" />
  <path d="M128 72C224 26 350 43 429 122" fill="none" stroke="#ffffff" stroke-width="3" stroke-linecap="round" stroke-opacity="0.16" />
</svg>`;
}

const iconSvg = getStandaloneSvg('rounded');
const platformSvg = getStandaloneSvg('full');

// Write icon.svg
await writeFile(join(staticDir, 'icon.svg'), iconSvg, 'utf8');
console.log('✓ Written static/icon.svg');

// Render PNGs via Playwright
const browser = await chromium.launch();

async function renderPng(svgContent, width, height, targetPath) {
  const page = await browser.newPage({ viewport: { width, height }, deviceScaleFactor: 1 });
  const html = `
    <!DOCTYPE html>
    <html>
      <head>
        <style>
          * { margin: 0; padding: 0; box-sizing: border-box; }
          html, body { width: ${width}px; height: ${height}px; overflow: hidden; background: transparent; }
          svg { width: 100%; height: 100%; display: block; }
        </style>
      </head>
      <body>${svgContent}</body>
    </html>
  `;
  await page.setContent(html);
  // Two paint frames keep SVG filter output deterministic in headless Chromium.
  await page.evaluate(
    () => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)))
  );
  await page.screenshot({ path: targetPath, omitBackground: true });
  await page.close();
}

console.log('Rendering raster icons...');
await renderPng(platformSvg, 512, 512, join(staticDir, 'icon-512.png'));
await renderPng(platformSvg, 192, 192, join(staticDir, 'icon-192.png'));
await renderPng(platformSvg, 512, 512, join(staticDir, 'icon-maskable-512.png'));
await renderPng(platformSvg, 180, 180, join(staticDir, 'apple-touch-icon.png'));
await renderPng(iconSvg, 32, 32, join(staticDir, 'favicon-32.png'));

// Render temporary PNGs for multi-resolution favicon.ico
await renderPng(iconSvg, 16, 16, join(tempDir, 'favicon-16.png'));
await renderPng(iconSvg, 32, 32, join(tempDir, 'favicon-32.png'));
await renderPng(iconSvg, 48, 48, join(tempDir, 'favicon-48.png'));

await browser.close();

// Generate favicon.ico using python PIL
execSync(`python3 -c "
from PIL import Image
p16 = Image.open('${tempDir}/favicon-16.png')
p32 = Image.open('${tempDir}/favicon-32.png')
p48 = Image.open('${tempDir}/favicon-48.png')
p32.save('${staticDir}/favicon.ico', format='ICO', sizes=[(16, 16), (32, 32), (48, 48)], append_images=[p16, p48])
"`);

execSync(`rm -rf "${tempDir}"`);

// A screenshot is a 32-bit PNG, and the mark's soft gradients survive a
// 256-colour palette without anything anybody could see: 54 dB against the
// screenshot over both light and dark, no channel off by more than 12 in 255.
// That halves every raster — the 512-pixel icon was 164 kB.
//
// libimagequant, because Pillow's own octree quantiser rings the radial
// gradient visibly at the same palette size. And the opaque icons are
// quantised from RGB, not RGBA: a transparency chunk on a full-bleed icon is
// one iOS paints black behind, and a maskable icon is required to be opaque.
execSync(`python3 -c "
from PIL import Image
for name in ['icon-512', 'icon-192', 'icon-maskable-512', 'apple-touch-icon', 'favicon-32']:
    path = '${staticDir}/' + name + '.png'
    image = Image.open(path).convert('RGBA')
    opaque = image.getextrema()[3][0] == 255
    source = image.convert('RGB') if opaque else image
    source.quantize(colors=256, method=Image.Quantize.LIBIMAGEQUANT).save(path, optimize=True)
"`);

console.log('✓ All PWA and favicon assets successfully generated in static/');
