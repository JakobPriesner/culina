import { screen, within } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import AiPage from './+page.svelte';
import { assistance } from '$features/assistance/stores/assistance.svelte';
import { renderWithProviders } from '$lib/test/render';

/*
 * The page rather than the store, because what is worth proving is all about
 * what is on screen: that no API key is ever in a field somebody could read it
 * out of, that every provider can be connected at once, and that a job can only
 * be given to a provider that could actually do it.
 */
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

    return Promise.resolve(json(settings));
  });

  vi.stubGlobal('fetch', fetched);

  return fetched;
}

/** Lets every queued effect and the requests it made settle. */
const settle = async () => {
  for (let turn = 0; turn < 5; turn += 1) {
    await new Promise((resume) => setTimeout(resume, 0));
  }
};

/**
 * One settings row, found by its label.
 *
 * Scoped to the label element rather than to any text: "Gemini" is also the
 * text of a select option and part of a field label under Advanced, so a bare
 * text query finds three things and fails on all of them.
 */
const rowFor = (label: string) =>
  screen.getByText(label, { selector: '.label' }).closest('.row') as HTMLElement;

/**
 * The nth select in a row, or a failed test.
 *
 * A job row has two: the provider first, then the model. Indexing is never
 * merely assumed, because an off-by-one here would silently assert about the
 * wrong control.
 */
function picker(row: HTMLElement, nth: number): HTMLElement {
  const found = within(row).getAllByRole('combobox')[nth];

  if (found === undefined) {
    throw new Error(`Expected at least ${nth + 1} selects in this row.`);
  }

  return found;
}

beforeEach(() => {
  assistance.reset();
});

describe('the assistant settings page', () => {
  it('lists every provider, so adding one is filling a row in', async () => {
    serverAnswers({ ...configured, connections: [] });

    renderWithProviders(AiPage);
    await settle();

    // All three, none connected. The alternative — a list of what is connected
    // plus an Add button — makes the empty state a dead end.
    expect(rowFor('Gemini')).toBeInTheDocument();
    expect(rowFor('OpenAI')).toBeInTheDocument();
    expect(rowFor('Ollama')).toBeInTheDocument();
    expect(screen.getAllByText('Not connected')).toHaveLength(3);
  });

  it('shows the form before the providers have said what they offer', async () => {
    // The listing is one connection per provider to a company somewhere else.
    // Holding the screen blank for it is the delay this guards against.
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

    // Everything but the model pickers is already usable.
    expect(rowFor('Gemini')).toBeInTheDocument();
    expect(screen.getAllByText('Asking the provider…').length).toBeGreaterThan(0);

    answerModels(json(offered));
    await settle();

    expect(screen.queryByText('Asking the provider…')).not.toBeInTheDocument();
    expect(
      within(picker(rowFor('Draw a picture'), 1)).getByText('Nano Banana 2')
    ).toBeInTheDocument();
  });

  it('shows several providers connected at once', async () => {
    serverAnswers();

    renderWithProviders(AiPage);
    await settle();

    // The whole point of the change: they are not interchangeable, so a
    // household wants each for what it is good at.
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

    // The first combobox in the row is the provider; the second is the model.
    const drawing = picker(rowFor('Draw a picture'), 0);
    const offered = within(drawing)
      .getAllByRole('option')
      .map((option) => option.textContent?.trim());

    // Ollama serves language and vision models and makes no pictures, so it is
    // absent here rather than selectable and then refused.
    expect(offered).toContain('Gemini');
    expect(offered).toContain('OpenAI');
    expect(offered).not.toContain('Ollama');
  });

  it('sends each job to the provider it was given', async () => {
    const fetched = serverAnswers();

    renderWithProviders(AiPage);
    await settle();

    await userEvent.click(screen.getByRole('button', { name: 'Save' }));
    await settle();

    const write = fetched.mock.calls
      .map(([input]) => input)
      .find((input): input is Request => input instanceof Request && input.method === 'PUT');

    const body = JSON.parse(await write!.clone().text());
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

    await userEvent.click(screen.getByRole('button', { name: 'Save' }));
    await settle();

    const write = fetched.mock.calls
      .map(([input]) => input)
      .find((input): input is Request => input instanceof Request && input.method === 'PUT');

    // Omitted, not empty: an empty string would take a stored key away, and
    // this is somebody saving the form for an unrelated reason.
    const body = JSON.parse(await write!.clone().text());
    expect(body.connections.every((one: { apiKey?: string }) => one.apiKey === undefined)).toBe(
      true
    );
  });

  it('offers the models its provider listed, filtered to what the job needs', async () => {
    serverAnswers();

    renderWithProviders(AiPage);
    await settle();

    // "Read a photograph" is given to Gemini, which listed one text model and
    // one image model. Only the text one can do this job.
    const reading = picker(rowFor('Read a photograph'), 1);
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

    const drawing = picker(rowFor('Draw a picture'), 1);
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

    const reading = picker(rowFor('Read a photograph'), 1);
    await userEvent.selectOptions(reading, 'gemini-3-flash-preview');

    await userEvent.click(screen.getByRole('button', { name: 'Save' }));
    await settle();

    const write = fetched.mock.calls
      .map(([input]) => input)
      .find((input): input is Request => input instanceof Request && input.method === 'PUT');

    const body = JSON.parse(await write!.clone().text());
    const read = body.uses.find((one: { capability: string }) => one.capability === 'read');

    expect(read.provider).toBe('gemini');
    expect(read.model).toBe('gemini-3-flash-preview');
  });

  it('forgets the model when the provider changes, because it belonged to the old one', async () => {
    const fetched = serverAnswers();

    renderWithProviders(AiPage);
    await settle();

    const row = rowFor('Read a photograph');
    await userEvent.selectOptions(picker(row, 1), 'gemini-3-flash-preview');
    await userEvent.selectOptions(picker(row, 0), 'openai');

    await userEvent.click(screen.getByRole('button', { name: 'Save' }));
    await settle();

    const write = fetched.mock.calls
      .map(([input]) => input)
      .find((input): input is Request => input instanceof Request && input.method === 'PUT');

    const body = JSON.parse(await write!.clone().text());
    const read = body.uses.find((one: { capability: string }) => one.capability === 'read');

    // Carrying it over would name a Gemini model at OpenAI.
    expect(read.provider).toBe('openai');
    expect(read.model).toBe('');
  });

  it('falls back to a text box for a provider that could not be listed', async () => {
    serverAnswers();

    renderWithProviders(AiPage);
    await settle();

    // Ollama did not answer, and "Improve a recipe" is given to it. One combobox
    // — the provider — and a text box for the model, which is how this worked
    // before lists existed.
    const row = rowFor('Improve a recipe');
    expect(within(row).getAllByRole('combobox')).toHaveLength(1);
    expect(within(row).getByRole('textbox')).toBeInTheDocument();
    expect(screen.getByText(/Ollama did not answer/)).toBeInTheDocument();
  });

  it('asks the providers again after a key is saved, so the lists are not stale', async () => {
    // The moment somebody most wants a list is the moment after they paste the
    // key. Nothing could be listed before it existed.
    const fetched = serverAnswers();

    renderWithProviders(AiPage);
    await settle();

    await userEvent.click(within(rowFor('Gemini')).getByRole('button', { name: 'Replace' }));
    await userEvent.type(screen.getByPlaceholderText('Paste the key'), 'a-key');
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));
    await settle();

    const listings = fetched.mock.calls
      .map(([input]) => String(input instanceof Request ? input.url : input))
      .filter((url) => url.includes('/models'));

    // Once on opening the screen, once after the save.
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

    // Text boxes everywhere is the old behaviour and still usable. Text boxes
    // everywhere with nothing saying why is what this guards against.
    expect(screen.getByText(/model lists could not be loaded/)).toBeInTheDocument();
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

    // A spend read next to a budget somebody typed: two bare numbers leave it
    // to the reader to assume they are the same kind of thing.
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

    // The two have different answers: a key is replaced, a provider is waited
    // for. Telling somebody to check a key that signs every other call in this
    // app is sending them after the wrong thing.
    expect(screen.getByText(/OpenAI refused the key/)).toBeInTheDocument();
    expect(screen.getByText(/Ollama did not answer/)).toBeInTheDocument();
  });

  it('offers the whole catalogue when nothing in it looks like what the job needs', async () => {
    // Whether a model draws is read from its name, so the filter is a guess.
    // When the guess empties a job's list, the models are offered unfiltered:
    // choosing a wrong one costs a call that fails with a clear message, and
    // hiding the model somebody is paying for costs them the feature.
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

    // Twice: once in the writing job Gemini also has, where the model belongs
    // by the filter's own reading, and once in the drawing job, where it is
    // offered only because the alternative was an empty picker.
    expect(screen.getAllByRole('option', { name: 'A model' })).toHaveLength(2);
  });

  it('tells an empty catalogue apart from one that holds nothing for this job', async () => {
    // The two look identical from a picker with nothing in it and need
    // different things done about them: one is the key, the other is ordinary.
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

    // OpenAI listed nothing at all, so its job falls back to the box.
    const empty = screen.getByPlaceholderText('draft-default');

    const hintOf = (field: HTMLElement) =>
      field.closest('.field')?.querySelector('.hint')?.textContent ?? '';

    // What it says is product copy and is not what this asserts. That it says
    // anything is: a box with no explanation beside it is a dead end nobody
    // can act on, and the usual cause — a key that may make requests but not
    // read the catalogue — is not one anybody guesses.
    expect(hintOf(empty)).not.toBe('');

    // Gemini listed a model, so its job gets a picker rather than a box.
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

    expect(screen.getByText(/Nothing leaves this machine/)).toBeInTheDocument();
  });

  it('says what leaves the server as soon as one job is hosted', async () => {
    serverAnswers();

    renderWithProviders(AiPage);
    await settle();

    expect(screen.getByText(/sent to the provider each job uses/)).toBeInTheDocument();
  });
});
