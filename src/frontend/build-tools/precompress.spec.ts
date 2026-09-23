import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { randomBytes } from 'node:crypto';
import { mkdir, mkdtemp, readFile, rm, stat, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { brotliDecompressSync, gunzipSync } from 'node:zlib';

import { precompress } from './precompress';

/*
 * The host serves a compressed copy whenever one exists beside a file, so a
 * wrong copy is served to everybody whose browser can read it. What matters is
 * that the copies are the file, and that nothing is written that should not be.
 */
let build: string;

// Repetitive text, so compression has something to find — a real chunk does.
const script = 'export const greeting = "Guten Appetit";\n'.repeat(200);

beforeEach(async () => {
  build = await mkdtemp(join(tmpdir(), 'culina-precompress-'));
  await mkdir(join(build, '_app/immutable/chunks'), { recursive: true });
});

afterEach(() => rm(build, { recursive: true, force: true }));

const exists = (path: string) =>
  stat(join(build, path)).then(
    () => true,
    () => false
  );

describe('precompress', () => {
  it('writes copies that decompress to exactly the original', async () => {
    await writeFile(join(build, '_app/immutable/chunks/app.js'), script);

    await precompress(build);

    const brotli = await readFile(join(build, '_app/immutable/chunks/app.js.br'));
    const gzip = await readFile(join(build, '_app/immutable/chunks/app.js.gz'));

    expect(brotliDecompressSync(brotli).toString()).toBe(script);
    expect(gunzipSync(gzip).toString()).toBe(script);
  });

  it('reports each copy smaller than what it was made from', async () => {
    await writeFile(join(build, 'styles.css'), `.a{color:red}\n`.repeat(200));

    const [written] = await precompress(build);

    expect(written!.brotli).toBeLessThan(written!.bytes);
    expect(written!.gzip).toBeLessThan(written!.bytes);
  });

  it('writes no copy of a file too small to shrink', async () => {
    // Ten bytes: every compressed format's own framing is more than that.
    await writeFile(join(build, 'tiny.js'), 'export {};');

    await precompress(build);

    expect(await exists('tiny.js.br')).toBe(false);
    expect(await exists('tiny.js.gz')).toBe(false);
  });

  it('still compresses a small file when compressing it helps', async () => {
    // Under the kilobyte the old floor would have skipped, and worth having.
    await writeFile(join(build, 'small.js'), 'export const label = "Rezept";\n'.repeat(20));

    const [written] = await precompress(build);

    expect(written!.bytes).toBeLessThan(1024);
    expect(await exists('small.js.br')).toBe(true);
  });

  it('leaves formats that are compressed already alone', async () => {
    await writeFile(join(build, 'photo.webp'), Buffer.alloc(4096, 7));
    await writeFile(join(build, 'icon.png'), Buffer.alloc(4096, 7));

    await precompress(build);

    expect(await exists('photo.webp.br')).toBe(false);
    expect(await exists('icon.png.gz')).toBe(false);
  });

  it('never compresses the document, which the host renders rather than serves', async () => {
    await writeFile(join(build, 'index.html'), `<p>${'Rezept '.repeat(500)}</p>`);

    await precompress(build);

    expect(await exists('index.html.br')).toBe(false);
  });

  it('writes no copy that came out larger than the file', async () => {
    // Random bytes do not compress, so every copy of them is bigger.
    await writeFile(join(build, 'noise.js'), randomBytes(4096));

    const [written] = await precompress(build);

    expect(written).toMatchObject({ brotli: null, gzip: null });
    expect(await exists('noise.js.br')).toBe(false);
    expect(await exists('noise.js.gz')).toBe(false);
  });

  it('is not fooled by a directory named like a script', async () => {
    await mkdir(join(build, 'vendor.js'), { recursive: true });

    await expect(precompress(build)).resolves.toEqual([]);
  });

  it('can run twice over the same build without compressing its own copies', async () => {
    await writeFile(join(build, 'app.js'), script);

    await precompress(build);
    const second = await precompress(build);

    expect(second.map((one) => one.file)).toEqual(['app.js']);
    expect(await exists('app.js.br.br')).toBe(false);
  });
});
