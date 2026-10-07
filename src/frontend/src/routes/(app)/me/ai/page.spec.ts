import { screen, within } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import AiPage from './+page.svelte';
import { assistance } from '$features/assistance/stores/assistance.svelte';
import { renderWithProviders } from '$lib/test/render';

/* Tests the page, not the store: no API key in a readable field, all providers connectable, jobs only to capable ones. */
const connection = (provider: string, overrides: object = {}) => ({
  provider,
  apiKeyConfigured: false,
  baseUrl: '',
  usable: false,
  ...overrides
});

const use = (capability: string, overrides: object = {}) => ({
  capability,
  enabled: false,
  provider: '',
  model: '',
  defaultModel: `${capability}-default`,
  ...overrides
});

const configured = {
  enabled: true,
  connections: [
    connection('gemini', { apiKeyConfigured: true, usable: true }),
    connection('openai', { apiKeyConfigured: true, usable: true }),
    connection('ollama', { baseUrl: 'http://localhost:11434', usable: true })
  ],
  uses: [
    use('improve', { enabled: true, provider: 'ollama' }),
    use('draft', { enabled: true, provider: 'openai' }),
    use('read', { enabled: true, provider: 'gemini' }),
    use('draw', { enabled: true, provider: 'gemini' })
  ],
  monthlyBudget: 20,
  personalBudget: null
};

const offered = {
  providers: [
    {
      provider: 'gemini',
      reachable: true,
      problem: null,
      models: [
        { id: 'gemini-3-flash-preview', label: 'Gemini 3 Flash', canDraw: false },
        { id: 'gemini-3.1-flash-image-preview', label: 'Nano Banana 2', canDraw: true }
      ]
    },
    {
      provider: 'openai',
      reachable: true,
      problem: null,
      models: [{ id: 'gpt-6-astra', label: 'gpt-6-astra', canDraw: false }]
    },
    { provider: 'ollama', reachable: false, problem: 'assistance.unavailable', models: [] }
  ]
};

const emptyUsage = {
  since: '2026-09-01T00:00:00Z',
  totalCost: 0,
  monthlyBudget: 20,
  totalInputTokens: 0,
  totalOutputTokens: 0,
  totalPictures: 0,
  unpriced: 0,
  byPerson: [],
  byCapability: []
};

/* Saving re-reads the session; answering with assistant settings crashed the session store (no households). */
const me = {
  userId: 'u1',
  email: 'jakob@example.com',
  displayName: 'Jakob',
  isAdmin: true,
  createdAt: '2026-01-01T00:00:00Z',
  version: 1,
  households: [{ householdId: 'h1', name: 'Home', role: 'owner' }]
};

const myPreferences = {
  locale: 'en',
  theme: 'warm-paper',
  mode: 'light',
  measurementSystem: 'metric',
  version: 1
};

function serverAnswers(settings: object = configured, models: object = offered) {
  const json = (body: object) =>
    new Response(JSON.stringify(body), {
      status: 200,
      headers: { 'Content-Type': 'application/json' }
    });

  const fetched = vi.fn((input: unknown) => {
    const url = String(input instanceof Request ? input.url : input);

    if (url.includes('/usage')) return Promise.resolve(json(emptyUsage));
    if (url.includes('/models')) return Promise.resolve(json(models));
    if (url.includes('/users/me/settings')) return Promise.resolve(json(myPreferences));
    if (url.includes('/users/me')) return Promise.resolve(json(me));

    return Promise.resolve(json(settings));
  });

  vi.stubGlobal('fetch', fetched);

  return fetched;
}

const settle = async () => {
  for (let turn = 0; turn < 5; turn += 1) {
    await new Promise((resume) => setTimeout(resume, 0));
  }
};

/** One settings row by its label element; plain text also matches a select option and an Advanced field label. */
const rowFor = (label: string) =>
  screen.getByText(label, { selector: '.label' }).closest('.row') as HTMLElement;

/** The nth select in a row (provider first, then model); fails rather than assuming an index. */
function picker(row: HTMLElement, nth: number): HTMLElement {
  const found = within(row).getAllByRole('combobox')[nth];

  if (found === undefined) {
    throw new Error(`Expected at least ${nth + 1} selects in this row.`);
  }

  return found;
}

async function writes(fetched: ReturnType<typeof serverAnswers>) {
  const sent = fetched.mock.calls
    .map(([input]) => input)
    .filter((input): input is Request => input instanceof Request && input.method === 'PUT');

  return Promise.all(sent.map(async (request) => JSON.parse(await request.clone().text())));
}

const flipTheSwitch = () =>
  userEvent.click(screen.getByRole('switch', { name: 'Use the assistant' }));

beforeEach(() => {
  assistance.reset();
});

describe('the assistant settings page', () => {
  it('lists every provider, so adding one is filling a row in', async () => {
    serverAnswers({ ...configured, connections: [] });

    renderWithProviders(AiPage);
    await settle();

    expect(rowFor('Gemini')).toBeInTheDocument();
    expect(rowFor('OpenAI')).toBeInTheDocument();
    expect(rowFor('Ollama')).toBeInTheDocument();
    expect(screen.getAllByText('Not connected')).toHaveLength(3);
  });

  it('shows the form before the providers have said what they offer', async () => {
    let answerModels = (_: Response) => {};
    const listed = new Promise<Response>((resume) => {
      answerModels = resume;
    });

    const json = (body: object) =>
      new Response(JSON.stringify(body), {
        status: 200,
        headers: { 'Content-Type': 'application/json' }
      });

    vi.stubGlobal(
      'fetch',
      vi.fn((input: unknown) => {
        const url = String(input instanceof Request ? input.url : input);

        if (url.includes('/usage')) return Promise.resolve(json(emptyUsage));
        if (url.includes('/models')) return listed;

        return Promise.resolve(json(configured));
      })
    );

    renderWithProviders(AiPage);
    await settle();

    expect(rowFor('Gemini')).toBeInTheDocument();
    expect(screen.getAllByText('Loading models…').length).toBeGreaterThan(0);

    answerModels(json(offered));
    await settle();

    expect(screen.queryByText('Loading models…')).not.toBeInTheDocument();
    expect(
      within(picker(rowFor('Create recipe image'), 1)).getByText('Nano Banana 2')
    ).toBeInTheDocument();
  });

  it('shows several providers connected at once', async () => {
    serverAnswers();

    renderWithProviders(AiPage);
    await settle();

    expect(screen.getAllByText('Ready')).toHaveLength(3);
  });

  it('never puts an API key in a field, only says that there is one', async () => {
    serverAnswers();

    renderWithProviders(AiPage);
    await settle();

    expect(screen.getAllByText('A key is set.')).toHaveLength(2);
    expect(screen.queryByPlaceholderText('Paste the key')).not.toBeInTheDocument();
  });

  it('asks for a key only once somebody says they want to replace one', async () => {
    serverAnswers();

    renderWithProviders(AiPage);
    await settle();

    await userEvent.click(within(rowFor('Gemini')).getByRole('button', { name: 'Replace' }));

    expect(screen.getByPlaceholderText('Paste the key')).toBeInTheDocument();
  });

  it('offers drawing only to providers that can draw', async () => {
    serverAnswers();

    renderWithProviders(AiPage);
    await settle();

    const drawing = picker(rowFor('Create recipe image'), 0);
    const offered = within(drawing)
      .getAllByRole('option')
      .map((option) => option.textContent?.trim());

    // Ollama makes no pictures, so it is absent rather than selectable and refused.
    expect(offered).toContain('Gemini');
    expect(offered).toContain('OpenAI');
    expect(offered).not.toContain('Ollama');
  });

  it('sends each job to the provider it was given', async () => {
    const fetched = serverAnswers();

    renderWithProviders(AiPage);
    await settle();

    await flipTheSwitch();
    await settle();

    const [body] = await writes(fetched);
    const sent = Object.fromEntries(
      body.uses.map((one: { capability: string; provider: string }) => [
        one.capability,
        one.provider
      ])
    );

    expect(sent).toEqual({
      improve: 'ollama',
      draft: 'openai',
      read: 'gemini',
      draw: 'gemini'
    });
  });

  it('sends no key at all when the form is saved without touching one', async () => {
    const fetched = serverAnswers();

    renderWithProviders(AiPage);
    await settle();

    await flipTheSwitch();
    await settle();

    // Omitted, not empty: an empty string would delete a stored key.
    const [body] = await writes(fetched);
    expect(body.connections.every((one: { apiKey?: string }) => one.apiKey === undefined)).toBe(
      true
    );
  });

  it('saves a typed field when focus leaves it, not on every keystroke', async () => {
    const fetched = serverAnswers();

    renderWithProviders(AiPage);
    await settle();

    const monthly = screen.getByRole('textbox', { name: 'Total per month' });
    await userEvent.clear(monthly);
    await userEvent.type(monthly, '35');
    await settle();

    expect(await writes(fetched)).toHaveLength(0);

    await userEvent.tab();
    await settle();

    const sent = await writes(fetched);
    expect(sent).toHaveLength(1);
    expect(sent[0].monthlyBudget).toBe(35);
  });

  it('sends nothing when focus merely passes through the form', async () => {
    const fetched = serverAnswers();

    renderWithProviders(AiPage);
    await settle();

    await userEvent.click(screen.getByRole('textbox', { name: 'Total per month' }));
    await userEvent.tab();
    await settle();

    expect(await writes(fetched)).toHaveLength(0);
  });

  it('keeps the stored key when a key field is opened and left empty', async () => {
    const fetched = serverAnswers();

    renderWithProviders(AiPage);
    await settle();

    await userEvent.click(within(rowFor('Gemini')).getByRole('button', { name: 'Replace' }));
    await userEvent.click(screen.getByPlaceholderText('Paste the key'));
    await userEvent.tab();
    await flipTheSwitch();
    await settle();

    const sent = await writes(fetched);
    const gemini = sent
      .at(-1)
      .connections.find((one: { provider: string }) => one.provider === 'gemini');
    expect(gemini.apiKey).toBeUndefined();
  });

  it('says the key is needed again once the address of a provider with one changes', async () => {
    serverAnswers();

    renderWithProviders(AiPage);
    await settle();

    await userEvent.click(screen.getByText('Advanced'));
    const address = screen.getByRole('textbox', { name: 'OpenAI — Address' });
    await userEvent.type(address, 'https://collector.example/v1');

    // The server only sends a stored key to the address it was saved for.
    expect(
      screen.getByText(
        'Changing the address means entering the key again: Culina only sends a stored key to the address it was saved for.'
      )
    ).toBeInTheDocument();
  });

  it('takes a key away only when asked to', async () => {
    const fetched = serverAnswers();

    renderWithProviders(AiPage);
    await settle();

    await userEvent.click(within(rowFor('Gemini')).getByRole('button', { name: 'Remove key' }));
    await settle();

    const [body] = await writes(fetched);
    const gemini = body.connections.find((one: { provider: string }) => one.provider === 'gemini');
    expect(gemini.apiKey).toBe('');
  });

  it('offers the models its provider listed, filtered to what the job needs', async () => {
    serverAnswers();

    renderWithProviders(AiPage);
    await settle();

    // The text-only listing cannot do "Read a photograph".
    const reading = picker(rowFor('Import recipe from photo'), 1);
    const labels = within(reading)
      .getAllByRole('option')
      .map((option) => option.textContent?.trim());

    expect(labels).toContain('Gemini 3 Flash');
    expect(labels).not.toContain('Nano Banana 2');
  });

  it('offers only drawing models to the drawing job', async () => {
    serverAnswers();

    renderWithProviders(AiPage);
    await settle();

    const drawing = picker(rowFor('Create recipe image'), 1);
    const labels = within(drawing)
      .getAllByRole('option')
      .map((option) => option.textContent?.trim());

    expect(labels).toContain('Nano Banana 2');
    expect(labels).not.toContain('Gemini 3 Flash');
  });

  it('sends the chosen model, not just the provider', async () => {
    const fetched = serverAnswers();

    renderWithProviders(AiPage);
    await settle();

    const reading = picker(rowFor('Import recipe from photo'), 1);
    await userEvent.selectOptions(reading, 'gemini-3-flash-preview');
    await settle();

    const [body] = await writes(fetched);
    const read = body.uses.find((one: { capability: string }) => one.capability === 'read');

    expect(read.provider).toBe('gemini');
    expect(read.model).toBe('gemini-3-flash-preview');
  });

  it('forgets the model when the provider changes, because it belonged to the old one', async () => {
    const fetched = serverAnswers();

    renderWithProviders(AiPage);
    await settle();

    const row = rowFor('Import recipe from photo');
    await userEvent.selectOptions(picker(row, 1), 'gemini-3-flash-preview');
    await userEvent.selectOptions(picker(row, 0), 'openai');
    await settle();

    const body = (await writes(fetched)).at(-1);
    const read = body.uses.find((one: { capability: string }) => one.capability === 'read');

    expect(read.provider).toBe('openai');
    expect(read.model).toBe('');
  });

  it('falls back to a text box for a provider that could not be listed', async () => {
    serverAnswers();

    renderWithProviders(AiPage);
    await settle();

    const row = rowFor('Improve a recipe');
    expect(within(row).getAllByRole('combobox')).toHaveLength(1);
    expect(within(row).getByRole('textbox')).toBeInTheDocument();
    expect(screen.getByText(/models from Ollama could not be loaded/i)).toBeInTheDocument();
  });

  it('says a provider has no list once, beside the provider, however many jobs it has', async () => {
    serverAnswers(
      {
        ...configured,
        uses: [
          use('improve', { enabled: true, provider: 'ollama' }),
          use('draft', { enabled: true, provider: 'ollama' }),
          use('read', { enabled: true, provider: 'ollama' }),
          use('draw', { enabled: true, provider: 'gemini' })
        ]
      },
      {
        providers: [
          ...offered.providers.filter((one) => one.provider !== 'ollama'),
          { provider: 'ollama', reachable: true, problem: null, models: [] }
        ]
      }
    );

    renderWithProviders(AiPage);
    await settle();

    const explained = screen.getAllByText(/Ollama answered, but listed no models/);

    expect(explained).toHaveLength(1);
    expect(rowFor('Ollama')).toContainElement(explained[0]!);

    for (const job of ['Improve a recipe', 'Write from an idea', 'Import recipe from photo']) {
      expect(within(rowFor(job)).getByText(/No list from Ollama/)).toBeInTheDocument();
    }
  });

  it('asks the providers again after a key is saved, so the lists are not stale', async () => {
    const fetched = serverAnswers();

    renderWithProviders(AiPage);
    await settle();

    await userEvent.click(within(rowFor('Gemini')).getByRole('button', { name: 'Replace' }));
    await userEvent.type(screen.getByPlaceholderText('Paste the key'), 'a-key');
    await userEvent.tab();
    await settle();

    const listings = fetched.mock.calls
      .map(([input]) => String(input instanceof Request ? input.url : input))
      .filter((url) => url.includes('/models'));

    expect(listings).toHaveLength(2);
  });

  it('says so when the lists could not be fetched at all', async () => {
    const json = (body: object) =>
      new Response(JSON.stringify(body), {
        status: 200,
        headers: { 'Content-Type': 'application/json' }
      });

    vi.stubGlobal(
      'fetch',
      vi.fn((input: unknown) => {
        const url = String(input instanceof Request ? input.url : input);

        if (url.includes('/usage')) return Promise.resolve(json(emptyUsage));
        if (url.includes('/models')) return Promise.resolve(new Response('', { status: 500 }));

        return Promise.resolve(json(configured));
      })
    );

    renderWithProviders(AiPage);
    await settle();

    expect(screen.getByText(/model list could not be loaded/)).toBeInTheDocument();
  });

  it('says what the sums are in, rather than leaving bare numbers', async () => {
    const json = (body: object) =>
      new Response(JSON.stringify(body), {
        status: 200,
        headers: { 'Content-Type': 'application/json' }
      });

    vi.stubGlobal(
      'fetch',
      vi.fn((input: unknown) => {
        const url = String(input instanceof Request ? input.url : input);

        if (url.includes('/usage'))
          return Promise.resolve(
            json({
              ...emptyUsage,
              totalCost: 3.5,
              byPerson: [{ userId: 'u1', displayName: 'Jakob', calls: 4, cost: 3.5 }]
            })
          );
        if (url.includes('/models')) return Promise.resolve(json(offered));

        return Promise.resolve(json(configured));
      })
    );

    renderWithProviders(AiPage);
    await settle();

    expect(screen.getAllByText(/\$/).length).toBeGreaterThan(0);
    expect(screen.queryByText('3.5')).not.toBeInTheDocument();
  });

  it('tells a refused key apart from a provider that is down', async () => {
    serverAnswers(configured, {
      providers: [
        ...offered.providers.filter((one) => one.provider !== 'openai'),
        { provider: 'openai', reachable: false, problem: 'assistance.rejected', models: [] }
      ]
    });

    renderWithProviders(AiPage);
    await settle();

    // A key is replaced, a provider is waited for: different messages.
    expect(screen.getByText(/OpenAI rejected the API key/)).toBeInTheDocument();
    expect(screen.getByText(/models from Ollama could not be loaded/i)).toBeInTheDocument();
  });

  it('offers the whole catalogue when nothing in it looks like what the job needs', async () => {
    // Image-capable is guessed from the name; if that empties a job's list, models are offered unfiltered (a wrong pick fails clearly, hiding the paid model loses the feature).
    serverAnswers(configured, {
      providers: [
        {
          provider: 'gemini',
          reachable: true,
          problem: null,
          models: [{ id: 'a-model-of-some-kind', label: 'A model', canDraw: false }]
        }
      ]
    });

    renderWithProviders(AiPage);
    await settle();

    expect(screen.getAllByRole('option', { name: 'A model' })).toHaveLength(2);
  });

  it('tells an empty catalogue apart from one that holds nothing for this job', async () => {
    serverAnswers(configured, {
      providers: [
        { provider: 'openai', reachable: true, problem: null, models: [] },
        {
          provider: 'gemini',
          reachable: true,
          problem: null,
          models: [{ id: 'gemini-3-flash-preview', label: 'Gemini 3 Flash', canDraw: false }]
        }
      ]
    });

    renderWithProviders(AiPage);
    await settle();

    const empty = screen.getByPlaceholderText('draft-default');

    const hintOf = (field: HTMLElement) =>
      field.closest('.field')?.querySelector('.hint')?.textContent ?? '';

    // Only that some explanation exists is asserted, not its copy: a box with no explanation is a dead end.
    expect(hintOf(empty)).not.toBe('');

    expect(screen.queryByPlaceholderText('draw-default')).not.toBeInTheDocument();
  });

  it('says nothing leaves the machine when every job is local', async () => {
    serverAnswers({
      ...configured,
      uses: [
        use('improve', { enabled: true, provider: 'ollama' }),
        use('draft', { enabled: true, provider: 'ollama' }),
        use('read', { enabled: true, provider: 'ollama' }),
        use('draw')
      ]
    });

    renderWithProviders(AiPage);
    await settle();

    expect(screen.getByText(/not to an external AI provider/)).toBeInTheDocument();
  });

  it('says what leaves the server as soon as one job is hosted', async () => {
    serverAnswers();

    renderWithProviders(AiPage);
    await settle();

    expect(screen.getByText(/sent to the provider selected for each feature/)).toBeInTheDocument();
  });
});
