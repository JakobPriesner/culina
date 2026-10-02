import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { clientError, ErrorCodes } from '$api';

import { createAutosave } from './autosave.svelte';

/*
 * There is no Save button, so these are the guarantees that replace one: work
 * is not lost, and two saves cannot overtake each other.
 */
const offline = clientError(ErrorCodes.offline, 'No connection.');

beforeEach(() => vi.useFakeTimers());
afterEach(() => vi.useRealTimers());

describe('typing', () => {
  it('saves once after the typing stops, not once per keystroke', async () => {
    const save = vi.fn(async () => null);
    const autosave = createAutosave(save);

    autosave.touch();
    autosave.touch();
    autosave.touch();

    expect(save).not.toHaveBeenCalled();

    await vi.advanceTimersByTimeAsync(1000);

    expect(save).toHaveBeenCalledOnce();
    expect(autosave.state).toBe('saved');
  });

  it('reports a failure without pretending it saved', async () => {
    const autosave = createAutosave(async () => ({
      code: 'recipes.invalid',
      detail: 'No.',
      status: 400,
      requestId: null,
      fields: [],
      retryAfterSeconds: null
    }));

    autosave.touch();
    await vi.advanceTimersByTimeAsync(1000);

    expect(autosave.state).toBe('failed');
    expect(autosave.failure?.code).toBe('recipes.invalid');
  });
});

describe('a change made while a save is running', () => {
  it('is sent afterwards rather than dropped', async () => {
    const { promise, resolve } = Promise.withResolvers<null>();
    const save = vi.fn().mockReturnValueOnce(promise).mockResolvedValue(null);
    const autosave = createAutosave(save);

    autosave.touch();
    await vi.advanceTimersByTimeAsync(1000);

    // Typing continues while the first save is still in the air.
    autosave.touch();
    await vi.advanceTimersByTimeAsync(1000);

    expect(save).toHaveBeenCalledOnce();

    resolve(null);
    await vi.advanceTimersByTimeAsync(0);

    // The second save runs after the first, never alongside it, so the version
    // it sends is the one the first produced.
    expect(save).toHaveBeenCalledTimes(2);
  });
});

describe('leaving the page', () => {
  it('sends what is owed immediately', async () => {
    const save = vi.fn(async () => null);
    const autosave = createAutosave(save);

    autosave.touch();
    await autosave.flush();

    expect(save).toHaveBeenCalledOnce();
  });

  it('sends nothing when nothing was typed', async () => {
    const save = vi.fn(async () => null);
    const autosave = createAutosave(save);

    await autosave.flush();

    expect(save).not.toHaveBeenCalled();
  });

  it('sends nothing the server already has', async () => {
    const save = vi.fn(async () => null);
    const autosave = createAutosave(save);

    autosave.touch();
    await vi.advanceTimersByTimeAsync(1000);
    await autosave.flush();

    expect(save).toHaveBeenCalledOnce();
  });

  it('tries again what a failed save did not deliver', async () => {
    const save = vi.fn().mockResolvedValueOnce(offline).mockResolvedValue(null);
    const autosave = createAutosave(save);

    autosave.touch();
    await vi.advanceTimersByTimeAsync(1000);
    await autosave.flush();

    expect(save).toHaveBeenCalledTimes(2);
  });

  it('owes nothing once a failure has been cleared', async () => {
    const save = vi.fn(async () => offline);
    const autosave = createAutosave(save);

    autosave.touch();
    await vi.advanceTimersByTimeAsync(1000);
    autosave.clear();
    await autosave.flush();

    expect(save).toHaveBeenCalledOnce();
  });

  it('fires nothing after it is disposed', async () => {
    const save = vi.fn(async () => null);
    const autosave = createAutosave(save);

    autosave.touch();
    autosave.dispose();
    await vi.advanceTimersByTimeAsync(5000);

    expect(save).not.toHaveBeenCalled();
  });
});
