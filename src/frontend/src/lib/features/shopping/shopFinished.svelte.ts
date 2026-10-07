import { shopping } from './stores/shopping.svelte';

/**
 * Whether the shop is done, and whether it finished while somebody was here.
 *
 * Nothing left to find, but the trolley is not empty: a finished shop. Olli
 * celebrates the shop finishing while somebody was ticking the last item off,
 * once, and not a list that was already done when the page opened: a
 * celebration is for effort, and opening a page is none.
 */
export function trackShopFinished() {
  const finished = $derived(shopping.toBuy.length === 0 && shopping.bought.length > 0);

  let justFinished = $state(false);
  let sawUnfinished = false;

  $effect(() => {
    if (!finished) {
      sawUnfinished = shopping.toBuy.length > 0;
      justFinished = false;
    } else if (sawUnfinished) {
      justFinished = true;
    }
  });

  return {
    get finished() {
      return finished;
    },
    get justFinished() {
      return justFinished;
    }
  };
}
