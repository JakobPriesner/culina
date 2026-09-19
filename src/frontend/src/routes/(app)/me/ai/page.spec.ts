import { screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import AiPage from './+page.svelte';
import { assistance } from '$features/assistance/stores/assistance.svelte';
import { renderWithProviders } from '$lib/test/render';

/*
 * The page rather than the store, because the two things worth proving here are
 * both about what is on screen: that the API key is never in a field somebody
 * could read it out of, and that choosing Ollama removes the controls that do
 * not apply to it. Neither is visible from the store.
 */
const connected = {
  enabled: true,
  provider: 'openai',
  apiKeyConfigured: true,
  connected: true,
  baseUrl: '',
  composeModel: 'gpt-4o-mini',
  drawModel: 'gpt-image-1',
  improveEnabled: true,
  draftEnabled: true,
  readEnabled: true,
  drawEnabled: true,
  monthlyBudget: 20,
  personalBudget: null
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

function serverAnswers(settings: object = connected) {
  const json = (body: object) =>
    new Response(JSON.stringify(body), {
      status: 200,
      headers: { 'Content-Type': 'application/json' }
    });

  const fetched = vi.fn((input: unknown) => {
    const url = String(input instanceof Request ? input.url : input);

    return Promise.resolve(url.includes('/usage') ? json(emptyUsage) : json(settings));
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

beforeEach(() => {
  assistance.reset();
});

describe('the assistant settings page', () => {
  it('never puts the API key in a field, only says that there is one', async () => {
    serverAnswers();

    renderWithProviders(AiPage);
    await settle();

    // The key is not in the response at all, so a box rendered empty would read
    // as "no key" and saving would look like it had wiped one.
    expect(screen.getByText('A key is set.')).toBeInTheDocument();
    expect(screen.queryByPlaceholderText('Paste the key')).not.toBeInTheDocument();
  });

  it('asks for a key only once somebody says they want to replace it', async () => {
    serverAnswers();

    renderWithProviders(AiPage);
    await settle();

    await userEvent.click(screen.getByRole('button', { name: 'Replace' }));

    expect(screen.getByPlaceholderText('Paste the key')).toBeInTheDocument();
  });

  it('drops the key and the drawing controls when the model runs on your own machine', async () => {
    serverAnswers();

    renderWithProviders(AiPage);
    await settle();

    await userEvent.click(screen.getByRole('button', { name: 'Ollama' }));

    // Absent, not disabled. Ollama has nobody to authenticate to and does not
    // make pictures, so a key row and a drawing switch would both be controls
    // that can never do anything.
    expect(screen.queryByText('A key is set.')).not.toBeInTheDocument();
    expect(screen.queryByRole('switch', { name: 'Draw a picture' })).not.toBeInTheDocument();
    expect(screen.getByRole('switch', { name: 'Improve a recipe' })).toBeInTheDocument();
  });

  it('says what leaves the server, and says it differently for a local model', async () => {
    serverAnswers();

    renderWithProviders(AiPage);
    await settle();

    expect(screen.getByText(/sent to OpenAI to be read/)).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Ollama' }));

    expect(screen.getByText(/Nothing leaves this machine/)).toBeInTheDocument();
  });

  it('sends no key at all when the form is saved without touching it', async () => {
    const fetched = serverAnswers();

    renderWithProviders(AiPage);
    await settle();

    await userEvent.click(screen.getByRole('button', { name: 'Save' }));
    await settle();

    const write = fetched.mock.calls
      .map(([input]) => input)
      .find((input): input is Request => input instanceof Request && input.method === 'PUT');

    expect(write).toBeDefined();

    // Omitted, not empty: an empty string would take the stored key away, and
    // this is somebody saving the form for an unrelated reason.
    const body = JSON.parse(await write!.clone().text());
    expect(body.apiKey).toBeUndefined();
  });
});
