import { beforeEach, describe, expect, it } from 'vitest';
import { resetAllStores } from '$shell/stores';
import { libraryView } from './libraryView.svelte';

beforeEach(() => libraryView.reset());

describe('the library view', () => {
  it('keeps a household’s search and quick filter on return', () => {
    libraryView.forHousehold('one');
    libraryView.query = 'tomato';
    libraryView.quick = true;
    libraryView.forHousehold('one');
    expect(libraryView.query).toBe('tomato');
    expect(libraryView.quick).toBe(true);
  });
  it('does not carry another household’s filters across', () => {
    libraryView.forHousehold('one');
    libraryView.query = 'tomato';
    libraryView.quick = true;
    libraryView.forHousehold('two');
    expect(libraryView.query).toBe('');
    expect(libraryView.quick).toBe(false);
  });
  it('forgets the search at sign-out', () => {
    libraryView.forHousehold('one');
    libraryView.query = 'tomato';
    libraryView.quick = true;
    resetAllStores();
    expect(libraryView.query).toBe('');
    expect(libraryView.quick).toBe(false);
  });
});
