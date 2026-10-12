/* The worker's side of the offline reads in lib/api/offlineReads.ts: same-origin, kept alive by the fetch event. */
import { base } from '$service-worker';
import { offlineReads, type CachePolicy } from '../lib/api/offlineReads';
import { forgetKitchen } from '../lib/features/cooking/timerState';

export type { CachePolicy };

const reads = offlineReads({ base, locate: (path) => path, signedOut: forgetKitchen });

export const isApi = reads.isApi;

export const policyFor = reads.policyFor;

/** Answers a read from the worker; must never throw, since a rejection reaches the page as "Failed to fetch". */
export function apiResponse(event: FetchEvent, policy: CachePolicy): Promise<Response> {
  return reads.answer(event.request, policy, (work) => event.waitUntil(work));
}
