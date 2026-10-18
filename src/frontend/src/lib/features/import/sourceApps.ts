import { formatList } from '$shell/i18n';

/**
 * Every app a library can be imported from. A new importer on the server is added here and nowhere else
 * in the frontend: the connect form, the token links, the "supported are …" hints and the welcome tour all
 * read this list.
 */
export const sourceApps = [
  // Product names, the same in every language; `tokenPath` is where that app keeps its API tokens.
  { kind: 'tandoor', name: 'Tandoor', tokenPath: '/settings' },
  { kind: 'mealie', name: 'Mealie', tokenPath: '/user/profile/api-tokens' }
] as const;

export type SourceKind = (typeof sourceApps)[number]['kind'];

export const isSourceKind = (value: string): value is SourceKind =>
  sourceApps.some((app) => app.kind === value);

/** "Tandoor and Mealie", in the reader's language, however many there are. */
export const sourceAppNames = (): string => formatList(sourceApps.map((app) => app.name));
