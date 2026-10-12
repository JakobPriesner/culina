import type { Plugin } from 'vite';

/**
 * Keeps the app's own modules out of the website's build. Their callers sit behind `__CULINA_NATIVE__`, which folds away,
 * but a dynamic `import()` is turned into a chunk before that happens: the website shipped Capacitor's plugins unloaded.
 * So in a website build each module is replaced with a stub of the same shape. `enforce: 'pre'` as for the gallery.
 */
const stubs: Record<string, string> = {
  'src/lib/app/nativeShell.ts': 'export async function startNativeShell() {}\n',
  'src/lib/app/nativeImages.ts':
    'export function startNativeImages() {}\nexport function forgetNativeImages() {}\n',
  'src/lib/api/connect.ts': [
    'export const supportedApiVersions = { oldest: 0, newest: 0 };',
    "export async function connect() { return 'address'; }",
    'export async function disconnect() {}',
    'export function normaliseAddress() { return null; }',
    ''
  ].join('\n')
};

export function nativeOnly(enabled: boolean): Plugin {
  return {
    name: 'culina:native-only',
    enforce: 'pre',
    apply: 'build',

    load(id) {
      if (enabled) {
        return null;
      }

      const path = id.split('?')[0]?.replaceAll('\\', '/') ?? '';
      const stub = Object.entries(stubs).find(([source]) => path.endsWith(source));

      return stub ? stub[1] : null;
    }
  };
}
