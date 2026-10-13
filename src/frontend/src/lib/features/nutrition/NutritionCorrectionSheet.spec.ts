import { screen, waitFor, within } from '@testing-library/svelte';
import { userEvent } from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import NutritionCorrectionSheet from './NutritionCorrectionSheet.svelte';
import type { NutritionLine } from './types';
import { session } from '$features/auth/session.svelte';
import { renderWithProviders } from '$lib/test/render';
import { preferences } from '$shell/preferences.svelte';
import { resetAllStores } from '$shell/stores';

const butters = [
  { code: 'M111111', nameDe: 'Süßrahmbutter', nameEn: 'Sweet cream butter', energyKcal: 741 },
  { code: 'M222222', nameDe: 'Butterschmalz', nameEn: 'Clarified butter', energyKcal: null }
];

const counted: NutritionLine = {
  ingredientId: 'a',
  status: 'counted',
  reason: null,
  food: {
    code: 'M110100',
    nameDe: 'Butter',
    nameEn: 'Butter',
    labelDe: 'Butter',
    labelEn: 'Butter'
  },
  grams: 200,
  via: 'mass',
  corrected: false,
  canRaiseEnergy: false,
  energyKcal: 372
};

let asked: ReturnType<typeof vi.fn>;

const respond = (items: unknown[] = butters) =>
  vi.stubGlobal(
    'fetch',
    (asked = vi.fn(() =>
      Promise.resolve(
        new Response(JSON.stringify({ items }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' }
        })
      )
    ))
  );

function show(line: NutritionLine = counted, extra: Record<string, unknown> = {}) {
  const handlers = {
    onchoose: vi.fn(),
    onexclude: vi.fn(),
    onreset: vi.fn(),
    onweigh: vi.fn(),
    onclose: vi.fn()
  };

  renderWithProviders(NutritionCorrectionSheet, {
    props: { name: 'Butter', line, ...handlers, ...extra }
  });

  return handlers;
}

/* jsdom has <dialog> but not the top layer, so showModal is the open state. */
beforeEach(() => {
  HTMLDialogElement.prototype.showModal = function showModal(this: HTMLDialogElement) {
    this.open = true;
  };
  preferences.setLocale('en');
});

afterEach(() => {
  preferences.setLocale('en');
  vi.unstubAllGlobals();
});

describe('the correction sheet', () => {
  it('keeps what it showed while it fades out after an answer', async () => {
    respond();

    let finishExit!: () => void;
    const exit = new Promise<void>((resolve) => (finishExit = resolve));

    HTMLDialogElement.prototype.getAnimations = () => [{ finished: exit } as unknown as Animation];
    HTMLDialogElement.prototype.close = function close(this: HTMLDialogElement) {
      this.open = false;
      this.dispatchEvent(new Event('close'));
    };

    const handlers = {
      onchoose: vi.fn(),
      onexclude: vi.fn(),
      onreset: vi.fn(),
      onweigh: vi.fn(),
      onclose: vi.fn()
    };
    const view = renderWithProviders(NutritionCorrectionSheet, {
      props: { name: 'Butter', line: counted, ...handlers }
    });

    expect(await screen.findByRole('dialog', { name: 'What is “Butter”?' })).toBeInTheDocument();

    // The caller empties both the moment a choice is made; the sheet is still on its way out.
    await view.rerender({ name: null, line: null });

    expect(
      screen.getByRole('heading', { name: 'What is “Butter”?', hidden: true })
    ).toBeInTheDocument();
    expect(screen.getByRole('list', { name: 'Counts as', hidden: true })).toBeInTheDocument();

    finishExit();
    Reflect.deleteProperty(HTMLDialogElement.prototype, 'getAnimations');

    await waitFor(() =>
      expect(
        screen.queryByRole('list', { name: 'Counts as', hidden: true })
      ).not.toBeInTheDocument()
    );
  });

  it('names the ingredient and says the answer holds for every recipe', async () => {
    respond();
    show();

    expect(await screen.findByRole('dialog', { name: 'What is “Butter”?' })).toBeInTheDocument();
    expect(
      screen.getByText(
        'Applies to every recipe of yours that uses “Butter”, and to households that inherit from you.'
      )
    ).toBeInTheDocument();
  });

  it('shows the current choice first with a checkmark and where it comes from', async () => {
    respond();
    show();

    const current = await screen.findByRole('button', { name: /Butter\s*Default/ });

    expect(current).toHaveAttribute('aria-current', 'true');
  });

  it('names the current food as a reader would, with the table name under it', async () => {
    respond();
    show({
      ...counted,
      food: { ...counted.food!, nameEn: 'Beef/pork, mince mixed, raw', labelEn: 'mixed mince' }
    });

    expect(
      await screen.findByRole('button', { name: /mixed mince\s*BLS: Beef\/pork, mince mixed, raw/ })
    ).toHaveAttribute('aria-current', 'true');
  });

  it('says a household choice is theirs', async () => {
    respond();
    show({ ...counted, corrected: true });

    expect(await screen.findByRole('button', { name: /Butter\s*your choice/ })).toHaveAttribute(
      'aria-current'
    );
  });

  it('searches from the name and shows each food with its energy per 100 g', async () => {
    respond();
    show();

    expect(
      await screen.findByRole('button', { name: /Sweet cream butter\s*741 kcal per 100 g/ })
    ).toBeInTheDocument();
    // A food with no energy is named without a number rather than with a made-up one.
    expect(screen.getByRole('button', { name: 'Clarified butter' })).toBeInTheDocument();
    expect(asked.mock.calls[0]?.[0].url).toContain('q=Butter');
  });

  it('chooses a food by its code', async () => {
    respond();

    const { onchoose, onclose } = show();

    await userEvent.click(await screen.findByRole('button', { name: /Sweet cream butter/ }));

    expect(onchoose).toHaveBeenCalledWith(expect.objectContaining({ code: 'M111111' }));
    expect(onclose).not.toHaveBeenCalled();
  });

  it('says plainly when nothing fits', async () => {
    respond([]);
    show();

    expect(await screen.findByText('Nothing found for “Butter”.')).toBeInTheDocument();
  });

  it('only says the search is not working when it fails, and still offers not counting', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.resolve(new Response(null, { status: 503 })))
    );

    const { onexclude } = show();

    expect(await screen.findByText(/Search is not working right now/)).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: "Don't count this" }));

    expect(onexclude).toHaveBeenCalled();
  });

  it('offers going back to the default only for a choice the household made', async () => {
    respond();

    const first = show();

    await screen.findByRole('button', { name: /Sweet cream butter/ });
    expect(screen.queryByRole('button', { name: 'Back to the default' })).not.toBeInTheDocument();
    expect(first.onreset).not.toHaveBeenCalled();
  });

  it('goes back to the default', async () => {
    respond();

    const { onreset } = show({ ...counted, corrected: true });

    await userEvent.click(await screen.findByRole('button', { name: 'Back to the default' }));

    expect(onreset).toHaveBeenCalled();
  });

  it('marks a household exclusion as the current choice and does not offer it again', async () => {
    respond();
    show({
      ingredientId: 'a',
      status: 'excluded',
      reason: null,
      food: null,
      grams: null,
      via: null,
      corrected: true,
      canRaiseEnergy: false,
      energyKcal: null
    });

    await waitFor(() =>
      expect(screen.getByRole('button', { name: /Don't count this/ })).toHaveAttribute(
        'aria-current'
      )
    );
    expect(screen.getAllByRole('button', { name: /Don't count this/ })).toHaveLength(1);
  });
});

const milk: NutritionLine = {
  ...counted,
  food: {
    code: 'H1',
    nameDe: 'Vollmilch, 3,5 % Fett',
    nameEn: 'Whole milk, 3.5 % fat',
    labelDe: 'Vollmilch 3,5 %',
    labelEn: 'whole milk 3.5 %'
  },
  variants: [
    { code: 'H1', labelDe: 'Vollmilch 3,5 %', labelEn: 'whole milk 3.5 %', energyKcal: 65 },
    { code: 'H2', labelDe: 'fettarme Milch 1,5 %', labelEn: 'low-fat milk 1.5 %', energyKcal: 47 },
    { code: 'H3', labelDe: 'Milch ohne Zahl', labelEn: 'milk without number', energyKcal: null }
  ]
};

describe('similar foods', () => {
  it('shows the current one ticked above them, with its energy and whose choice it is', async () => {
    respond();
    show(milk);

    const now = await screen.findByRole('list', { name: 'Counts as' });

    expect(
      within(now).getByRole('button', { name: /whole milk 3\.5 %.*65 kcal per 100 g · Default/ })
    ).toHaveAttribute('aria-current', 'true');
  });

  it('lists the others in one tap, the energy as quiet text, without the current one', async () => {
    respond();
    show(milk);

    const list = await screen.findByRole('list', { name: 'Similar foods' });

    expect(
      within(list).getByRole('button', { name: /low-fat milk 1\.5 %\s*47 kcal per 100 g/ })
    ).not.toHaveAttribute('aria-current');
    expect(within(list).getByRole('button', { name: 'milk without number' })).toBeInTheDocument();
    expect(within(list).queryByRole('button', { name: /whole milk/ })).not.toBeInTheDocument();
  });

  it('does not search until asked, so the similar foods are the offer', async () => {
    respond();
    show(milk);

    await screen.findByRole('list', { name: 'Similar foods' });

    expect(screen.getByRole('searchbox', { name: 'Search other foods' })).toHaveValue('');
    expect(asked).not.toHaveBeenCalled();

    await userEvent.type(screen.getByRole('searchbox', { name: 'Search other foods' }), 'Hafer');

    expect(await screen.findByRole('button', { name: /Sweet cream butter/ })).toBeInTheDocument();
    expect(asked.mock.calls.at(-1)?.[0].url).toContain('q=Hafer');
  });

  it('chooses one by its code, and the search below is for everything else', async () => {
    respond();

    const { onchoose } = show(milk);

    await userEvent.click(await screen.findByRole('button', { name: /low-fat milk 1\.5 %/ }));

    expect(onchoose).toHaveBeenCalledWith(expect.objectContaining({ code: 'H2' }));
    expect(screen.getByRole('searchbox', { name: 'Search other foods' })).toBeInTheDocument();
  });

  it('speaks German', async () => {
    preferences.setLocale('de');
    respond();
    show(milk);

    const list = await screen.findByRole('list', { name: 'Ähnliche Lebensmittel' });

    expect(within(list).getByRole('button', { name: /fettarme Milch 1,5 %/ })).toBeInTheDocument();
  });

  it('looks as it did before for a food with no kinds', async () => {
    respond();
    show();

    await screen.findByRole('button', { name: /Butter\s*Default/ });
    expect(screen.queryByRole('list', { name: 'Similar foods' })).not.toBeInTheDocument();
    expect(screen.getByRole('searchbox', { name: 'Search foods' })).toBeInTheDocument();
  });
});

describe('a choice made up the inheritance chain', () => {
  const me = {
    userId: 'u1',
    email: 'jakob@example.com',
    displayName: 'Jakob',
    isAdmin: false,
    createdAt: '2026-01-01T00:00:00Z',
    version: 1,
    assistance: { improve: false, draft: false, read: false, draw: false },
    households: [
      { householdId: 'home', name: 'Home', role: 'owner', inheritsFrom: [] },
      {
        householdId: 'flat',
        name: 'Flat',
        role: 'owner',
        inheritsFrom: [{ householdId: 'home', name: 'Home' }]
      }
    ]
  };

  const settings = {
    locale: 'en',
    theme: 'warm-paper',
    mode: 'light',
    measurementSystem: 'metric'
  };

  beforeEach(async () => {
    localStorage.clear();
    resetAllStores();
    vi.stubGlobal(
      'fetch',
      vi.fn((input: Request) =>
        Promise.resolve(
          new Response(JSON.stringify(input.url.endsWith('/settings') ? settings : me), {
            headers: { 'Content-Type': 'application/json' }
          })
        )
      )
    );
    await session.refresh();
    respond();
  });

  it('names the household that chose, and leaves taking it back to them', async () => {
    show({ ...counted, corrected: true, chosenBy: 'home' }, { householdId: 'flat' });

    expect(
      await screen.findByRole('button', { name: /Butter\s*chosen in “Home”/ })
    ).toHaveAttribute('aria-current', 'true');
    expect(screen.queryByRole('button', { name: 'Back to the default' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Take back your choice' })).not.toBeInTheDocument();
  });

  it('lets an heir take back its own choice, which leads to what it inherits', async () => {
    const { onreset } = show(
      { ...counted, corrected: true, chosenBy: 'flat' },
      { householdId: 'flat' }
    );

    await screen.findByRole('button', { name: /Butter\s*your choice/ });
    await userEvent.click(screen.getByRole('button', { name: 'Take back your choice' }));

    expect(onreset).toHaveBeenCalled();
  });

  it('starts from an inherited weight, says whose it is, and offers no removal', async () => {
    show(
      { ...counted, unitKey: 'piece', via: 'householdWeight', weighedBy: 'home', grams: 150 },
      { householdId: 'flat', unit: 'piece', perUnit: 150 }
    );

    expect(await screen.findByRole('textbox', { name: 'Weight in grams' })).toHaveValue('150');
    expect(screen.getAllByText('chosen in “Home”').length).toBeGreaterThan(0);
    expect(screen.queryByRole('button', { name: 'Remove weight' })).not.toBeInTheDocument();
  });
});

describe('the weight of one unit', () => {
  const onion: NutritionLine = {
    ...counted,
    status: 'amountNotInGrams',
    reason: 'count',
    grams: null,
    via: null,
    energyKcal: null,
    unitKey: 'piece'
  };

  const weigh = (line: NutritionLine, extra: Record<string, unknown> = {}) => {
    respond();

    return show(line, { unit: 'piece', ...extra });
  };

  it('asks what one unit weighs, says it holds for every recipe, and starts empty', async () => {
    weigh(onion);

    expect(
      await screen.findByRole('heading', { name: 'How much does 1 piece of “Butter” weigh?' })
    ).toBeInTheDocument();
    expect(
      screen.getByText('Applies to all your recipes with “Butter” in piece.')
    ).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: 'Weight in grams' })).toHaveValue('');
    expect(screen.queryByRole('button', { name: 'Remove weight' })).not.toBeInTheDocument();
  });

  it('starts from the typical weight and says where it comes from', async () => {
    weigh(
      {
        ...onion,
        status: 'counted',
        via: 'typicalWeight',
        grams: 110,
        source: 'USDA FoodData Central'
      },
      { perUnit: 110 }
    );

    expect(await screen.findByRole('textbox', { name: 'Weight in grams' })).toHaveValue('110');
    expect(screen.getByText('typical 110 g · USDA FoodData Central')).toBeInTheDocument();
  });

  it('starts from the household weight and offers to take it back', async () => {
    const { onweigh } = weigh(
      { ...onion, status: 'counted', via: 'householdWeight', grams: 150 },
      { perUnit: 150 }
    );

    expect(await screen.findByRole('textbox', { name: 'Weight in grams' })).toHaveValue('150');
    expect(screen.queryByText(/^typical/)).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Remove weight' }));

    expect(onweigh).toHaveBeenCalledWith(null);
  });

  it('saves what was typed, with a comma or a point', async () => {
    const { onweigh } = weigh(onion);

    const field = await screen.findByRole('textbox', { name: 'Weight in grams' });

    await userEvent.type(field, '4,5');
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    expect(onweigh).toHaveBeenCalledWith(4.5);
  });

  it.each(['', '0', '-3', '10001', 'abc'])(
    'tells %j is no weight, and sends nothing',
    async (typed) => {
      const { onweigh } = weigh(onion);

      const field = await screen.findByRole('textbox', { name: 'Weight in grams' });

      if (typed) {
        await userEvent.type(field, typed);
      }

      await userEvent.click(screen.getByRole('button', { name: 'Save' }));

      expect(await screen.findByRole('alert')).toHaveTextContent(
        'Enter a weight above 0 and at most 10,000 grams.'
      );
      expect(onweigh).not.toHaveBeenCalled();
    }
  );

  it('takes 10,000 g', async () => {
    const { onweigh } = weigh(onion);

    await userEvent.type(await screen.findByRole('textbox', { name: 'Weight in grams' }), '10000');
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    expect(onweigh).toHaveBeenCalledWith(10000);
  });

  it('is absent for a line counted in grams, and when there is no way to say it', async () => {
    respond();
    show({ ...counted, unitKey: null }, { unit: 'piece' });

    await screen.findByRole('button', { name: /Butter\s*Default/ });
    expect(screen.queryByRole('textbox', { name: 'Weight in grams' })).not.toBeInTheDocument();
  });

  it('speaks German', async () => {
    preferences.setLocale('de');
    respond();
    show(onion, { unit: 'Zehe', name: 'Knoblauch' });

    expect(
      await screen.findByRole('heading', { name: 'Wie viel wiegt 1 Zehe „Knoblauch“?' })
    ).toBeInTheDocument();
    expect(
      screen.getByText('Gilt für alle eure Rezepte mit „Knoblauch“ in Zehe.')
    ).toBeInTheDocument();
  });
});

describe('foods from the package', () => {
  const light = {
    id: 'f1',
    code: 'h:f1',
    name: 'Kokosmilch light',
    energyKj: 314,
    energyKcal: 75,
    fat: 7,
    own: true
  };

  let posted: unknown[];

  /** The table finds nothing; the household has its own foods, and takes a new one. */
  function serve(own: unknown[]) {
    posted = [];
    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: Request) => {
        const json = (body: unknown, status = 200) =>
          new Response(JSON.stringify(body), {
            status,
            headers: { 'Content-Type': 'application/json' }
          });

        if (input.url.includes('/households/h1/foods') && input.method === 'POST') {
          const body = (await input.json()) as { name: string; energyKcal: number };
          posted.push(body);

          return json({ ...light, id: 'f2', code: 'h:f2', ...body, energyKj: 0 }, 201);
        }

        return json({ items: input.url.includes('/households/h1/foods') ? own : [] });
      })
    );
  }

  const unknownKind: NutritionLine = { ...counted, status: 'unknownKind', food: null };

  afterEach(() => resetAllStores());

  it('offers the household’s own food for what was searched, cited from the package', async () => {
    serve([light]);
    const { onchoose } = show(unknownKind, { name: 'Kokosmilch light', householdId: 'h1' });

    const list = await screen.findByRole('list', { name: 'Your foods from the package' });
    expect(list).toHaveTextContent('Kokosmilch light');
    expect(list).toHaveTextContent('75 kcal per 100 g · From the package');

    await userEvent.click(within(list).getByRole('button'));

    expect(onchoose).toHaveBeenCalledWith({
      code: 'h:f1',
      nameDe: 'Kokosmilch light',
      nameEn: 'Kokosmilch light',
      energyKcal: 75,
      source: 'package'
    });
  });

  it('adds a food from the label, starting from the ingredient’s name, and chooses it', async () => {
    serve([]);
    const { onchoose } = show(unknownKind, { name: 'Kokosmilch light', householdId: 'h1' });

    await userEvent.click(await screen.findByRole('button', { name: 'Add from the package' }));

    expect(screen.getByLabelText('Name')).toHaveValue('Kokosmilch light');

    await userEvent.type(screen.getByLabelText('Energy (kcal)'), '75');
    await userEvent.type(screen.getByLabelText('Fat (g)'), '7,5');
    await userEvent.click(screen.getByRole('button', { name: 'Save and use' }));

    await waitFor(() =>
      expect(onchoose).toHaveBeenCalledWith(
        expect.objectContaining({ code: 'h:f2', source: 'package' })
      )
    );
    expect(posted).toEqual([
      expect.objectContaining({ name: 'Kokosmilch light', energyKcal: 75, fat: 7.5, protein: null })
    ]);
  });

  it('asks for the kilocalories before sending anything', async () => {
    serve([]);
    show(unknownKind, { name: 'Kokosmilch light', householdId: 'h1' });

    await userEvent.click(await screen.findByRole('button', { name: 'Add from the package' }));
    await userEvent.click(screen.getByRole('button', { name: 'Save and use' }));

    expect(screen.getByRole('alert')).toHaveTextContent('A name and the kilocalories are needed.');
    expect(posted).toEqual([]);
  });

  it('cites the package for a chosen food of the household', async () => {
    serve([]);
    show(
      {
        ...counted,
        corrected: true,
        food: {
          code: 'h:f1',
          source: 'package',
          nameDe: 'Kokosmilch light',
          nameEn: 'Kokosmilch light',
          labelDe: 'Kokosmilch light',
          labelEn: 'Kokosmilch light'
        }
      },
      { name: 'Kokosmilch light', householdId: 'h1' }
    );

    const now = await screen.findByRole('list', { name: 'Counts as' });

    expect(now).toHaveTextContent('From the package');
  });
});
