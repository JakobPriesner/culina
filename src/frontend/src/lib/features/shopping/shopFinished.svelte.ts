import { shopping } from './stores/shopping.svelte';

/** Whether the shop is finished, and whether it finished while the page was open (a list already done on arrival does not celebrate). */
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
