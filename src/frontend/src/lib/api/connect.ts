import { rememberSessionToken, useOrigin } from './instance';

/**
 * Choosing the app's server (docs/native-app-design.md, 3.2): only the app's connect screen and its "use a different
 * server" reach this module, so the website never loads it.
 */

/**
 * The API generations this build understands (`Contracts.ApiContract.Version` on the server, sent by `GET /setup`).
 * Widened in the same change that raises the server's.
 */
export const supportedApiVersions = { oldest: 1, newest: 1 } as const;

/** How long the address check waits: a server on the far side of a slow home upload. */
const checkTimeoutMs = 10_000;

export type ConnectFailure =
  /** Not something that reads as a server address. */
  | 'address'
  /** Nothing answered. */
  | 'unreachable'
  /** Something answered, but not Culina. */
  | 'not-culina'
  /** A Culina still being set up; that happens in a browser. */
  | 'not-set-up'
  /** A server older than this app understands. */
  | 'server-too-old'
  /** A server newer than this app understands. */
  | 'app-too-old';

/** Checks that a Culina this app understands answers at `address`, and makes it the instance. Null when connected. */
export async function connect(address: string): Promise<ConnectFailure | null> {
  const candidate = normaliseAddress(address);

  if (!candidate) {
    return 'address';
  }

  const failure = await check(candidate);

  if (failure) {
    return failure;
  }

  // A different server is a different set of accounts: no token carries over.
  useOrigin(candidate);
  await rememberSessionToken(null);

  return null;
}

/** Forgets the server and the session with it, for "use a different server". */
export async function disconnect(): Promise<void> {
  useOrigin(null);
  await rememberSessionToken(null);
}

/**
 * The origin a typed address means, or null. A bare host is https; plain http only for an address on the home network
 * (iOS asks for local-network access there), since anything else would send the password in the clear.
 */
export function normaliseAddress(input: string): string | null {
  const trimmed = input.trim();

  if (!trimmed) {
    return null;
  }

  let url: URL;

  try {
    url = new URL(/^[a-z][a-z\d+.-]*:\/\//i.test(trimmed) ? trimmed : `https://${trimmed}`);
  } catch {
    return null;
  }

  if (url.username || url.password) {
    return null;
  }

  if (url.protocol === 'https:' || (url.protocol === 'http:' && isPrivateHost(url.hostname))) {
    return url.origin;
  }

  return null;
}

function isPrivateHost(hostname: string): boolean {
  return (
    hostname === 'localhost' ||
    hostname.endsWith('.localhost') ||
    hostname.endsWith('.local') ||
    hostname.endsWith('.home.arpa') ||
    /^127\./.test(hostname) ||
    /^10\./.test(hostname) ||
    /^192\.168\./.test(hostname) ||
    /^172\.(1[6-9]|2\d|3[01])\./.test(hostname)
  );
}

async function check(candidate: string): Promise<ConnectFailure | null> {
  let response: Response;

  try {
    response = await fetch(`${candidate}/api/v1/setup`, {
      credentials: 'omit',
      headers: { Accept: 'application/json' },
      signal: AbortSignal.timeout(checkTimeoutMs)
    });
  } catch {
    return 'unreachable';
  }

  const body: unknown = await response.json().catch(() => null);

  if (!response.ok || !isSetup(body)) {
    return 'not-culina';
  }

  // Culina from before the apps existed has no apiVersion.
  if (typeof body.apiVersion !== 'number' || body.apiVersion < supportedApiVersions.oldest) {
    return 'server-too-old';
  }

  if (body.apiVersion > supportedApiVersions.newest) {
    return 'app-too-old';
  }

  return body.stage === 'complete' ? null : 'not-set-up';
}

function isSetup(body: unknown): body is { stage: string; apiVersion?: unknown } {
  return (
    typeof body === 'object' && body !== null && 'stage' in body && typeof body.stage === 'string'
  );
}
