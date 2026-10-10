import { cooking } from './stores/cooking.svelte';
import { createTimers } from './timers.svelte';
import { createWakeLock } from './wakeLock.svelte';
import { registerStore } from '$shell/stores';

export const kitchenTimers = createTimers(
  () => cooking.session?.sessionId ?? null,
  () =>
    cooking.session
      ? `/recipes/${encodeURIComponent(cooking.session.recipeId)}/cook?yield=${cooking.session.servings}`
      : '/'
);
export const kitchenWakeLock = createWakeLock();
cooking.onEnded = (sessionId) => kitchenTimers.clear(sessionId);
registerStore(() => kitchenTimers.clear());
