import type { components } from '$api/generated/schema';

type ServerWire = components['schemas']['SettingsGetServerResponse'];
type ServerRequest = components['schemas']['SettingsUpdateServerRequest'];
type DatabaseWire = components['schemas']['SettingsGetDatabaseResponse'];
type DatabaseRequest = components['schemas']['SettingsUpdateDatabaseRequest'];

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
  'archiveExportsPerHour',
  'requestsPerSessionPerMinute'
];

/**
 * The settings as a form holds them: text, not numbers and lists, so re-parsing never eats a typed comma
 * or turns an emptied box into 0. Converted on save.
 */
export interface ServerDraft {
  secure: boolean;
  sessionDays: string;
  renewAfterHours: string;
  maxSessionDays: string;
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
  /** The server the password that is set belongs to. */
  readonly server: {
    readonly host: string;
    readonly port: number;
    readonly name: string;
    readonly username: string;
  };
}

/** The environment variable that fixes one server setting, as the server names it. */
export const variables = {
  secure: 'Cookies__Secure',
  sessionDays: 'Cookies__SessionDays',
  renewAfterHours: 'Cookies__RenewAfterHours',
  maxSessionDays: 'Cookies__MaxSessionDays',
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

export const wholeNumber = (text: string): number | null =>
  /^\s*\d+\s*$/.test(text) ? Number(text) : null;

export function toServerDraft(wire: ServerWire): ServerDraft {
  return {
    secure: wire.cookies.secure,
    sessionDays: String(wire.cookies.sessionDays),
    renewAfterHours: String(wire.cookies.renewAfterHours),
    maxSessionDays: String(wire.cookies.maxSessionDays),
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

/** Draft fields that are not whole numbers, by name; checked before sending since the request cannot carry "12a". */
export function unreadableNumbers(draft: ServerDraft): string[] {
  const numbers: Record<string, string> = {
    sessionDays: draft.sessionDays,
    renewAfterHours: draft.renewAfterHours,
    maxSessionDays: draft.maxSessionDays,
    ...draft.limits
  };

  return Object.entries(numbers)
    .filter(([, text]) => wholeNumber(text) === null)
    .map(([field]) => field);
}

export function toServerRequest(draft: ServerDraft): ServerRequest {
  const number = (text: string) => wholeNumber(text) ?? 0;

  return {
    cookies: {
      secure: draft.secure,
      sessionDays: number(draft.sessionDays),
      renewAfterHours: number(draft.renewAfterHours),
      maxSessionDays: number(draft.maxSessionDays)
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
    passwordConfigured: wire.passwordConfigured,
    server: { host: wire.host, port: wire.port, name: wire.name, username: wire.username }
  };
}

/** Whether saving needs the password again: the server only returns a stored password to the server it was saved for. */
export const needsPasswordAgain = (draft: DatabaseDraft, facts: DatabaseFacts): boolean =>
  facts.passwordConfigured &&
  draft.password.length === 0 &&
  (draft.host.trim() !== facts.server.host ||
    wholeNumber(draft.port) !== facts.server.port ||
    draft.name.trim() !== facts.server.name ||
    draft.username.trim() !== facts.server.username);

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
