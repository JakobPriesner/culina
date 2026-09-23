import { brotliCompressSync, constants, gzipSync } from 'node:zlib';
import { glob, readFile, writeFile } from 'node:fs/promises';
import { join, relative } from 'node:path';

/**
 * Writes a Brotli and a gzip copy beside every text file in a build.
 *
 * Culina's host compresses no response, and that is deliberate: compressing a
 * cookie-authenticated response invites BREACH. But the rule was wider than its
 * reason. BREACH needs a secret and attacker-controlled input in the same
 * response, and a content-hashed JavaScript file is the same bytes for every
 * visitor, carries no secret and reflects nothing. So these files were sent at
 * three times their size — 211 kB for a first load the budget measures at 70 —
 * to defend something that was never at risk.
 *
 * Compressing at build time rather than per request keeps the original rule
 * exactly as strong as it was: nothing the server generates is ever
 * compressed, because the server compresses nothing. It picks between files
 * that already exist, and the host's static file handling does the choosing.
 */

/**
 * Text. Images and fonts are compressed formats already, and would only grow.
 *
 * No size floor. There was one, at a kilobyte, on the theory that a file that
 * small fits in one packet either way — and measured, it cost 14 kB across the
 * build, which the service worker downloads whole on a first visit. Whether a
 * copy is worth having is decided by whether it came out smaller, below.
 */
const compressible = /\.(?:js|mjs|css|svg|json|webmanifest|txt|xml)$/;

export interface Compressed {
  /** The file, relative to the build directory. */
  readonly file: string;
  readonly bytes: number;
  /** Null when Brotli did not make it smaller, and no copy was written. */
  readonly brotli: number | null;
  /** Null when gzip did not make it smaller, and no copy was written. */
  readonly gzip: number | null;
}

/**
 * Compresses every eligible file under `buildDir`, in place.
 *
 * A copy is only written when it is actually smaller. The host serves a
 * variant whenever one exists, so a copy that came out larger would make the
 * response worse for exactly the clients that asked for it to be better.
 */
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
