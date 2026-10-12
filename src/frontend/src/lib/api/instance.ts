import { native } from './platform';
import { secrets } from './secrets';

/**
 * The self-hosted server the installed app talks to (docs/native-app-design.md, 3.2). The website is served by its server,
 * so there the instance is simply "this origin" and every function here answers as if nothing were configured.
 */

/** The address, which is not secret: device storage. */
const originKey = 'culina.instance';

/** The session token, which is: the Keychain or Keystore (see ./secrets). */
const tokenKey = 'culina.session';

let origin: string | null = null;
let token: string | null = null;
let loading: Promise<void> | null = null;

/** Reads what this device remembers; at most once per launch. A ternary, so the website's build drops the rest. */
export function loadInstance(): Promise<void> {
  return native ? (loading ??= remembered()) : Promise.resolve();
}

async function remembered(): Promise<void> {
  origin = readOrigin();
  token = origin ? await secrets.read(tokenKey) : null;
}

/** The connected server's origin, or null (always null on the website). */
export function instanceOrigin(): string | null {
  return native ? origin : null;
}

/** Where `path` (`/api/v1/...`) is answered: the path itself on the website, the connected server's in the app. */
export function apiUrl(path: string): string {
  return native && origin ? `${origin}${path}` : path;
}

/**
 * The address people open Culina at, for links meant for somebody else (a shared recipe, an invitation): this origin on
 * the website, the connected server's in the app, whose own origin (`capacitor://localhost`) means nothing elsewhere.
 */
export function publicOrigin(): string {
  return (native ? origin : null) ?? globalThis.location.origin;
}

/** The app's session token, sent as `Authorization: Bearer`; null on the website, whose session is the cookie. */
export function bearerToken(): string | null {
  return native ? token : null;
}

/** Makes `value` the connected server (or none); remembered for the next launch. */
export function useOrigin(value: string | null): void {
  origin = value;
  writeOrigin(value);
}

/** Keeps the token sign-in returned, or forgets it on sign-out or expiry. */
export function rememberSessionToken(value: string | null): Promise<void> {
  return native ? keepToken(value) : Promise.resolve();
}

async function keepToken(value: string | null): Promise<void> {
  token = value;

  await (value ? secrets.write(tokenKey, value) : secrets.remove(tokenKey));
}

function readOrigin(): string | null {
  try {
    return globalThis.localStorage?.getItem(originKey) ?? null;
  } catch {
    return null;
  }
}

function writeOrigin(value: string | null): void {
  try {
    if (value) {
      globalThis.localStorage?.setItem(originKey, value);
    } else {
      globalThis.localStorage?.removeItem(originKey);
    }
  } catch {
    // Not remembered: the app asks again next launch.
  }
}
