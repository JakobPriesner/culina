import { access, glob, readFile } from 'node:fs/promises';
import { join } from 'node:path';

/**
 * What a release build must not contain, beside the gallery (assertNoGallery.ts). Searched for in the output, since
 * none of it shows in review: a build flag that stops folding ships everything behind it.
 */

/** Text only the app's code holds (lib/api/client.ts `toInstance`); in the website it means `__CULINA_NATIVE__` stopped folding. */
const nativeOnly = ['No server is connected.'];

export async function findNativeText(buildDir: string): Promise<string[]> {
  const found: string[] = [];

  for await (const entry of glob('**/*.{js,html}', { cwd: buildDir })) {
    const contents = await readFile(join(buildDir, entry), 'utf8');

    for (const text of nativeOnly) {
      if (contents.includes(text)) {
        found.push(`${entry} contains "${text}"`);
      }
    }
  }

  return found.sort();
}

/** Source maps, or a pointer to one: never shipped, from the website or the app (CLAUDE.md). */
export async function findSourceMaps(buildDir: string): Promise<string[]> {
  const found: string[] = [];

  for await (const entry of glob('**/*', { cwd: buildDir })) {
    if (entry.endsWith('.map')) {
      found.push(`${entry} is a source map`);
    } else if (/\.(js|css|html)$/.test(entry)) {
      const contents = await readFile(join(buildDir, entry), 'utf8');

      if (contents.includes('sourceMappingURL')) {
        found.push(`${entry} points at a source map`);
      }
    }
  }

  return found.sort();
}

/** The website's worker in the app's bundle: WKWebView offers none on the app's scheme, and its caches would shadow the app's own. */
export async function findServiceWorker(buildDir: string): Promise<string[]> {
  try {
    await access(join(buildDir, 'service-worker.js'));

    return ['service-worker.js is in the app bundle'];
  } catch {
    return [];
  }
}
