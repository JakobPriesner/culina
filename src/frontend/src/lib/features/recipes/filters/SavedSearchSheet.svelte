<script lang="ts">
  import { Button, Field, Sheet, TextInput } from '$ds';
  import { m } from '$shell/i18n';
  import { explain } from '$shell/explain';
  import { toaster } from '$shell/toaster.svelte';

  import { savedSearches, worthSaving, type SavedSearch } from '../stores/savedSearches.svelte';
  import type { RecipeQuery } from '../stores/libraryView.svelte';
  import { summarise } from './labels';

  /**
   * Saving what the toolbar is showing, and everything saved before.
   *
   * One sheet for both, because they are one thought: somebody opening "saved
   * searches" is either adding to the list or acting on it, and two sheets
   * would mean guessing which before they had looked.
   *
   * Nothing here is optimistic. A saved search is a small deliberate act with a
   * name attached, and a name that appeared and then vanished because the
   * server disagreed would be worse than a moment's wait.
   */
  interface Props {
    open: boolean;
    householdId: string;
    view: RecipeQuery;
    onapply: (search: SavedSearch) => void;
    onpromote: (search: SavedSearch) => void;
    onclose: () => void;
  }

  let { open = $bindable(), householdId, view, onapply, onpromote, onclose }: Props = $props();

  let name = $state('');
  let busy = $state(false);

  const current = $derived(view.snapshot());
  const savable = $derived(worthSaving(current));
  const summary = $derived(summarise(current));

  /**
   * Whether a shelf could ask the same question.
   *
   * A cookbook fills itself from tags and a time limit; it cannot ask for
   * words. So a search that is only words has nothing a shelf could be made
   * from, and offering the button anyway would be offering an empty cookbook.
   */
  const shelvable = (search: SavedSearch): boolean =>
    search.tags.length > 0 || search.maxMinutes !== null;

  async function save() {
    const trimmed = name.trim();

    if (trimmed.length === 0 || !savable) {
      return;
    }

    busy = true;

    const outcome = await savedSearches.save(householdId, trimmed, current);

    busy = false;

    if ('id' in outcome) {
      name = '';
      toaster.show({ message: () => m['saved.saved']({ name: outcome.name }) });
      onclose();

      return;
    }

    toaster.show({
      message: () => (outcome.status === 409 ? m['saved.nameTaken']() : explain(outcome)),
      tone: 'danger'
    });
  }

  async function overwrite(search: SavedSearch) {
    busy = true;

    const outcome = await savedSearches.revise(search.id, search.name, current);

    busy = false;

    toaster.show(
      'id' in outcome
        ? { message: () => m['saved.updated']({ name: outcome.name }) }
        : { message: () => explain(outcome), tone: 'danger' }
    );
  }

  async function forget(search: SavedSearch) {
    busy = true;

    const failure = await savedSearches.forget(search.id);

    busy = false;

    toaster.show(
      failure
        ? { message: () => m['saved.deleteFailed'](), tone: 'danger' }
        : { message: () => m['saved.deleted']({ name: search.name }) }
    );
  }
</script>

<Sheet bind:open title={m['saved.title']()} closeLabel={m['saved.cancel']()} {onclose}>
  <div class="panel">
    <section class="saving">
      <h3 class="heading">{m['saved.saveTitle']()}</h3>

      {#if savable}
        <p class="summary">{m['saved.summary']({ summary })}</p>

        <Field label={m['saved.name']()} hint={m['saved.nameHint']()}>
          {#snippet children({ id, describedBy })}
            <TextInput
              {id}
              {describedBy}
              bind:value={name}
              placeholder={m['saved.namePlaceholder']()}
              maxlength={40}
            />
          {/snippet}
        </Field>

        <div class="confirm">
          <Button
            variant="primary"
            loading={busy}
            disabled={name.trim().length === 0}
            onclick={() => void save()}
          >
            {m['saved.confirm']()}
          </Button>
        </div>
      {:else}
        <p class="summary">{m['saved.nothingToSave']()}</p>
      {/if}
    </section>

    <section class="saved">
      <h3 class="heading">{m['saved.manage']()}</h3>

      {#if savedSearches.items.length === 0}
        <p class="summary">{m['saved.none']()}</p>
      {:else}
        <ul class="list">
          {#each savedSearches.items as search (search.id)}
            <li class="one">
              <div class="what">
                <p class="name">{search.name}</p>
                <p class="detail">{summarise(search)}</p>
              </div>

              <div class="actions">
                <Button
                  size="sm"
                  onclick={() => {
                    onapply(search);
                    onclose();
                  }}
                >
                  {m['filters.action']()}
                </Button>

                <!-- Only where there is something to save over it with.
                     Otherwise this button would quietly empty a search. -->
                {#if savable}
                  <Button
                    size="sm"
                    variant="ghost"
                    loading={busy}
                    onclick={() => void overwrite(search)}
                  >
                    {m['saved.update']()}
                  </Button>
                {/if}

                {#if shelvable(search)}
                  <Button size="sm" variant="ghost" onclick={() => onpromote(search)}>
                    {m['saved.promote']()}
                  </Button>
                {/if}

                <Button
                  size="sm"
                  variant="ghost"
                  loading={busy}
                  onclick={() => void forget(search)}
                >
                  {m['saved.delete']()}
                </Button>
              </div>
            </li>
          {/each}
        </ul>
      {/if}
    </section>
  </div>
</Sheet>

<style>
  .panel {
    display: flex;
    flex-direction: column;
    gap: var(--space-6);
  }

  .heading {
    font-size: var(--text-base);
    font-weight: var(--weight-medium);
    margin-bottom: var(--space-2);
  }

  .summary {
    color: var(--text-muted);
    font-size: var(--text-sm);
    max-width: 48ch;
    margin-bottom: var(--space-3);
  }

  .confirm {
    display: flex;
    justify-content: flex-end;
    margin-top: var(--space-3);
  }

  .saved {
    padding-top: var(--space-4);
    border-top: 1px solid var(--border);
  }

  .list {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .one {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-2) var(--space-4);
  }

  .what {
    min-width: 0;
  }

  .name {
    font-weight: var(--weight-medium);
  }

  .detail {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
  }
</style>
