import { createHash } from 'node:crypto';
import { readFile, rm, writeFile } from 'node:fs/promises';
import { join } from 'node:path';

/**
 * Turns the SvelteKit output in `build-native/` into what the installed app ships (docs/native-app-design.md, 5.2).
 * On the website the .NET host stamps a fresh CSP nonce into every `index.html` it serves; the app's copy is a file on the
 * phone, so its inline scripts and styles are allowed by hash instead, and the policy travels in the document itself.
 */

/** Must match `SecurityHeaders.AnnouncerStyleHash` on the server: the one style attribute SvelteKit writes. */
const announcerStyleHash = 'sha256-S8qMpvofolR8Mpjy4kQvEm7m1q8clzU4dfDH0AmvZjo=';

/** Must match `SecurityHeaders.TrustedTypesPolicies`: the worker's policy is created when its module loads, even where no worker is registered. */
const trustedTypesPolicies = 'svelte-trusted-html sveltekit-trusted-url culina-worker-url';

/** Marks the document as the app's, for styles that differ (safe areas) and for the release check that the website has none of it. */
export const nativeShellMarker = 'data-shell="culina-app"';

/** Website-only files: the worker (WKWebView has none on the app's scheme), the install manifest, crawler and contact files. */
const websiteOnly = [
  'service-worker.js',
  'manifest.webmanifest',
  'robots.txt',
  'humans.txt',
  'sitemap.xml'
];

const inlineScript = /<script(?<attributes>[^>]*)>(?<body>[\s\S]*?)<\/script>/g;
const inlineStyle = /<style(?<attributes>[^>]*)>(?<body>[\s\S]*?)<\/style>/g;

const hash = (body: string) =>
  `'sha256-${createHash('sha256').update(body, 'utf8').digest('base64')}'`;

/** The app's `index.html`: nonce placeholders gone, every inline script and style hashed into a `<meta>` policy. */
export function finishShell(html: string): string {
  const withoutNonces = html.replaceAll(' nonce="__CULINA_NONCE__"', '');

  const scripts = [...withoutNonces.matchAll(inlineScript)]
    .filter((match) => !/\bsrc=/.test(match.groups!['attributes']!))
    .map((match) => hash(match.groups!['body']!));
  const styles = [...withoutNonces.matchAll(inlineStyle)].map((match) =>
    hash(match.groups!['body']!)
  );

  const policy = [
    "default-src 'self'",
    `script-src 'self' ${scripts.join(' ')}`.trim(),
    `style-src 'self' ${styles.join(' ')}`.trim(),
    `style-src-attr 'unsafe-hashes' '${announcerStyleHash}'`,
    // Photos arrive as object URLs (lib/app/nativeImages.ts), never from the server directly.
    "img-src 'self' data: blob:",
    // The server is chosen at run time; plain http only ever for an address on the home network (lib/api/instance.ts).
    "connect-src 'self' https: http:",
    "font-src 'self'",
    "object-src 'none'",
    "base-uri 'none'",
    "form-action 'self'",
    "require-trusted-types-for 'script'",
    `trusted-types ${trustedTypesPolicies}`
  ].join('; ');

  return withoutNonces
    .replace('<html', `<html ${nativeShellMarker}`)
    .replace(
      '<meta charset="utf-8" />',
      `<meta charset="utf-8" />\n    <meta http-equiv="Content-Security-Policy" content="${policy}" />`
    )
    .replace(/\s*<link rel="manifest"[^>]*>/, '');
}

export async function finishNativeShell(buildDir: string): Promise<void> {
  const shell = join(buildDir, 'index.html');

  await writeFile(shell, finishShell(await readFile(shell, 'utf8')));

  for (const file of websiteOnly) {
    await rm(join(buildDir, file), { force: true });
  }
}
