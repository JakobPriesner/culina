/**
 * Whether this bundle is the installed app's (`CULINA_TARGET=native`, see docs/native-app-design.md part 5).
 * A `define` literal like `__CULINA_GALLERY__`, so the web build drops every native branch and ships none of it.
 */
export const native: boolean = __CULINA_NATIVE__;

/** The push relay the app registers its device with, or empty when this build has none (always on the website). */
export const relayUrl: string = __CULINA_RELAY__;

export type NativePlatform = 'ios' | 'android';

/** Which app this is, from the bridge the native shell injects; Android when there is none (the bundle on a plain static host). */
export function nativePlatform(): NativePlatform {
  const bridge = (globalThis as { Capacitor?: { getPlatform?: () => string } }).Capacitor;

  return bridge?.getPlatform?.() === 'ios' ? 'ios' : 'android';
}
