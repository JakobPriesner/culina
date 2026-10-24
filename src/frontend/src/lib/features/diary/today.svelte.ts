import { createSubscriber } from 'svelte/reactivity';

import { asDate } from '$features/planning/mealPlan.svelte';

// Moves on at midnight, and when the app comes back from a night in the background, where timers may not have run.
const subscribe = createSubscriber((update) => {
  let timer: ReturnType<typeof setTimeout>;

  const atMidnight = () => {
    const now = new Date();
    const tomorrow = new Date(now.getFullYear(), now.getMonth(), now.getDate() + 1);

    timer = setTimeout(() => {
      update();
      atMidnight();
    }, tomorrow.getTime() - now.getTime());
  };

  const back = () => {
    if (!document.hidden) update();
  };

  atMidnight();
  document.addEventListener('visibilitychange', back);

  return () => {
    clearTimeout(timer);
    document.removeEventListener('visibilitychange', back);
  };
});

/** Today's date, read reactively: a page left open overnight logs to the new day, not to yesterday. */
export function currentDate(): string {
  subscribe();

  // eslint-disable-next-line svelte/prefer-svelte-reactivity -- Read once for its date; the subscriber is what reacts.
  return asDate(new Date());
}
