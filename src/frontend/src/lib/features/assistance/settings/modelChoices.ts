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

/** One entry of a picker. */
export interface Choice {
  value: string;
  label: string;
}

/** The providers this job could be given to, plus "not offered". */
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
 * The models this job may be given, as the provider listed them.
 *
 * Filtered by what the job needs: drawing sees only the models that draw, and
 * the other three see only the ones that do not. An empty first entry keeps
 * "whatever Culina currently defaults to" reachable, which is what most
 * instances should stay on.
 *
 * The filter is a convenience and never a gate. Whether a model draws is the
 * adapter reading its name, because no provider states it — so when that
 * reading leaves a job with nothing to choose from, the whole catalogue is
 * offered instead of an empty list. Being wrong costs a call that fails with
 * a clear message; hiding the model somebody is paying for costs them the
 * feature, and no message at all explains where it went.
 *
 * Null means there is nothing to build a list from at all: the providers are
 * still being asked, this one did not answer, or it answered with an empty
 * catalogue. Those end in the text box, because a name can always be typed.
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

/**
 * Whether a job is offered the provider's whole catalogue, because none of
 * it announced itself as the kind this job needs.
 *
 * Ordinary, and said under the picker so nobody wonders why a writing model
 * is offered to the job that draws: a provider can offer forty models and
 * none that draws.
 */
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

/**
 * What is wrong with a provider's list of models, said once, beside the
 * provider.
 *
 * It is a fact about the connection, not about any one job: a key that may
 * not read the catalogue leaves every job without a list. Said under each
 * job it was the same long paragraph three times, the page twice as long on
 * a phone, and a screen reader reading it three times over.
 */
export function catalogueProblem(
  models: readonly ProviderModels[],
  provider: Provider
): string | null {
  const listed = offeredBy(models, provider);
  const name = providerName(provider);

  if (!listed) {
    return null;
  }

  // A refused key and a provider that is down read the same from here and
  // are not the same thing: one is replaced, the other is waited for.
  // "Check the key and the address" sent somebody to replace a key that
  // signs every other call in this app perfectly well — providers scope
  // keys, and reading the catalogue is a permission of its own.
  if (!listed.reachable) {
    return listed.problem === 'assistance.rejected'
      ? m['ai.models.rejected']({ provider: name })
      : m['ai.models.unreachable']({ provider: name });
  }

  return listed.models.length === 0 ? m['ai.models.emptyCatalogue']({ provider: name }) : null;
}

/** Whether a job's provider has no list for a reason already said beside it. */
export const unlistedByProvider = (models: readonly ProviderModels[], use: Use): boolean =>
  use.provider !== '' && catalogueProblem(models, use.provider) !== null;

/**
 * Whether a provider's address was changed while its stored key is kept.
 *
 * The server only ever sends a stored key to the address it was saved with,
 * so that save asks for the key again — said under the address being typed,
 * rather than only in the refusal afterwards.
 */
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
