import { brotliCompressSync, constants, gzipSync } from 'node:zlib';
import { glob, readFile, writeFile } from 'node:fs/promises';
import { join, relative } from 'node:path';

/**
 * Writes Brotli and gzip copies beside every text file in a build. The host compresses no response (BREACH), but hashed static files
 * carry no secret and reflect nothing, so build-time copies keep that rule intact: the server only picks between files that exist.
 */

/** Text only (images and fonts are compressed already). No size floor: a 1 kB one cost 14 kB in the service worker's first download. */
const compressible = /\.(?:js|mjs|css|svg|json|webmanifest|txt|xml)$/;

export interface Compressed {
  readonly file: string;
  readonly bytes: number;
  /** Null when Brotli did not make it smaller, and no copy was written. */
  readonly brotli: number | null;
  /** Null when gzip did not make it smaller, and no copy was written. */
  readonly gzip: number | null;
}

/** Compresses every eligible file under `buildDir` in place, writing a copy only when smaller, since the host serves any variant that exists. */
export async function precompress(buildDir: string): Promise<Compressed[]> {
  const written: Compressed[] = [];

  for await (const entry of glob('**/*', { cwd: buildDir, withFileTypes: true })) {
    if (!entry.isFile() || !compressible.test(entry.name)) {
      continue;
    }

    const path = join(entry.parentPath, entry.name);
    const file = relative(buildDir, path);
    const original = await readFile(path);

    const brotli = brotliCompressSync(original, {
      params: {
        [constants.BROTLI_PARAM_QUALITY]: constants.BROTLI_MAX_QUALITY,
        [constants.BROTLI_PARAM_SIZE_HINT]: original.byteLength
      }
    });
    const gzip = gzipSync(original, { level: constants.Z_BEST_COMPRESSION });

    written.push({
      file,
      bytes: original.byteLength,
      brotli: await keepIfSmaller(`${path}.br`, brotli, original),
      gzip: await keepIfSmaller(`${path}.gz`, gzip, original)
    });
  }

  return written;
}

async function keepIfSmaller(
  path: string,
  compressed: Buffer,
  original: Buffer
): Promise<number | null> {
  if (compressed.byteLength >= original.byteLength) {
    return null;
  }

  await writeFile(path, compressed);

  return compressed.byteLength;
}

export function describe(files: readonly Compressed[]): string {
  const sum = (pick: (file: Compressed) => number) =>
    files.reduce((total, file) => total + pick(file), 0);

  const original = sum((file) => file.bytes);
  const brotli = sum((file) => file.brotli ?? file.bytes);
  const gzip = sum((file) => file.gzip ?? file.bytes);

  return (
    `compressed ${files.length} files: ${kb(original)} as built, ` +
    `${kb(brotli)} as Brotli, ${kb(gzip)} as gzip`
  );
}

const kb = (bytes: number) => `${(bytes / 1024).toFixed(1)} kB`;
