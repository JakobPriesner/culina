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
    Culina: The Artisanal Cocotte.
    A French enamelled cast-iron Dutch oven on a warm terracotta hearth.
    A symbol of slow cooking, kitchen warmth, and homemade meals.
    The brass knob adds a tactile culinary accent; the sturdy loop handles and
    domed lid create an unmistakable culinary silhouette that remains legible
    from a 512px app launcher down to a 16px browser tab favicon.

    Literal colours are intentional. This file is fetched as an image and does
    not inherit the app stylesheet.
  -->
  <defs>
    <linearGradient id="culina-ground" x1="100" y1="40" x2="420" y2="480" gradientUnits="userSpaceOnUse">
      <stop offset="0%" stop-color="#df6e4b" />
      <stop offset="48%" stop-color="#bf5335" />
      <stop offset="100%" stop-color="#8f331b" />
    </linearGradient>
    <radialGradient id="culina-light" cx="0" cy="0" r="1" gradientTransform="translate(160 110) rotate(48) scale(380 360)" gradientUnits="userSpaceOnUse">
      <stop stop-color="#ffffff" stop-opacity="0.18" />
      <stop offset="0.6" stop-color="#ffffff" stop-opacity="0" />
      <stop offset="1" stop-color="#000000" stop-opacity="0.22" />
    </radialGradient>
    <linearGradient id="culina-knob" x1="226" y1="132" x2="286" y2="166" gradientUnits="userSpaceOnUse">
      <stop offset="0%" stop-color="#ffd584" />
      <stop offset="100%" stop-color="#c98a2e" />
    </linearGradient>
    <filter id="culina-shadow" x="50" y="80" width="412" height="330" filterUnits="userSpaceOnUse">
      <feDropShadow dx="0" dy="12" stdDeviation="14" flood-color="#451408" flood-opacity="0.32" />
    </filter>
  </defs>

  <rect width="512" height="512" rx="${radius}" fill="url(#culina-ground)" />
  <rect width="512" height="512" rx="${radius}" fill="url(#culina-light)" />

  <g filter="url(#culina-shadow)">
    <!-- Brass Knob -->
    <rect x="226" y="132" width="60" height="24" rx="12" fill="url(#culina-knob)" />
    <rect x="246" y="154" width="20" height="12" rx="2" fill="url(#culina-knob)" />

    <!-- Lid -->
    <path
      d="M136 208 C148 166 196 160 256 160 C316 160 364 166 376 208 C378 215 372 220 364 220 H148 C140 220 134 215 136 208 Z"
      fill="#fffaf2"
    />

    <!-- Body & Handles -->
    <path
      fill-rule="evenodd"
      clip-rule="evenodd"
      d="
        M140 240
        H98 C82 240 72 250 72 266 C72 282 82 292 98 292 H128
        L138 340 C146 372 172 388 214 388 H298 C340 388 366 372 374 340
        L384 292 H414 C430 292 440 282 440 266 C440 250 430 240 414 240
        H372
        C372 240 140 240 140 240 Z

        M98 258
        H124 V274 H98
        C94 274 90 270 90 266
        C90 262 94 258 98 258 Z

        M388 258
        H414
        C418 258 422 262 422 266
        C422 270 418 274 414 274
        H388 V258 Z
      "
      fill="#fffaf2"
    />
  </g>
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
