<script lang="ts">
  import type { AppError } from '$api';
  import { Button } from '$ds';

  import { m } from '$shell/i18n';
  import { toaster } from '$shell/toaster.svelte';
  import FormFailure from './FormFailure.svelte';
  import { heirsOf, removeHeir, type Heir } from './households.svelte';

  /**
   * Who reads this household's recipes from elsewhere. Set up from the heir's side, so everyone here is told;
   * an owner can cut a direct heir (its own heirs go too), a deeper one says which household it reads through.
   */
  interface Props {
    householdId: string;
    /** Whether the person reading may cut a household loose. */
    owner: boolean;
  }

  let { householdId, owner }: Props = $props();

  let heirs = $state<readonly Heir[] | null>(null);
  let failure = $state<AppError | null>(null);
  /** The household being cut loose, while that is in flight. */
  let removing = $state<string | null>(null);

  async function load(id: string) {
    const result = await heirsOf(id);

    // Another household may be on screen by the time this one answers.
    if (id !== householdId) {
      return;
    }

    if (result.ok) {
      heirs = result.value;
      failure = null;
    } else {
      failure = result.error;
    }
  }

  $effect(() => {
    heirs = null;
    void load(householdId);
  });

  const names = $derived(new Map((heirs ?? []).map((heir) => [heir.householdId, heir.name])));

  async function cut(heir: Heir) {
    removing = heir.householdId;
    failure = await removeHeir(householdId, heir.householdId);
    removing = null;

    if (!failure) {
      toaster.show({
        message: () => m['household.heirs.removed']({ name: heir.name }),
        tone: 'success'
      });
      await load(householdId);
    }
  }
</script>

<div class="heirs">
  <p class="heading">{m['household.heirs.title']()}</p>

  <FormFailure {failure} />

  {#if heirs && heirs.length === 0}
    <p class="none">{m['household.heirs.none']()}</p>
  {:else if heirs}
    <ul class="list">
      {#each heirs as heir (heir.householdId)}
        {@const direct = heir.inheritsFrom === householdId}
        <li class="heir">
          <span class="name">
            {heir.name}
            {#if !direct}
              <span class="through">
                {m['household.heirs.through']({ name: names.get(heir.inheritsFrom) ?? '' })}
              </span>
            {/if}
          </span>

          {#if owner && direct}
            <Button
              variant="secondary"
              size="sm"
              loading={removing === heir.householdId}
              disabled={removing !== null}
              onclick={() => void cut(heir)}
            >
              {m['household.heirs.remove']()}
            </Button>
          {/if}
        </li>
      {/each}
    </ul>
  {/if}
</div>

<style>
  .heirs {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
  }

  .heading {
    font-weight: var(--weight-semibold);
  }

  .none,
  .through {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .list {
    display: flex;
    flex-direction: column;
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .heir {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-2) var(--space-4);
    min-height: var(--control-md);
    padding-block: var(--space-2);
    border-bottom: 1px solid var(--border);
  }

  .name {
    display: flex;
    flex-direction: column;
    min-width: 0;
    overflow-wrap: anywhere;
  }
</style>
