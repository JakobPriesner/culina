import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { createDraftStore } from './drafts.svelte';

/*
 * What the store can get wrong is what it makes of the stream: a draft that
 * stops growing, an ask that never stops looking busy, and — the one that
 * costs money — a second call started while the first is still running.
 */

/**
 * Stands in for the streamed response, so a test can be the assistant.
 *
 * A real `ReadableStream` behind a real `Response`, because that is what the
 * reader in `$api/events` consumes. A hand-written double of the parser would
 * only prove that the double agrees with itself.
 */
class FakeStream {
  static last: FakeStream | null = null;

  readonly response: Response;

  #push!: ReadableStreamDefaultController<Uint8Array>;
  #encoder = new TextEncoder();

  constructor() {
    const body = new ReadableStream<Uint8Array>({
      start: (controller) => {
        this.#push = controller;
      }
    });

    this.response = new Response(body, {
      status: 200,
      headers: { 'Content-Type': 'text/event-stream' }
    });

    FakeStream.last = this;
  }

  /** One event, framed the way the server frames it. */
  send(event: {
    draft: Partial<Draft>;
    finished?: boolean;
    problem?: { code: string; detail: string };
  }) {
    const data = JSON.stringify({
      finished: false,
      ...event,
      draft: { draftId: 'd1', groups: [], steps: [], tags: [], ...event.draft }
    });

    this.#push.enqueue(this.#encoder.encode(`data: ${data}\n\n`));
  }

  /** The connection dying, which is not the same as the recipe finishing. */
  cut() {
    this.#push.close();
  }
}

type Draft = {
  draftId: string;
  title?: string;
  groups: { name: string | null; ingredients: { name: string }[] }[];
  steps: { text: string }[];
  tags: string[];
};

let asked: { url: string; method: string; type: string | null; body: unknown }[] = [];

function assistant(reply: () => Response = () => new FakeStream().response) {
  vi.stubGlobal(
    'fetch',
    vi.fn((input: Request | string, init?: RequestInit) => {
      const headers = new Headers(init?.headers);

      asked.push({
        url: typeof input === 'string' ? input : input.url,
        method: typeof input === 'string' ? (init?.method ?? 'GET') : input.method,
        type: headers.get('Content-Type'),
        body: init?.body
      });

      return Promise.resolve(reply());
    })
  );
}

const problem = (code: string, status: number) =>
  new Response(JSON.stringify({ code, detail: 'No.' }), {
    status,
    headers: { 'Content-Type': 'application/problem+json' }
  });

/** Lets the reader's promises run before a test looks at what they did. */
const opened = () => vi.waitFor(() => expect(FakeStream.last).not.toBeNull());

const anIdea = { kind: 'idea' as const, householdId: 'h1', material: 'aubergines', language: 'en' };

describe('the draft store', () => {
  beforeEach(() => {
    asked = [];
    FakeStream.last = null;
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('shows the recipe growing rather than only the finished one', async () => {
    assistant();

    const drafts = createDraftStore();
    const finished = drafts.ask(anIdea);

    await opened();

    FakeStream.last!.send({ draft: { title: 'Auberginenauflauf' } });

    // The title lands seconds before the steps do, and that is the point: a
    // person reads it and decides, rather than watching a spinner.
    await vi.waitFor(() => expect(drafts.draft?.title).toBe('Auberginenauflauf'));
    expect(drafts.asking).toBe(true);

    FakeStream.last!.send({
      draft: {
        title: 'Auberginenauflauf',
        groups: [{ name: null, ingredients: [{ name: 'Aubergine' }] }]
      }
    });

    await vi.waitFor(() => expect(drafts.draft?.groups[0]?.ingredients).toHaveLength(1));

    FakeStream.last!.send({
      draft: {
        title: 'Auberginenauflauf',
        groups: [{ name: null, ingredients: [{ name: 'Aubergine' }] }],
        steps: [{ text: 'Alles backen' }]
      },
      finished: true
    });

    expect(await finished).toBeNull();
    expect(drafts.asking).toBe(false);
    expect(drafts.draft?.steps).toHaveLength(1);
  });

  it('reports a failure the assistant sent after it had already started answering', async () => {
    assistant();

    const drafts = createDraftStore();
    const finished = drafts.ask(anIdea);

    await opened();

    FakeStream.last!.send({
      draft: { title: 'Auberginen' },
      finished: true,
      problem: { code: 'assistance.unavailable', detail: 'Not just now.' }
    });

    const failure = await finished;

    // It cannot be a status code: the 200 went out with the first event.
    expect(failure?.code).toBe('assistance.unavailable');
    expect(drafts.error?.detail).toBe('Not just now.');

    // What was written before it stopped is kept. It was paid for, and half a
    // recipe is a starting point.
    expect(drafts.draft?.title).toBe('Auberginen');
  });

  it('stops looking busy when the connection dies mid-recipe', async () => {
    assistant();

    const drafts = createDraftStore();
    const finished = drafts.ask(anIdea);

    await opened();

    FakeStream.last!.send({ draft: { title: 'Auberginen' } });
    FakeStream.last!.cut();

    // A body that ends without saying it finished is a tunnel that collapsed.
    // Without this the screen writes forever.
    const failure = await finished;

    expect(failure?.code).toBe('client.offline');
    expect(drafts.asking).toBe(false);
  });

  it('keeps a refusal to start as an ordinary failure', async () => {
    assistant(() => problem('assistance.budget_exhausted', 429));

    const drafts = createDraftStore();

    const failure = await drafts.ask(anIdea);

    // Everything that can refuse the ask is decided before the stream opens,
    // so the budget still answers with a status code somebody can act on.
    expect(failure?.code).toBe('assistance.budget_exhausted');
    expect(failure?.status).toBe(429);
    expect(drafts.asking).toBe(false);
  });

  it('ignores a second ask while one is running, because the second one costs', async () => {
    assistant();

    const drafts = createDraftStore();
    const finished = drafts.ask(anIdea);

    await opened();

    await drafts.ask(anIdea);

    expect(asked).toHaveLength(1);

    FakeStream.last!.send({ draft: { title: 'Auberginen' }, finished: true });

    await finished;
  });

  it('says its body is JSON, or the endpoint is not there at all', async () => {
    assistant();

    const drafts = createDraftStore();
    const finished = drafts.ask({ ...anIdea, kind: 'revision', recipeId: 'r1' });

    await opened();

    // Without this `fetch` labels the body text/plain, the endpoint's JSON
    // binding stops matching the route, and the answer is a 404 about an
    // endpoint that plainly exists — which on screen is a button that spends
    // a few seconds looking busy and then does nothing.
    expect(asked[0]?.type).toBe('application/json');
    expect(JSON.parse(String(asked[0]?.body))).toMatchObject({
      kind: 'revision',
      recipeId: 'r1'
    });

    FakeStream.last!.send({ draft: { title: 'Auberginen' }, finished: true });

    await finished;
  });

  it('posts a photograph to its own route, as multipart', async () => {
    assistant();

    const drafts = createDraftStore();
    const finished = drafts.read({
      householdId: 'h1',
      language: 'de',
      file: new File(['x'], 'page.jpg', { type: 'image/jpeg' })
    });

    await opened();

    expect(asked[0]?.method).toBe('POST');
    expect(asked[0]?.url).toContain('/api/v1/recipe-drafts/photographs?householdId=h1&language=de');

    // Nothing set here on purpose: the browser writes its own multipart type
    // with the boundary in it, and a Content-Type of ours would replace that
    // with one the server cannot split.
    expect(asked[0]?.type).toBeNull();

    FakeStream.last!.send({ draft: { title: 'Linsensuppe' }, finished: true });

    expect(await finished).toBeNull();
    expect(drafts.draft?.title).toBe('Linsensuppe');
  });
});
