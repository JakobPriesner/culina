import type { components } from '$api/generated/schema';

type ServerWire = components['schemas']['SettingsGetServerResponse'];
type ServerRequest = components['schemas']['SettingsUpdateServerRequest'];
type DatabaseWire = components['schemas']['SettingsGetDatabaseResponse'];
type DatabaseRequest = components['schemas']['SettingsUpdateDatabaseRequest'];

/** How far a fresh instance has got in being set up. */
export type SetupStage = 'database' | 'account' | 'complete';

export interface Setup {
  readonly stage: SetupStage;
  /** Changes whenever the server restarts to apply a setting. */
  readonly startedAt: string;
}

export type RateLimits = ServerWire['rateLimits'];
export type RateLimit = keyof RateLimits;
export type OtlpProtocol = 'grpc' | 'http_protobuf';

/** The order the limits are shown in: people and sign-in first, plumbing last. */
export const rateLimits: readonly RateLimit[] = [
  'loginPerIpPerMinute',
  'loginPerAccountPerMinute',
  'registerPerIpPerHour',
  'invitationPerIpPerHour',
  'sharedRecipesPerIpPerMinute',
  'importsPerHour',
  'sourceRequestsPerHour',
  'assistantRequestsPerHour',
  'requestsPerSessionPerMinute'
];

/**
 * The server settings as a form holds them while somebody edits them.
 *
 * Text where a person types, not the numbers and lists the server takes: a
 * field that re-parsed every keystroke would eat the comma somebody just typed
 * between two addresses, and turn an emptied number box into a zero under
 * their cursor. The conversion happens once, on save.
 */
export interface ServerDraft {
  secure: boolean;
  sessionDays: string;
  renewAfterHours: string;
  knownProxies: string;
  knownNetworks: string;
  limits: Record<RateLimit, string>;
  otlpEndpoint: string;
  otlpProtocol: OtlpProtocol;
}

/** What the server says about itself beside the values: what is fixed, and how this request arrived. */
export interface ServerFacts {
  /** Environment variable names, e.g. `Cookies__Secure`. */
  readonly pinned: ReadonlySet<string>;
  readonly writable: boolean;
  readonly connection: ServerWire['connection'];
}

export interface DatabaseDraft {
  host: string;
  port: string;
  name: string;
  username: string;
  /** A new password, or empty to keep the one that is set. */
  password: string;
  requireSsl: boolean;
  maxPoolSize: string;
}

export interface DatabaseFacts {
  readonly pinned: ReadonlySet<string>;
  readonly writable: boolean;
  readonly passwordConfigured: boolean;
}

/** The environment variable that fixes one server setting, as the server names it. */
export const variables = {
  secure: 'Cookies__Secure',
  sessionDays: 'Cookies__SessionDays',
  renewAfterHours: 'Cookies__RenewAfterHours',
  knownProxies: 'ForwardedHeaders__KnownProxies',
  knownNetworks: 'ForwardedHeaders__KnownNetworks',
  otlpEndpoint: 'OTEL_EXPORTER_OTLP_ENDPOINT',
  otlpProtocol: 'OTEL_EXPORTER_OTLP_PROTOCOL'
} as const;

/** `loginPerIpPerMinute` → `RateLimits__LoginPerIpPerMinute`. */
export const limitVariable = (limit: RateLimit): string =>
  `RateLimits__${limit[0]!.toUpperCase()}${limit.slice(1)}`;

/** `host` → `Database__Host`. */
export const databaseVariable = (field: keyof DatabaseDraft): string =>
  `Database__${field[0]!.toUpperCase()}${field.slice(1)}`;

const list = (entries: readonly string[]): string => entries.join(', ');

const entries = (text: string): string[] =>
  text
    .split(',')
    .map((entry) => entry.trim())
    .filter((entry) => entry.length > 0);

/** A whole number as typed, or null when it is not one. */
export const wholeNumber = (text: string): number | null =>
  /^\s*\d+\s*$/.test(text) ? Number(text) : null;

export function toServerDraft(wire: ServerWire): ServerDraft {
  return {
    secure: wire.cookies.secure,
    sessionDays: String(wire.cookies.sessionDays),
    renewAfterHours: String(wire.cookies.renewAfterHours),
    knownProxies: list(wire.forwardedHeaders.knownProxies),
    knownNetworks: list(wire.forwardedHeaders.knownNetworks),
    limits: Object.fromEntries(
      rateLimits.map((limit) => [limit, String(wire.rateLimits[limit])])
    ) as Record<RateLimit, string>,
    otlpEndpoint: wire.telemetry.otlpEndpoint ?? '',
    otlpProtocol: wire.telemetry.otlpProtocol === 'http_protobuf' ? 'http_protobuf' : 'grpc'
  };
}

export function toServerFacts(wire: ServerWire): ServerFacts {
  return {
    pinned: new Set(wire.pinned),
    writable: wire.writable,
    connection: wire.connection
  };
}

/**
 * The fields of a draft that are not whole numbers, by name — checked before
 * sending, because the request cannot carry "12a" as a number at all.
 */
export function unreadableNumbers(draft: ServerDraft): string[] {
  const numbers: Record<string, string> = {
    sessionDays: draft.sessionDays,
    renewAfterHours: draft.renewAfterHours,
    ...draft.limits
  };

  return Object.entries(numbers)
    .filter(([, text]) => wholeNumber(text) === null)
    .map(([field]) => field);
}

/** Only called once `unreadableNumbers` is empty. */
export function toServerRequest(draft: ServerDraft): ServerRequest {
  const number = (text: string) => wholeNumber(text) ?? 0;

  return {
    cookies: {
      secure: draft.secure,
      sessionDays: number(draft.sessionDays),
      renewAfterHours: number(draft.renewAfterHours)
    },
    forwardedHeaders: {
      knownProxies: entries(draft.knownProxies),
      knownNetworks: entries(draft.knownNetworks)
    },
    rateLimits: Object.fromEntries(
      rateLimits.map((limit) => [limit, number(draft.limits[limit])])
    ) as RateLimits,
    telemetry: {
      otlpEndpoint: draft.otlpEndpoint.trim() || null,
      otlpProtocol: draft.otlpProtocol
    }
  };
}

export function toDatabaseDraft(wire: DatabaseWire): DatabaseDraft {
  return {
    host: wire.host,
    port: String(wire.port),
    name: wire.name,
    username: wire.username,
    password: '',
    requireSsl: wire.requireSsl,
    maxPoolSize: String(wire.maxPoolSize)
  };
}

export function toDatabaseFacts(wire: DatabaseWire): DatabaseFacts {
  return {
    pinned: new Set(wire.pinned),
    writable: wire.writable,
    passwordConfigured: wire.passwordConfigured
  };
}

/** The fields of a database draft that are not whole numbers. */
export const unreadableDatabaseNumbers = (draft: DatabaseDraft): string[] =>
  (['port', 'maxPoolSize'] as const).filter((field) => wholeNumber(draft[field]) === null);

export function toDatabaseRequest(draft: DatabaseDraft): DatabaseRequest {
  return {
    host: draft.host.trim(),
    port: wholeNumber(draft.port) ?? 0,
    name: draft.name.trim(),
    username: draft.username.trim(),
    // Empty is "keep the one that is set": the form never had it to send back.
    password: draft.password.length > 0 ? draft.password : null,
    requireSsl: draft.requireSsl,
    maxPoolSize: wholeNumber(draft.maxPoolSize) ?? 0
  };
}
