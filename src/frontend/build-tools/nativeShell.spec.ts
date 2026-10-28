import { createHash } from 'node:crypto';

import { describe, expect, it } from 'vitest';

import { finishShell, nativeShellMarker } from './nativeShell.ts';

/* The app's index.html is a file on the phone: no server stamps a nonce into it, so its policy allows exact hashes. */
const shell = `<!doctype html>
<html lang="en">
  <head>
    <meta charset="utf-8" />
    <link rel="manifest" href="/manifest.webmanifest" />
    <script nonce="__CULINA_NONCE__">boot()</script>
    <style nonce="__CULINA_NONCE__">body{margin:0}</style>
    <script type="module" src="/_app/immutable/entry/start.js"></script>
  </head>
  <body><script>start()</script></body>
</html>`;

const sha = (body: string) => `'sha256-${createHash('sha256').update(body).digest('base64')}'`;

describe("the app's shell", () => {
  const finished = finishShell(shell);
  const policy = /http-equiv="Content-Security-Policy" content="([^"]+)"/.exec(finished)?.[1] ?? '';

  it('carries no nonce placeholder', () => {
    expect(finished).not.toContain('__CULINA_NONCE__');
  });

  it('allows exactly its own inline scripts and styles', () => {
    expect(policy).toContain(
      `script-src 'self' ${sha('boot()')} ${sha('start()')} 'wasm-unsafe-eval';`
    );
    expect(policy).toContain(`style-src 'self' ${sha('body{margin:0}')};`);
    expect(policy).not.toContain('unsafe-inline');
    expect(policy).not.toContain("'unsafe-eval'");
    expect(policy).toContain("require-trusted-types-for 'script'");
  });

  it('drops the install manifest and says it is the app', () => {
    expect(finished).not.toContain('rel="manifest"');
    expect(finished).toContain(`<html ${nativeShellMarker} lang="en">`);
  });
});
