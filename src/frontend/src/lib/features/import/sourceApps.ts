import { formatList } from '$shell/i18n';

/**
 * Every app a library can be imported from. A new importer on the server is added here and nowhere else
 * in the frontend: the connect form, the token links, the "supported are …" hints and the welcome tour all
 * read this list.
 */
export const sourceApps = [
  // Product names, the same in every language. `tokenPath` is where that app keeps its API tokens, or null for
  // an app that has none to paste, which is connected by signing in only; `account` is what its sign-in asks for.
  { kind: 'tandoor', name: 'Tandoor', tokenPath: '/settings', account: 'username' },
  { kind: 'mealie', name: 'Mealie', tokenPath: '/user/profile/api-tokens', account: 'username' },
  { kind: 'culina', name: 'Culina', tokenPath: null, account: 'email' }
] as const;

export type SourceApp = (typeof sourceApps)[number];

export type SourceKind = SourceApp['kind'];

export const isSourceKind = (value: string): value is SourceKind =>
  sourceApps.some((app) => app.kind === value);

/** "Tandoor and Mealie", in the reader's language, however many there are. */
export const sourceAppNames = (): string => formatList(sourceApps.map((app) => app.name));
