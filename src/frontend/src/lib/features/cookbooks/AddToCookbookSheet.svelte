<script lang="ts">
  import { Button, Checkbox, Sheet } from '$ds';

  import { m } from '$shell/i18n';
  import { toaster } from '$shell/toaster.svelte';
  import CookbookSheet from './CookbookSheet.svelte';
  import { cookbooks } from './stores/cookbooks.svelte';
  import type { CookbookRules } from './types';

  /**
   * Which shelves a recipe is on.
   *
   * A list of ticks rather than a list of buttons, because "which cookbooks is
   * this in" and "put it in one" are the same question asked once — and a tick
   * already on is how you take it off again, so there is nothing else to find.
   *
   * Every tick is optimistic and rolls back exactly if the write fails; the
   * sheet stays open across several, because somebody sorting a recipe onto
   * three shelves should not have to reopen it twice.
   */
  interface Props {
    open: boolean;
    householdId: string;
    recipeId: string;
    onclose: () => void;
  }

  let { open, householdId, recipeId, onclose }: Props = $props();

  let making = $state(false);
  let saving = $state(false);

  // Only while it is open: a closed sheet that keeps its list warm is two
  // requests on every recipe page nobody asked for.
  $effect(() => {
    if (open) {
      void cookbooks.list(householdId);
      void cookbooks.loadMemberships(recipeId);
    }
  });

  async function toggle(cookbookId: string, name: string, on: boolean) {
    const done = await cookbooks.setOn(recipeId, { id: cookbookId, name }, on);

    if (!done) {
      toaster.show({ message: () => m['cookbooks.add.failed'](), tone: 'danger' });
    }
  }

  async function make(name: string, description: string | null, rules: CookbookRules | null) {
    saving = true;

    const created = await cookbooks.create(householdId, name, description ?? undefined, rules);

    saving = false;

    if (!created) {
      toaster.show({ message: () => m['cookbooks.add.failed'](), tone: 'danger' });

      return;
    }

    making = false;

    // Straight onto the shelf somebody just made for it, unless the shelf fills
    // itself — then the rules decide, and this recipe is either already on it
    // or was never meant to be. Making a cookbook from here is never the goal;
    // putting this recipe somewhere is.
    if (created.kind === 'manual') {
      await toggle(created.id, created.name, true);
    }
  }
</script>

<Sheet {open} title={m['cookbooks.add.title']()} closeLabel={m['cookbooks.add.done']()} {onclose}>
  <div class="picker">
    {#if cookbooks.items.length === 0 && cookbooks.status === 'ready'}
      <p class="nothing">{m['cookbooks.add.none']()}</p>
    {:else}
      <ul class="shelves">
        {#each cookbooks.items as cookbook (cookbook.id)}
          <li>
            <!-- A shelf that fills itself is shown and not offered: hiding it
                 would leave somebody hunting for a cookbook they know they
                 have, and the disabled tick still says whether the recipe is
                 on it. -->
            <Checkbox
              label={cookbook.name}
              checked={cookbook.kind === 'smart'
                ? cookbooks.contains(recipeId, cookbook.id)
                : cookbooks.contains(recipeId, cookbook.id)}
              disabled={cookbook.kind === 'smart'}
              onchange={(on) => void toggle(cookbook.id, cookbook.name, on)}
            />

            {#if cookbook.kind === 'smart'}
              <p class="automatic">{m['cookbooks.rules.byHand']()}</p>
            {/if}
          </li>
        {/each}
      </ul>
    {/if}

    <Button onclick={() => (making = true)}>{m['cookbooks.new.action']()}</Button>
  </div>

  {#snippet footer()}
    <Button variant="primary" onclick={onclose}>{m['cookbooks.add.done']()}</Button>
  {/snippet}
</Sheet>

<CookbookSheet
  open={making}
  {householdId}
  {saving}
  onsave={make}
  onclose={() => (making = false)}
/>

<style>
  .picker {
    display: flex;
    flex-direction: column;
    gap: var(--space-6);
  }

  .shelves {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .nothing {
    color: var(--text-muted);
  }

  .automatic {
    margin-top: var(--space-1);
    /* Muted rather than dimmed with opacity: opacity blends text toward
       whatever is behind it by an amount no contrast test can reach. */
    color: var(--text-muted);
    font-size: var(--text-sm);
  }
</style>
