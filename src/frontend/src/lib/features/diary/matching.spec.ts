import { describe, expect, it } from 'vitest';

import { fit, fitting } from './matching';

describe('fit', () => {
  it('finds the words in any order, whatever the umlauts and punctuation', () => {
    expect(fit('Milch, fettarm, 1,5 % Fett', 'fettarm milch')).not.toBeNull();
    expect(fit('Hähnchenbrust', 'hahnchen')).not.toBeNull();
    expect(fit('Skyr Natur', 'natur skyr')).not.toBeNull();
  });

  it('finds a word inside a compound, but only from three letters on', () => {
    expect(fit('Vollmilch', 'milch')).not.toBeNull();
    expect(fit('Vollmilch', 'mi')).toBeNull();
  });

  it('needs every word', () => {
    expect(fit('Skyr Natur', 'skyr vanille')).toBeNull();
  });
});

describe('fitting', () => {
  it('puts a name that starts with the word before one that only holds it', () => {
    const names = ['Buttermilch', 'Milchreis', 'Milch'];

    expect(fitting(names, (name) => name, 'milch')).toEqual(['Milch', 'Milchreis', 'Buttermilch']);
  });
});
