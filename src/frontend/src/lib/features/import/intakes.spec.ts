import { beforeEach, describe, expect, it, vi } from 'vitest';

import { watch } from '$api';

import { intakes, type IntakeJob } from './intakes.svelte';

vi.mock('$api', async () => ({
  ...(await vi.importActual<object>('$api')),
  watch: vi.fn()
}));

type Handlers = {
  message: (event: { snapshot: boolean; jobs: IntakeJob[] }) => void;
  failed: (error: unknown) => void;
};

const job = (id: string, stage: string) =>
  ({ id, stage, householdId: 'h', createdAt: '2026-01-01T00:00:00Z' }) as IntakeJob;

let handlers: Handlers[];
let close: ReturnType<typeof vi.fn>;
const latest = () => handlers[handlers.length - 1]!;

beforeEach(() => {
  handlers = [];
  close = vi.fn();
  vi.mocked(watch).mockImplementation(((_path: string, given: Handlers) => {
    handlers.push(given);
    return { close };
  }) as never);
  intakes.own(null);
  intakes.own('person');
});

describe('the intake stream', () => {
  it('opens one stream and applies the snapshot', () => {
    intakes.follow();
    intakes.follow();

    expect(watch).toHaveBeenCalledTimes(1);
    expect(vi.mocked(watch).mock.calls[0]?.[0]).toBe('/api/v1/recipe-intakes/events');

    latest().message({ snapshot: true, jobs: [job('a', 'reading'), job('b', 'ready')] });

    expect(intakes.jobs.map((one) => one.id)).toEqual(['a', 'b']);
  });

  it('applies a change, adds a new job first, and drops a reviewed one', () => {
    intakes.follow();
    latest().message({ snapshot: true, jobs: [job('a', 'reading'), job('b', 'ready')] });

    latest().message({ snapshot: false, jobs: [job('a', 'writing')] });
    expect(intakes.jobs.map((one) => `${one.id}:${one.stage}`)).toEqual(['a:writing', 'b:ready']);

    latest().message({ snapshot: false, jobs: [job('c', 'queued')] });
    expect(intakes.jobs.map((one) => one.id)).toEqual(['c', 'a', 'b']);

    latest().message({ snapshot: false, jobs: [job('b', 'reviewed')] });
    expect(intakes.jobs.map((one) => one.id)).toEqual(['c', 'a']);
  });

  it('leaves the list alone for a heartbeat', () => {
    intakes.follow();
    latest().message({ snapshot: true, jobs: [job('a', 'reading')] });
    const before = intakes.jobs;

    latest().message({ snapshot: false, jobs: [] });

    expect(intakes.jobs).toBe(before);
  });

  it('replaces the list with the snapshot of a reconnect', () => {
    intakes.follow();
    latest().message({ snapshot: true, jobs: [job('a', 'reading')] });

    latest().message({ snapshot: true, jobs: [job('a', 'ready'), job('b', 'failed')] });

    expect(intakes.jobs.map((one) => `${one.id}:${one.stage}`)).toEqual(['a:ready', 'b:failed']);
  });

  it('opens again after the stream gave up', () => {
    intakes.follow();
    latest().failed({ code: 'network.offline' });

    expect(intakes.error).not.toBeNull();

    intakes.follow();

    expect(watch).toHaveBeenCalledTimes(2);
  });

  it('closes when stopped or when the owner changes, and ignores a late event', () => {
    intakes.follow();
    const first = latest();

    intakes.own('someone else');

    expect(close).toHaveBeenCalledTimes(1);

    first.message({ snapshot: true, jobs: [job('a', 'reading')] });

    expect(intakes.jobs).toEqual([]);
  });

  it('does not open without an owner', () => {
    intakes.own(null);
    intakes.follow();

    expect(watch).not.toHaveBeenCalled();
  });
});
