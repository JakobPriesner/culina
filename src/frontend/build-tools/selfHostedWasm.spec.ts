import { describe, expect, it } from 'vitest';

import { selfHostedWasm } from './selfHostedWasm.ts';

const ponyfill =
  '/node_modules/.pnpm/barcode-detector@3.2.2/node_modules/barcode-detector/dist/es/zxing-exported.js';

function transform(code: string, id = ponyfill) {
  const hook = selfHostedWasm().transform as (
    this: { error: (message: string) => never },
    code: string,
    id: string
  ) => { code: string } | null;

  return hook.call(
    {
      error: (message) => {
        throw new Error(message);
      }
    },
    code,
    id
  );
}

describe('the barcode decoder’s WebAssembly', () => {
  it('loses the CDN address from the ponyfill’s default', () => {
    const result = transform(
      'return n ? `https://fastly.jsdelivr.net/npm/zxing-wasm@3.1.3/dist/${n[1]}/${e}` : t + e;'
    );

    expect(result?.code).toBe('return n ? `zxing-wasm@3.1.3/dist/${n[1]}/${e}` : t + e;');
  });

  it('fails the build when a new version names another CDN', () => {
    expect(() => transform('`https://unpkg.com/zxing-wasm@4.0.0/dist/${e}`')).toThrow(/CDN/);
  });

  it('leaves every other module alone', () => {
    expect(transform('`https://unpkg.com/x`', '/src/lib/elsewhere.ts')).toBeNull();
  });
});
