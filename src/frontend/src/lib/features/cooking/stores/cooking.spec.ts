import { beforeEach, describe, expect, it, vi } from 'vitest';

import { cooking } from './cooking.svelte';

/*
 * One session at a time, and a step advance that feels instant. Those are the
 * two things the cook actually experiences.
 */
const session = (over: Record<string, unknown> = {}) => ({
  sessionId: 's1',
  recipeId: 'r1',
  recipeTitle: 'Lemon orzo',
  servings: 4,
  currentStepIndex: 0,
  startedAt: '2026-09-12T12:00:00Z',
  lastActiveAt: '2026-09-12T12:00:00Z',
  version: 1,
  ...over
});

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': status >= 400 ? 'application/problem+json' : 'application/json' }
  });

let sent: Request[];

function serverAnswers(reply: (request: Request) => Response | Promise<Response>) {
  sent = [];
  vi.stubGlobal(
    'fetch',
    vi.fn((input: Request) => {
      sent.push(input);

      return Promise.resolve(reply(input));
    })
  );
}

beforeEach(() => {
  cooking.reset();
  serverAnswers(() => json(session()));
});

describe('resuming on boot', () => {
  it('holds whatever is being cooked', async () => {
    await cooking.resume();

    expect(cooking.session?.recipeTitle).toBe('Lemon orzo');
    expect(cooking.resolved).toBe(true);
  });

  it('says so plainly when nothing is', async () => {
    serverAnswers(() => json({ code: 'cooking.session_not_found', detail: 'No.' }, 404));

    await cooking.resume();

    expect(cooking.session).toBeNull();
    // Resolved either way: the bar has to know the difference between "nothing
    // is cooking" and "we have not asked yet".
    expect(cooking.resolved).toBe(true);
  });
});

describe('moving through the steps', () => {
  it('moves immediately, before the server has answered', async () => {
    await cooking.resume();

    cooking.moveTo('r1', 2);

    // Not awaited: tapping next must feel instant, and the server's answer
    // changes nothing the cook can see.
    expect(cooking.session?.currentStepIndex).toBe(2);
  });

  it('collapses a flurry of taps into the step they landed on', async () => {
    const { promise, resolve } = Promise.withResolvers<Response>();
    let first = true;

    serverAnswers((request) => {
      if (request.method === 'PATCH' && first) {
        first = false;

        return promise;
      }

      return json(session());
    });

    await cooking.resume();

    cooking.moveTo('r1', 1);
    cooking.moveTo('r1', 2);
    cooking.moveTo('r1', 3);

    resolve(json(session({ currentStepIndex: 1 })));
    await vi.waitFor(() => expect(sent.filter((one) => one.method === 'PATCH')).toHaveLength(2));

    // Two requests for three taps, and the second carries where they actually
    // are — four requests would arrive out of order and land them elsewhere.
    const last = sent.filter((one) => one.method === 'PATCH').at(-1)!;

    expect(await last.json()).toEqual({ currentStepIndex: 3 });
  });

  it('never moves the session of a different recipe', async () => {
    await cooking.resume();

    cooking.moveTo('another-recipe', 2);

    expect(cooking.session?.currentStepIndex).toBe(0);
    expect(sent.filter((one) => one.method === 'PATCH')).toHaveLength(0);
  });
});

describe('rescaling mid-cook', () => {
  it('keeps the new amount when it saves', async () => {
    await cooking.resume();

    serverAnswers(() => json(session({ servings: 6, version: 2 })));
    await cooking.rescale(6);

    expect(cooking.session?.servings).toBe(6);
  });

  it('puts the old amount back when it does not', async () => {
    await cooking.resume();

    serverAnswers(() => json({ code: 'cooking.session_finished', detail: 'Over.' }, 409));
    await cooking.rescale(6);

    // An amount that silently failed to save is worse than one that visibly
    // did not change.
    expect(cooking.session?.servings).toBe(4);
  });
});

describe('finishing', () => {
  it('leaves nothing cooking', async () => {
    await cooking.resume();

    serverAnswers(() => new Response(null, { status: 204 }));
    await cooking.end(true);

    expect(cooking.session).toBeNull();
  });
});
