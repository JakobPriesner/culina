<script lang="ts">
  import { Button, Checkbox, Sheet } from '$ds';

  import { m } from '$shell/i18n';
  import { toaster } from '$shell/toaster.svelte';
  import CookbookSheet from './CookbookSheet.svelte';
  import { cookbooks } from './stores/cookbooks.svelte';
  import type { CookbookRules } from './types';

  /** Which shelves a recipe is on, as optimistic ticks (rolled back if the write fails); the sheet stays open across several. */
  interface Props {
    open: boolean;
    householdId: string;
    recipeId: string;
    onclose: () => void;
  }

  let { open, householdId, recipeId, onclose }: Props = $props();

  let making = $state(false);
  let saving = $state(false);

  // Only while open: a closed sheet keeping its list warm costs two requests on every recipe page.
  $effect(() => {
    if (open) {
      void cookbooks.list(householdId);
      void cookbooks.loadMemberships(recipeId, householdId);
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

    // Onto the shelf just made, unless it fills itself (then the rules decide): making a cookbook from here is never the goal.
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
            <!-- A self-filling shelf is shown but not offered: hiding it would leave people hunting,
                 and the disabled tick says whether the recipe is on it. -->
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
    /* Muted, not dimmed with opacity, which blends text toward the background beyond what a contrast test sees. */
    color: var(--text-muted);
    font-size: var(--text-sm);
  }
</style>
