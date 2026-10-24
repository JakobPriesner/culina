import {
  clientError,
  err,
  ErrorCodes,
  http,
  ok,
  publicOrigin,
  request,
  type AppError,
  type Result
} from '$api';
import { ResetEpoch, removeUndoable } from '$shell/optimistic';
import { LatestRead, registerStore, type LoadStatus } from '$shell/stores';

/** What a session was started from; anything newer than this app reads as `other`. */
export type DeviceKind = 'browser' | 'ios' | 'android' | 'instance' | 'agent' | 'other';

/** One signed-in session: where it came from and when it was last used, for deciding whether to end it. */
export interface Device {
  readonly sessionId: string;
  readonly kind: DeviceKind;
  readonly userAgent: string | null;
  readonly ipAddress: string | null;
  readonly lastSeenAt: string;
  readonly isCurrent: boolean;
}

const kinds: readonly DeviceKind[] = ['browser', 'ios', 'android', 'instance', 'agent'];

const kindOf = (client: string): DeviceKind => kinds.find((kind) => kind === client) ?? 'other';

/** The person's sessions, most recently used first, with this one on top. */
class Devices {
  #items = $state<Device[]>([]);
  #status = $state<LoadStatus>('idle');
  #error = $state<AppError | null>(null);

  #reads = new LatestRead();
  #epoch = new ResetEpoch();

  get items(): readonly Device[] {
    return this.#items;
  }

  get status(): LoadStatus {
    return this.#status;
  }

  get error(): AppError | null {
    return this.#error;
  }

  async load(): Promise<void> {
    const isLatest = this.#reads.start();

    this.#status = 'loading';

    const result = await request(() => http.GET('/api/v1/sessions'));

    if (!isLatest()) {
      return;
    }

    if (result.ok) {
      this.#items = result.value.items
        .map((item) => ({
          sessionId: item.sessionId,
          kind: kindOf(item.client),
          userAgent: item.userAgent ?? null,
          ipAddress: item.ipAddress ?? null,
          lastSeenAt: item.lastSeenAt,
          isCurrent: item.isCurrent
        }))
        .sort(
          (a, b) =>
            Number(b.isCurrent) - Number(a.isCurrent) || b.lastSeenAt.localeCompare(a.lastSeenAt)
        );
      this.#status = 'ready';
      this.#error = null;
    } else {
      this.#error = result.error;
      this.#status = 'failed';
    }
  }

  /** Signs one session out; gone from the list at once and put back if the server says no. */
  async revoke(sessionId: string): Promise<AppError | null> {
    const gone = removeUndoable(this.#items, (one) => one.sessionId === sessionId);
    const putBack = this.#epoch.guard(() => (this.#items = gone.undo(this.#items)));

    this.#items = gone.items;

    const result = await request(() =>
      http.DELETE('/api/v1/sessions/{sessionId}', { params: { path: { sessionId } } })
    );

    if (!result.ok) {
      putBack();

      return result.error;
    }

    return null;
  }

  reset(): void {
    this.#reads.cancel();
    this.#epoch.bump();
    this.#items = [];
    this.#status = 'idle';
    this.#error = null;
  }
}

export const devices = new Devices();

// Another person's devices must not be listed after the next sign-in on this one.
registerStore(() => devices.reset());

/**
 * A session for an AI agent, whose token works only on the MCP server. Returned, never kept: the caller shows it
 * once and drops it. This browser stays signed in (the server starts a separate session and sets no cookie).
 */
export async function connectAgent(email: string, password: string): Promise<Result<string>> {
  const result = await request(() =>
    http.POST('/api/v1/sessions', { body: { email, password, client: 'agent' } })
  );

  if (!result.ok) {
    return result;
  }

  // Always present for an agent; its absence is a server defect, not something to show as a token.
  const token = result.value.sessionToken;

  return token
    ? ok(token)
    : err(clientError(ErrorCodes.unexpected, 'The server did not return a token.'));
}

/** Where an agent reaches this server's MCP endpoint. */
export const mcpUrl = (): string => `${publicOrigin()}/api/mcp`;

/** The line that adds this server to Claude Code. */
export const claudeCommand = (url: string, token: string): string =>
  `claude mcp add --transport http culina ${url} --header "Authorization: Bearer ${token}"`;

/** "Firefox on macOS": enough to tell two browsers apart without showing the whole user-agent string. */
export function describeUserAgent(userAgent: string | null): string | null {
  if (!userAgent) {
    return null;
  }

  const browser = browsers.find(([pattern]) => pattern.test(userAgent))?.[1];
  const system = systems.find(([pattern]) => pattern.test(userAgent))?.[1];

  if (browser && system) {
    return `${browser} · ${system}`;
  }

  return browser ?? system ?? null;
}

// Order matters: Edge and Opera say Chrome too, and Chrome says Safari.
const browsers: readonly (readonly [RegExp, string])[] = [
  [/Edg\//, 'Edge'],
  [/OPR\//, 'Opera'],
  [/Firefox\/|FxiOS\//, 'Firefox'],
  [/Chrome\/|CriOS\//, 'Chrome'],
  [/Safari\//, 'Safari']
];

// iPhone and iPad first: an iPad says "Mac OS X".
const systems: readonly (readonly [RegExp, string])[] = [
  [/iPhone|iPad|iPod/, 'iOS'],
  [/Android/, 'Android'],
  [/Mac OS X|Macintosh/, 'macOS'],
  [/Windows/, 'Windows'],
  [/CrOS/, 'ChromeOS'],
  [/Linux/, 'Linux']
];
