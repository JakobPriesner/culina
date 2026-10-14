import { nativePlatform, relayUrl } from './platform';

/*
 * The publisher's push relay (docs/native-app-design.md, 7.1): the one host besides the person's own
 * server that the app talks to. It is told a store token and nothing else, so no session, no cookies.
 */

/** The relay's token for this phone's store token (7.1, step 2); null when the relay will not give one. */
export async function registerAtRelay(deviceToken: string): Promise<string | null> {
  try {
    const response = await fetch(new URL('/v1/devices', relayUrl), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ platform: nativePlatform(), deviceToken }),
      credentials: 'omit',
      signal: AbortSignal.timeout(15_000)
    });

    if (!response.ok) {
      return null;
    }

    const { relayToken } = (await response.json()) as { relayToken?: unknown };

    return typeof relayToken === 'string' && relayToken.length > 0 ? relayToken : null;
  } catch {
    return null;
  }
}

/** Revokes the relay token; best effort, as an unreachable relay must not keep anybody signed in. */
export async function revokeAtRelay(relayToken: string): Promise<void> {
  try {
    await fetch(new URL(`/v1/devices/${encodeURIComponent(relayToken)}`, relayUrl), {
      method: 'DELETE',
      credentials: 'omit',
      signal: AbortSignal.timeout(15_000)
    });
  } catch {
    // The server forgot the device already, which is what stops notifications to this person.
  }
}
