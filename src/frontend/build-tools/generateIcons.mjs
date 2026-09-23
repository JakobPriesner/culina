import { chromium } from '../node_modules/@playwright/test/index.mjs';
import { writeFile } from 'node:fs/promises';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { execSync } from 'node:child_process';

const __dirname = dirname(fileURLToPath(import.meta.url));
const staticDir = join(__dirname, '../static');
const tempDir = join(__dirname, '../.icon-temp');

execSync(`mkdir -p "${tempDir}"`);

const MARK_INNER_SVG = `
  <!-- Steam plumes -->
  <path d="M16 16.5 C14.2 13.2 17.8 10.8 16 7.5 C15.2 6.2 14.5 5.5 14 4.8" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" fill="none" />
  <path d="M23.5 16.5 C21.7 13.2 25.3 10.8 23.5 7.5 C22.7 6.2 22 5.5 21.5 4.8" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" fill="none" />
  <!-- Bowl Rim -->
  <path d="M7 20.5 H33" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" />
  <!-- Bowl Body -->
  <path d="M8.5 20.5 C9.2 27.8 14.2 32 20 32 C25.8 32 30.8 27.8 31.5 20.5" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" fill="none" />
  <!-- Ceramic Foot -->
  <path d="M16 34 H24" stroke="currentColor" stroke-width="2" stroke-linecap="round" />
`;

function getStandaloneSvg(size = 512, shape = 'circle') {
  const isMaskable = shape === 'maskable';
  const targetRatio = isMaskable ? 0.46 : 0.56;
  const scale = (size * targetRatio) / 40;
  const tx = (size - 40 * scale) / 2;
  const ty = (size - 40 * scale) / 2;

  let bgShape = '';
  if (isMaskable) {
    // Full bleed square for Android adaptive icons
    bgShape = `
      <rect width="${size}" height="${size}" fill="url(#culina-grad)" />
      <rect width="${size}" height="${size}" fill="url(#culina-ambient)" />
    `;
  } else if (shape === 'circle') {
    // Circular badge with delicate border ring
    const r = size * 0.46875;
    const c = size / 2;
    bgShape = `
      <circle cx="${c}" cy="${c}" r="${r}" fill="url(#culina-grad)" />
      <circle cx="${c}" cy="${c}" r="${r}" fill="url(#culina-ambient)" />
      <circle cx="${c}" cy="${c}" r="${r - 1}" fill="none" stroke="#4a5f3d" stroke-width="${Math.max(1, size * 0.005)}" stroke-opacity="0.8" />
    `;
  } else if (shape === 'squircle') {
    // iOS apple-touch-icon: full-bleed square with no transparent corners (iOS masks corners)
    bgShape = `
      <rect width="${size}" height="${size}" fill="url(#culina-grad)" />
      <rect width="${size}" height="${size}" fill="url(#culina-ambient)" />
    `;
  }

  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${size} ${size}" role="img" aria-label="Culina">
  <!--
    A bowl with rising steam in the Culina dark olive seal.
    Scalable vector icon used as modern SVG favicon and PWA brand mark.

    Literal colours, not token names: this file is fetched by the browser as an
    image and never sees the app's stylesheet. Ground is the olive 800/900 pair
    (#34432c to #1d2618); the mark is sand 50 (#fdfbf7), the warm porcelain
    paper tone.

    Keep the token names themselves out of this comment. XML forbids a double
    hyphen inside a comment, every one of those names begins with two, and the
    whole document fails to parse over it. That is not a warning anywhere: the
    browser simply has no icon.
  -->
  <defs>
    <linearGradient id="culina-grad" x1="20%" y1="0%" x2="80%" y2="100%">
      <stop offset="0%" stop-color="#34432c" />
      <stop offset="50%" stop-color="#283522" />
      <stop offset="100%" stop-color="#1d2618" />
    </linearGradient>
    <radialGradient id="culina-ambient" cx="50%" cy="25%" r="75%">
      <stop offset="0%" stop-color="#ffffff" stop-opacity="0.12" />
      <stop offset="60%" stop-color="#ffffff" stop-opacity="0" />
      <stop offset="100%" stop-color="#000000" stop-opacity="0.3" />
    </radialGradient>
    <filter id="culina-depth" x="-20%" y="-20%" width="140%" height="140%">
      <feDropShadow dx="0" dy="${Math.max(1, size * 0.006)}" stdDeviation="${Math.max(1, size * 0.008)}" flood-color="#0e140c" flood-opacity="0.5" />
    </filter>
  </defs>

  ${bgShape}

  <g transform="translate(${tx}, ${ty}) scale(${scale})" color="#fdfbf7" filter="url(#culina-depth)">
    ${MARK_INNER_SVG}
  </g>
</svg>`;
}

const iconSvg = getStandaloneSvg(512, 'circle');
const maskableSvg = getStandaloneSvg(512, 'maskable');
const appleTouchSvg = getStandaloneSvg(180, 'squircle');

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
  await page.screenshot({ path: targetPath, omitBackground: true });
  await page.close();
}

console.log('Rendering raster icons...');
await renderPng(iconSvg, 512, 512, join(staticDir, 'icon-512.png'));
await renderPng(iconSvg, 192, 192, join(staticDir, 'icon-192.png'));
await renderPng(maskableSvg, 512, 512, join(staticDir, 'icon-maskable-512.png'));
await renderPng(appleTouchSvg, 180, 180, join(staticDir, 'apple-touch-icon.png'));
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
