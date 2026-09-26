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
  <!-- Steam plumes: smooth organic vapor with tapered ends -->
  <path d="M15.5 16.1 C13.2 13.1 17.5 10.4 15.5 6.6 C14.7 5.2 13.8 4.4 13.2 3.8" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round" fill="none" />
  <path d="M24.5 16.1 C22.2 13.1 26.5 10.4 24.5 6.6 C23.7 5.2 22.8 4.4 22.2 3.8" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round" fill="none" />
  <!-- Bowl Rim with gentle lip -->
  <rect x="5.5" y="18.6" width="29" height="3.2" rx="1.6" fill="currentColor" />
  <!-- Solid Ceramic Bowl Body -->
  <path d="M7.5 20.1 C8.3 28.1 13.8 32.4 20 32.4 C26.2 32.4 31.7 28.1 32.5 20.1 Z" fill="currentColor" />
  <!-- Distinct Ceramic Foot with 1.4 unit gap -->
  <rect x="14.5" y="33.8" width="11" height="2.4" rx="1.2" fill="currentColor" />
`;

function getStandaloneSvg(size = 512, shape = 'circle') {
  const isMaskable = shape === 'maskable';
  const targetRatio = isMaskable ? 0.5 : 0.68;
  const scale = (size * targetRatio) / 40;
  const tx = (size - 40 * scale) / 2;
  const ty = (size - 40 * scale) / 2;

  let bgShape = '';
  if (isMaskable || shape === 'squircle') {
    // Full bleed square for Android adaptive icons and iOS apple-touch-icon
    bgShape = `
      <rect width="${size}" height="${size}" fill="url(#culina-grad)" />
      <rect width="${size}" height="${size}" fill="url(#culina-ambient)" />
    `;
  } else if (shape === 'circle') {
    // Circular badge with warm olive gradient and crisp border ring
    const r = size * 0.47;
    const c = size / 2;
    bgShape = `
      <circle cx="${c}" cy="${c}" r="${r}" fill="url(#culina-grad)" />
      <circle cx="${c}" cy="${c}" r="${r}" fill="url(#culina-ambient)" />
      <circle cx="${c}" cy="${c}" r="${r - 0.75}" fill="none" stroke="#718b56" stroke-width="${Math.max(1, size * 0.015)}" stroke-opacity="0.75" />
    `;
  }

  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${size} ${size}" role="img" aria-label="Culina">
  <!--
    A porcelain bowl with rising steam in the Culina warm olive seal.
    Scalable vector icon used as modern SVG favicon and PWA brand mark.

    Literal colours, not token names: this file is fetched by the browser as an
    image and never sees the app stylesheet. Ground is the olive 700/900 pair
    (#4d6239 to #2c3a1e); the mark is sand 50 (#fdfbf7), the warm porcelain
    paper tone.

    Keep the token names themselves out of this comment. XML forbids a double
    hyphen inside a comment, every one of those names begins with two, and the
    whole document fails to parse over it. That is not a warning anywhere: the
    browser simply has no icon.
  -->
  <defs>
    <linearGradient id="culina-grad" x1="15%" y1="0%" x2="85%" y2="100%">
      <stop offset="0%" stop-color="#4d6239" />
      <stop offset="50%" stop-color="#3d4f2c" />
      <stop offset="100%" stop-color="#2c3a1e" />
    </linearGradient>
    <radialGradient id="culina-ambient" cx="50%" cy="20%" r="80%">
      <stop offset="0%" stop-color="#ffffff" stop-opacity="0.16" />
      <stop offset="55%" stop-color="#ffffff" stop-opacity="0" />
      <stop offset="100%" stop-color="#000000" stop-opacity="0.22" />
    </radialGradient>
  </defs>

  ${bgShape}

  <g transform="translate(${tx}, ${ty}) scale(${scale})" color="#fdfbf7">
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
