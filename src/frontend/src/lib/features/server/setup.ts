import { http, request } from '$api';

import type { Setup, SetupStage } from './types';

const stages: readonly SetupStage[] = ['database', 'account', 'complete'];

/**
 * Setup stage of this instance, or null if the server couldn't be asked. Null is not "complete":
 * an offline phone must not be pushed onto setup for an instance set up years ago.
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
 * Waits until the server answers as a host started after `since`: the old host still answers with the old
 * `startedAt`, then nothing (502), so only a new one means new settings. Null if it did not return in time.
 */
export async function waitForRestart(since: string): Promise<Setup | null> {
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
