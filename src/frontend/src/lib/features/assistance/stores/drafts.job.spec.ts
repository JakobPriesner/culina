import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { http, request, watch } from '$api';

import { createDraftStore } from './drafts.svelte';

/* An idea or a revision as a server job (docs/native-app-design.md, part 8): what the store must get right is following
   its own job, letting go of the stream while nobody looks, and finding the job again after the app was closed. */

vi.mock('$api', async () => ({
  ...(await vi.importActual<object>('$api')),
  watch: vi.fn(),
  request: vi.fn(),
  http: { PUT: vi.fn(), POST: vi.fn() }
}));

type Job = {
  id: string;
  stage: string;
  householdId: string;
  createdAt: string;
  purpose?: string;
  draft?: {
    draftId: string;
    title?: string;
    groups: never[];
    steps: never[];
    tags: never[];
  } | null;
  material?: string | null;
  errorCode?: string | null;
};

type Handlers = {
  message: (event: { snapshot: boolean; jobs: Job[] }) => void;
  failed: (error: unknown) => void;
};

let streams: { path: string; handlers: Handlers; close: ReturnType<typeof vi.fn> }[];
let started: unknown;
const latest = () => streams[streams.length - 1]!;

const job = (id: string, stage: string, extra: Partial<Job> = {}): Job => ({
  id,
  stage,
  householdId: 'h1',
  createdAt: '2026-10-14T00:00:00Z',
  purpose: 'draft',
  ...extra
});

const titled = (title: string) => ({ draftId: 'd1', title, groups: [], steps: [], tags: [] });

const anIdea = { kind: 'idea' as const, householdId: 'h1', material: 'aubergines', language: 'en' };

/** The id the store picked for its job, from what it asked the server. */
const askedId = () =>
  (vi.mocked(http.PUT).mock.calls[0]?.[1] as unknown as { params: { path: { id: string } } }).params
    .path.id;

function hide(hidden: boolean) {
  vi.spyOn(document, 'visibilityState', 'get').mockReturnValue(hidden ? 'hidden' : 'visible');
  document.dispatchEvent(new Event('visibilitychange'));
}

beforeEach(() => {
  localStorage.clear();
  streams = [];
  started = undefined;
  vi.mocked(watch).mockImplementation(((path: string, handlers: Handlers) => {
    const close = vi.fn();
    streams.push({ path, handlers, close });
    return { close };
  }) as never);
  vi.mocked(http.PUT).mockResolvedValue({} as never);
  vi.mocked(http.POST).mockResolvedValue({} as never);
  vi.mocked(request).mockImplementation((async (call: () => Promise<unknown>) => {
    await call();
    return started ?? { ok: true, value: job(askedId(), 'queued') };
  }) as never);
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe('a draft written as a server job', () => {
  it('is asked for under an id of its own, and grows on the stream that attends it', async () => {
    const drafts = createDraftStore();
    const finished = drafts.ask(anIdea);

    await vi.waitFor(() => expect(streams).toHaveLength(1));
    const id = askedId();

    expect(vi.mocked(http.PUT).mock.calls[0]?.[0]).toBe('/api/v1/recipe-drafts/{id}');
    expect(latest().path).toBe(`/api/v1/recipe-intakes/events?attending=${id}`);
    expect(drafts.concerns('idea')).toBe(true);

    latest().handlers.message({
      snapshot: true,
      jobs: [job(id, 'writing', { draft: titled('Auberginen') })]
    });
    expect(drafts.draft?.title).toBe('Auberginen');
    expect(drafts.asking).toBe(true);

    latest().handlers.message({
      snapshot: false,
      jobs: [
        job('another', 'ready', { draft: titled('Not mine') }),
        job(id, 'ready', { draft: titled('Auberginenauflauf') })
      ]
    });

    expect(await finished).toBeNull();
    expect(drafts.asking).toBe(false);
    expect(drafts.draft?.title).toBe('Auberginenauflauf');
    expect(latest().close).toHaveBeenCalled();
  });

  it('says why a job failed, keeping what was written', async () => {
    const drafts = createDraftStore();
    const finished = drafts.ask(anIdea);
    await vi.waitFor(() => expect(streams).toHaveLength(1));

    latest().handlers.message({
      snapshot: true,
      jobs: [
        job(askedId(), 'failed', { draft: titled('Auber'), errorCode: 'assistance.unavailable' })
      ]
    });

    expect((await finished)?.code).toBe('assistance.unavailable');
    expect(drafts.draft?.title).toBe('Auber');
  });

  it('keeps a refusal to start as an ordinary failure, with nothing to follow', async () => {
    started = {
      ok: false,
      error: { code: 'assistance.not_configured', detail: 'No.', status: 404 }
    };
    const drafts = createDraftStore();

    const failure = await drafts.ask(anIdea);

    expect(failure?.code).toBe('assistance.not_configured');
    expect(drafts.asking).toBe(false);
    expect(streams).toHaveLength(0);
    expect(localStorage.getItem('culina.drafts.job')).toBeNull();
  });

  it('lets go of the stream while the page is hidden, so the server says when it is ready, and follows it again on return', async () => {
    const drafts = createDraftStore();
    const finished = drafts.ask(anIdea);
    await vi.waitFor(() => expect(streams).toHaveLength(1));
    const id = askedId();

    hide(true);
    expect(streams[0]!.close).toHaveBeenCalled();
    expect(drafts.asking).toBe(true);

    hide(false);
    expect(streams).toHaveLength(2);
    latest().handlers.message({
      snapshot: true,
      jobs: [job(id, 'ready', { draft: titled('Auberginenauflauf') })]
    });

    expect(await finished).toBeNull();
    expect(drafts.draft?.title).toBe('Auberginenauflauf');
  });

  it('is found again by a fresh start of the app, words and all', async () => {
    const before = createDraftStore();
    void before.ask(anIdea);
    await vi.waitFor(() => expect(streams).toHaveLength(1));
    const id = askedId();

    // The app was closed: a new store, and the page that asked for it opening again.
    const after = createDraftStore();
    const resumed = after.resume('idea');

    expect(resumed).not.toBeNull();
    expect(latest().path).toBe(`/api/v1/recipe-intakes/events?attending=${id}`);
    latest().handlers.message({
      snapshot: true,
      jobs: [job(id, 'ready', { draft: titled('Auberginenauflauf'), material: 'aubergines' })]
    });

    expect(await resumed).toBeNull();
    expect(after.draft?.title).toBe('Auberginenauflauf');
    expect(after.material).toBe('aubergines');
    expect(after.resume('revision', 'r1')).toBeNull();
  });

  it('belongs to the recipe it rewrites, and nowhere else', async () => {
    const drafts = createDraftStore();
    void drafts.ask({ ...anIdea, kind: 'revision', recipeId: 'r1' });
    await vi.waitFor(() => expect(streams).toHaveLength(1));

    expect(drafts.concerns('revision', 'r1')).toBe(true);
    expect(drafts.concerns('revision', 'r2')).toBe(false);
    expect(drafts.concerns('idea')).toBe(false);
    expect(createDraftStore().remembers('revision', 'r1')).toBe(true);
    expect(createDraftStore().remembers('revision', 'r2')).toBe(false);
  });

  it('is dismissed on the server too, which stops one still being written', async () => {
    const drafts = createDraftStore();
    const finished = drafts.ask(anIdea);
    await vi.waitFor(() => expect(streams).toHaveLength(1));
    const id = askedId();

    drafts.dismiss();

    expect(await finished).toBeNull();
    expect(vi.mocked(http.POST).mock.calls[0]?.[0]).toBe('/api/v1/recipe-intakes/{id}/reviewed');
    expect(vi.mocked(http.POST).mock.calls[0]?.[1]).toEqual({ params: { path: { id } } });
    expect(drafts.concerns('idea')).toBe(false);
    expect(createDraftStore().resume('idea')).toBeNull();
  });

  it('is let go of quietly when it was dismissed elsewhere', async () => {
    const drafts = createDraftStore();
    const finished = drafts.ask(anIdea);
    await vi.waitFor(() => expect(streams).toHaveLength(1));

    latest().handlers.message({ snapshot: true, jobs: [] });

    expect(await finished).toBeNull();
    expect(drafts.draft).toBeNull();
    expect(drafts.concerns('idea')).toBe(false);
  });
});
