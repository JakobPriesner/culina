import { m } from '$shell/i18n';

import {
  providersFor,
  type Assistance,
  type Capability,
  type Connection,
  type Provider,
  type ProviderModels,
  type Use
} from '../types';
import { providerName } from './labels';

export interface Choice {
  value: string;
  label: string;
}

/** Providers this job could use, plus "not offered". */
export const providerChoices = (capability: Capability): Choice[] => [
  { value: '', label: m['ai.job.none']() },
  ...providersFor(capability).map((provider) => ({
    value: provider,
    label: providerName(provider)
  }))
];

const offeredBy = (models: readonly ProviderModels[], provider: Provider | '') =>
  provider === '' ? undefined : models.find((one) => one.provider === provider);

/**
 * Models this job may use, filtered by draw/non-draw. The filter is a convenience, not a gate:
 * `canDraw` is guessed from model names, so an empty match falls back to the whole catalogue.
 * Null means no list can be built (still loading, unreachable, empty); the UI then offers a text box.
 */
export function modelsFor(
  models: readonly ProviderModels[],
  capability: Capability,
  use: Use
): Choice[] | null {
  const listed = offeredBy(models, use.provider);

  if (!listed?.reachable || listed.models.length === 0) {
    return null;
  }

  const wanted = listed.models.filter((model) =>
    capability === 'draw' ? model.canDraw : !model.canDraw
  );

  const offered = wanted.length > 0 ? wanted : listed.models;

  return [
    { value: '', label: `${m['ai.job.model.any']()} — ${use.defaultModel}` },
    ...offered.map((model) => ({ value: model.id, label: model.label }))
  ];
}

/** Whether the whole catalogue is offered because no model matches the job's kind. */
export function offersWholeCatalogue(
  models: readonly ProviderModels[],
  capability: Capability,
  use: Use
): boolean {
  const listed = offeredBy(models, use.provider);

  return (
    listed?.reachable === true &&
    listed.models.length > 0 &&
    !listed.models.some((model) => (capability === 'draw' ? model.canDraw : !model.canDraw))
  );
}

/** What is wrong with a provider's model list; shown once per provider since it affects every job. */
export function catalogueProblem(
  models: readonly ProviderModels[],
  provider: Provider
): string | null {
  const listed = offeredBy(models, provider);
  const name = providerName(provider);

  if (!listed) {
    return null;
  }

  // A refused key and a down provider need different advice: keys are scoped, and
  // reading the catalogue is its own permission.
  if (!listed.reachable) {
    return listed.problem === 'assistance.rejected'
      ? m['ai.models.rejected']({ provider: name })
      : m['ai.models.unreachable']({ provider: name });
  }

  return listed.models.length === 0 ? m['ai.models.emptyCatalogue']({ provider: name }) : null;
}

/** Whether the provider's list problem is already shown beside it. */
export const unlistedByProvider = (models: readonly ProviderModels[], use: Use): boolean =>
  use.provider !== '' && catalogueProblem(models, use.provider) !== null;

/** Whether the address changed while a stored key is kept; the server only sends a key to its saved address. */
export function keyNeededAgain(saved: Assistance | null, edited: Connection): boolean {
  const before = saved?.connections.find((one) => one.provider === edited.provider);

  return (
    before?.apiKeyConfigured === true &&
    edited.apiKey === undefined &&
    edited.baseUrl.trim() !== before.baseUrl
  );
}

/** A budget as typed: blank or not a number means no limit. */
export function budget(value: string): number | null {
  const parsed = Number(value.replace(',', '.'));

  return value.trim().length === 0 || Number.isNaN(parsed) ? null : parsed;
}
