import { describe, expect, it } from 'vitest';

import { isSourceKind, sourceAppNames, sourceApps } from './sourceApps';

describe('the apps a library can come from', () => {
  it('are named together as a sentence would, each one once', () => {
    expect(sourceAppNames()).toBe('Tandoor and Mealie');
    expect(new Set(sourceApps.map((app) => app.kind)).size).toBe(sourceApps.length);
  });

  it('know their own kinds and no others', () => {
    expect(isSourceKind('mealie')).toBe(true);
    expect(isSourceKind('web')).toBe(false);
  });
});
