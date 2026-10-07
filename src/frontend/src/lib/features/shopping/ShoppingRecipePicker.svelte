<script lang="ts">
  import { Button } from '$ds';
  import RecipePicker from '$features/recipes/RecipePicker.svelte';
  import type { RecipeSummary } from '$features/recipes/types';
  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';
  import { toaster } from '$shell/toaster.svelte';
  import { shopping } from './stores/shopping.svelte';

  interface Props {
    open: boolean;
    householdId: string;
  }

  let { open = $bindable(), householdId }: Props = $props();

  /** Which recipes this opening of the picker has already put on. */
  let taken = $state<string[]>([]);

  /**
   * A whole recipe in, at the yield it is written for.
   *
   * The other half of how a list fills up. Typing a line at a time is for the
   * milk and the washing-up liquid; a recipe is twelve lines nobody wants to
   * copy out, and the server merges them into what is already there.
   *
   * Its own yield rather than a number asked for here: the amounts a recipe
   * states are the ones somebody meant, and the recipe page is where a
   * different number is chosen — with every quantity on screen to check it
   * against, which is the part a picker row cannot show.
   *
   * The sheet stays up. A week's list is four or five recipes, and closing
   * after each one charged the search box, the scroll and the reopening to
   * every recipe after the first. The row saying it has been taken is the
   * receipt — a toast could not be one, because the sheet is a native dialog
   * and sits above it.
   */
  async function addRecipe(recipe: RecipeSummary) {
    if (taken.includes(recipe.id)) {
      return;
    }

    // Said before the round trip finishes. The list underneath updates when it
    // does, and a row that waits half a second to admit it was pressed is a
    // row somebody presses twice.
    taken = [...taken, recipe.id];

    const failure = await shopping.addRecipe(householdId, recipe.id, recipe.yieldAmount);

    if (!failure) {
      return;
    }

    // Out of the way, so the reason is readable: nothing may cover a dialog.
    taken = taken.filter((id) => id !== recipe.id);
    open = false;

    toaster.show({ message: () => explain(failure), tone: 'danger' });
  }

  function stop() {
    open = false;
    taken = [];
  }
</script>

<RecipePicker
  {open}
  {householdId}
  title={m['shopping.pick.title']()}
  {taken}
  onpick={(recipe) => void addRecipe(recipe)}
  onclose={stop}
>
  <!-- One way out that reads as finishing rather than abandoning, since by
       now the sheet is a list of things already done. -->
  {#snippet footer()}
    <Button variant="primary" onclick={stop}>{m['picker.done']()}</Button>
  {/snippet}
</RecipePicker>
