import { http, request } from '$api';

import type { Setup, SetupStage } from './types';

const stages: readonly SetupStage[] = ['database', 'account', 'complete'];

/**
 * How far this instance has got in being set up, or null when the server
 * could not be asked.
 *
 * Null is deliberately not "complete". A guard that cannot tell sends nobody
 * anywhere — an offline phone must not be pushed onto a setup screen for an
 * instance that was set up years ago.
 */
export async function readSetup(): Promise<Setup | null> {
  const result = await request(() => http.GET('/api/v1/setup'));

  if (!result.ok) {
    return null;
  }

  const stage = stages.find((known) => known === result.value.stage) ?? 'complete';

  return { stage, startedAt: result.value.startedAt };
}

/** How long a restart may take before the screen says something is wrong. */
const patienceMs = 90_000;

/** Often enough that a two-second restart feels like two seconds. */
const intervalMs = 500;

/**
 * Waits for the server to come back from a restart: until it answers, and
 * answers as a host that started after `since`.
 *
 * Both halves matter. Right after asking, the old host is still finishing its
 * last requests and answers perfectly well — with the old `startedAt`. Then
 * for a moment nothing answers at all, and the development proxy says 502.
 * Only a different `startedAt` means the new settings are in use.
 *
 * Resolves with the new host's setup stage, or null if it did not come back in
 * time.
 */
export async function waitForRestart(since: string | null): Promise<Setup | null> {
  const deadline = Date.now() + patienceMs;

  while (Date.now() < deadline) {
    await new Promise((resolve) => setTimeout(resolve, intervalMs));

    const setup = await readSetup();

    if (setup && setup.startedAt !== since) {
      return setup;
    }
  }

  return null;
}
