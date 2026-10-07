import { chromium, type Browser } from '../node_modules/@playwright/test/index.mjs';
import { mkdir, readFile, rm, writeFile } from 'node:fs/promises';
import { execFileSync } from 'node:child_process';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import * as prettier from 'prettier';

import { appIconFolder, appIcons, type AppIcon } from '../src/lib/app/appIcons.ts';

/**
 * Draws every app icon and writes the files a browser or phone reads: `pnpm icons`.
 * Flat on purpose (platforms add their own gloss), drawn inside the 409px maskable circle
 * (Android's 66/108 safe zone),
 * and heavier below 48px where a hairline gap is a smudge.
 */
const staticDir = join(dirname(fileURLToPath(import.meta.url)), '../static');
const tempDir = join(staticDir, '../.icon-temp');

const colours = {
  basil: '#2f4a35',
  saffron: '#e2b13c',
  cream: '#fdfbf7',
  paper: '#f2ece0',
  ink: '#1c1b17'
};

interface Drawing {
  ground: string;
  mark: (fill: { body: string; accent: string }, small: boolean) => string;
  body: string;
  accent: string;
}

function point(cx: number, cy: number, r: number, degrees: number): string {
  const radians = (degrees * Math.PI) / 180;

  return `${(cx + r * Math.cos(radians)).toFixed(1)} ${(cy - r * Math.sin(radians)).toFixed(1)}`;
}

function monogram({ body, accent }: { body: string; accent: string }, small: boolean): string {
  const outer = 124;
  const stroke = small ? 72 : 58;
  const inner = outer - stroke;
  const dot = small ? 40 : 32;
  const cx = 231;
  const cy = 256;
  const [from, to] = [40, 320];
  const ring =
    `M${point(cx, cy, outer, from)} A${outer} ${outer} 0 1 0 ${point(cx, cy, outer, to)} ` +
    `L${point(cx, cy, inner, to)} A${inner} ${inner} 0 1 1 ${point(cx, cy, inner, from)} Z`;
  const scale = small ? ' transform="translate(256 256) scale(1.16) translate(-256 -256)"' : '';

  return `<g${scale}><path d="${ring}" fill="${body}"/><circle cx="${cx + 142}" cy="${cy + outer - dot}" r="${dot}" fill="${accent}"/></g>`;
}

function cocotte({ body, accent }: { body: string; accent: string }, small: boolean): string {
  const scale = small ? ' transform="translate(256 256) scale(1.12) translate(-256 -256)"' : '';

  return `<g${scale} fill="${body}">
    <path d="M128 222 C128 186 180 172 256 172 C332 172 384 186 384 222 Z"/>
    <path d="M144 244 H368 V316 A68 68 0 0 1 300 384 H212 A68 68 0 0 1 144 316 Z"/>
    <rect x="108" y="252" width="52" height="32" rx="16"/>
    <rect x="352" y="252" width="52" height="32" rx="16"/>
    <circle cx="256" cy="148" r="22" fill="${accent}"/>
  </g>`;
}

const drawings: Record<AppIcon, Drawing> = {
  basil: { ground: colours.basil, mark: monogram, body: colours.cream, accent: colours.saffron },
  ink: { ground: colours.ink, mark: monogram, body: colours.cream, accent: colours.saffron },
  paper: { ground: colours.paper, mark: monogram, body: colours.basil, accent: colours.saffron },
  saffron: { ground: colours.saffron, mark: monogram, body: colours.ink, accent: colours.cream },
  cocotte: { ground: colours.basil, mark: cocotte, body: colours.cream, accent: colours.saffron }
};

/**
 * The icon as a file; platform icons are full squares because the platform cuts its own shape (a
 * rounded corner shows as a dark rim).
 */
function svg(icon: AppIcon, { radius = 0, small = false } = {}): string {
  const { ground, mark, body, accent } = drawings[icon];

  // Literal colours: this file is fetched as an image and reads no stylesheet.
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512" role="img" aria-label="Culina">
  <rect width="512" height="512" rx="${radius}" fill="${ground}"/>
  ${mark({ body, accent }, small)}
</svg>
`;
}

/** The shape alone, for Android's themed icons, which keep only the alpha. */
function monochrome(icon: AppIcon): string {
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512">${drawings[icon].mark({ body: '#fff', accent: '#fff' }, false)}</svg>`;
}

async function render(browser: Browser, markup: string, size: number, path: string) {
  const page = await browser.newPage({ viewport: { width: size, height: size } });

  await page.setContent(
    `<!doctype html><style>*{margin:0}svg{display:block;width:${size}px;height:${size}px}</style>${markup}`
  );
  await page.screenshot({ path, omitBackground: true });
  await page.close();
}

async function writeIcon(browser: Browser, icon: AppIcon) {
  const folder = join(staticDir, appIconFolder(icon));
  const square = svg(icon);
  const small = svg(icon, { radius: 96, small: true });

  await mkdir(folder, { recursive: true });
  await writeFile(join(folder, 'icon.svg'), svg(icon, { radius: 112 }));

  await render(browser, square, 512, join(folder, 'icon-512.png'));
  await render(browser, square, 192, join(folder, 'icon-192.png'));
  await render(browser, square, 512, join(folder, 'icon-maskable-512.png'));
  await render(browser, monochrome(icon), 512, join(folder, 'icon-monochrome-512.png'));
  await render(browser, square, 180, join(folder, 'apple-touch-icon.png'));

  for (const size of [16, 32, 48]) {
    await render(browser, small, size, join(tempDir, `${icon}-${size}.png`));
  }

  // Pillow writes the .ico from the largest size (it drops sizes bigger than its source) and
  // quantises PNGs to a palette; opaque icons are quantised from RGB because a transparency chunk
  // on a full-bleed icon paints black on iOS.
  execFileSync('python3', [
    '-c',
    `
import sys
from PIL import Image
folder, temp, icon = sys.argv[1:]
sizes = [Image.open(f'{temp}/{icon}-{size}.png') for size in (16, 32, 48)]
sizes[2].save(f'{folder}/favicon.ico', format='ICO', sizes=[(16, 16), (32, 32), (48, 48)], append_images=sizes[:2])
for name in ['icon-512', 'icon-192', 'icon-maskable-512', 'icon-monochrome-512', 'apple-touch-icon']:
    path = f'{folder}/{name}.png'
    image = Image.open(path).convert('RGBA')
    source = image.convert('RGB') if image.getextrema()[3][0] == 255 else image
    source.quantize(colors=256, method=Image.Quantize.LIBIMAGEQUANT).save(path, optimize=True)
`,
    folder,
    tempDir,
    icon
  ]);
}

/** The root manifest is hand-written; each icon's manifest is it with its own files in. */
async function writeManifests() {
  const rootPath = join(staticDir, 'manifest.webmanifest');
  const manifest = JSON.parse(await readFile(rootPath, 'utf8'));

  for (const icon of appIcons) {
    const folder = appIconFolder(icon);
    const path = join(staticDir, folder, 'manifest.webmanifest');
    const icons = [
      { src: `${folder}/icon.svg`, type: 'image/svg+xml', sizes: 'any', purpose: 'any' },
      { src: `${folder}/icon-192.png`, type: 'image/png', sizes: '192x192', purpose: 'any' },
      { src: `${folder}/icon-512.png`, type: 'image/png', sizes: '512x512', purpose: 'any' },
      {
        src: `${folder}/icon-maskable-512.png`,
        type: 'image/png',
        sizes: '512x512',
        purpose: 'maskable'
      },
      {
        src: `${folder}/icon-monochrome-512.png`,
        type: 'image/png',
        sizes: '512x512',
        purpose: 'monochrome'
      }
    ];
    const shortcuts = manifest.shortcuts.map((shortcut: object) => ({
      ...shortcut,
      icons: [{ src: `${folder}/icon-192.png`, sizes: '192x192' }]
    }));
    const json = JSON.stringify({ ...manifest, icons, shortcuts });
    const options = await prettier.resolveConfig(path, { editorconfig: true });

    await writeFile(path, await prettier.format(json, { ...options, filepath: path }));
  }
}

await rm(join(staticDir, 'icons'), { recursive: true, force: true });
await mkdir(tempDir, { recursive: true });

const browser = await chromium.launch();

for (const icon of appIcons) {
  await writeIcon(browser, icon);
  console.log(`✓ ${icon}`);
}

await browser.close();
await rm(tempDir, { recursive: true, force: true });
await writeManifests();

console.log('✓ Every app icon and its manifest written to static/');
