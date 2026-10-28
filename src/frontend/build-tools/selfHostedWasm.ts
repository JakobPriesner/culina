import type { Plugin } from 'vite';

/**
 * The barcode ponyfill's default `locateFile` fetches zxing-wasm's .wasm from jsdelivr. The scanner always replaces it
 * with the copy in `/_app/` (lib/features/scanning/scanner.ts), so the default is dead code; but the app's own policy
 * allows any https: connection, so the address is taken out of the bundle rather than trusted to stay unused. Without
 * its host the default names a same-origin path that does not exist: a missed override fails here, not at a CDN.
 */
const cdnPrefix = 'https://fastly.jsdelivr.net/npm/';

const anyCdn = /https?:\/\/[^\s"'`]*(?:jsdelivr|unpkg|cdnjs)/;

export function selfHostedWasm(): Plugin {
  return {
    name: 'culina:self-hosted-wasm',
    apply: 'build',

    transform(code, id) {
      if (!id.replaceAll('\\', '/').includes('/barcode-detector/dist/')) {
        return null;
      }

      const local = code.replaceAll(cdnPrefix, '');

      // A new version that moved its default elsewhere must not slip back in unnoticed.
      if (anyCdn.test(local)) {
        this.error(`${id} still names a CDN; extend build-tools/selfHostedWasm.ts.`);
      }

      return local === code ? null : { code: local, map: null };
    }
  };
}
