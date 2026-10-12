<script lang="ts">
  import { Button, Field, SearchField, Skeleton, TextInput } from '$ds';
  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';
  import { toaster } from '$shell/toaster.svelte';

  import { ingredientNames as store } from './stores/ingredientNames.svelte';

  /**
   * Every ingredient name the household's recipes use, most used first, each with a way to rename it. Typing a name
   * the household already uses merges the two instead, which is the same operation to the server: a rename is a
   * merge into a name nobody uses yet. The page loads the store.
   */
  interface Props {
    householdId: string;
    /** After any rename or merge, for what else on the page depends on names. */
    onmerged?: () => void;
  }

  let { householdId, onmerged }: Props = $props();

  const status = $derived(store.status(householdId));
  const names = $derived(store.names(householdId));

  let query = $state('');
  let editing = $state<string | null>(null);
  let typed = $state('');
  let saving = $state(false);

  const shown = $derived.by(() => {
    const wanted = query.trim().toLocaleLowerCase();

    return wanted ? names.filter((one) => one.name.toLocaleLowerCase().includes(wanted)) : names;
  });

  /** The name already in use that the typed one would merge into, matched loosely; the server folds properly. */
  const existing = $derived.by(() => {
    const wanted = typed.trim().toLocaleLowerCase();

    return (
      names.find((one) => one.name !== editing && one.name.toLocaleLowerCase() === wanted) ?? null
    );
  });

  function edit(name: string) {
    editing = name;
    typed = name;
  }

  async function save(from: string) {
    // Into the spelling already in use, so "hähnchenbrust" does not become a second "Hähnchenbrust".
    const to = existing?.name ?? typed.trim();

    if (!to || to === from) {
      editing = null;

      return;
    }

    saving = true;

    const result = await store.merge(householdId, from, to);

    saving = false;

    if (!result.ok) {
      toaster.show({ message: () => explain(result.error), tone: 'danger' });

      return;
    }

    editing = null;
    onmerged?.();
    toaster.show({
      message: () => m['ingredients.merged']({ from, to: result.value }),
      tone: 'success'
    });
  }
</script>

<div class="panel">
  {#if status === 'failed'}
    <div class="quiet" role="status">
      <p>{m['ingredients.failed']()}</p>
      <Button size="sm" onclick={() => void store.load(householdId)}>{m['error.retry']()}</Button>
    </div>
  {:else if status !== 'ready'}
    <div class="skeletons" aria-label={m['ingredients.loading']()}>
      {#each [0, 1, 2, 3] as row (row)}
        <Skeleton width={row % 2 ? '50%' : '70%'} height="1.25rem" />
      {/each}
    </div>
  {:else if names.length === 0}
    <p class="quiet">{m['ingredients.names.empty']()}</p>
  {:else}
    <SearchField
      id="ingredient-names-filter"
      bind:value={query}
      label={m['ingredients.names.filter']()}
      placeholder={m['ingredients.names.filter']()}
      clearLabel={m['ingredients.names.clear']()}
    />

    {#if shown.length === 0}
      <p class="quiet">{m['ingredients.names.none']({ query: query.trim() })}</p>
    {:else}
      <ul class="rows" aria-label={m['ingredients.names.title']()}>
        {#each shown as one (one.name)}
          <li class="row">
            <span class="name">{one.name}</span>
            <span class="count">{m['ingredients.recipes']({ count: one.recipeCount })}</span>

            {#if editing === one.name}
              <form
                class="form"
                onsubmit={(event) => {
                  event.preventDefault();
                  void save(one.name);
                }}
              >
                <Field
                  label={m['ingredients.names.into']({ name: one.name })}
                  hint={m['ingredients.names.intoHint']()}
                >
                  {#snippet children({ id, describedBy, invalid })}
                    <TextInput
                      {id}
                      {describedBy}
                      {invalid}
                      bind:value={typed}
                      autocomplete="off"
                      maxlength={120}
                    />
                  {/snippet}
                </Field>

                <span class="actions">
                  <Button type="submit" size="sm" variant="primary" loading={saving}>
                    {existing
                      ? m['ingredients.names.mergeInto']({ name: existing.name })
                      : m['ingredients.names.rename']()}
                  </Button>
                  <Button size="sm" variant="ghost" onclick={() => (editing = null)}>
                    {m['ingredients.names.cancel']()}
                  </Button>
                </span>
              </form>
            {:else}
              <Button
                size="sm"
                variant="ghost"
                label={m['ingredients.names.changeLabel']({ name: one.name })}
                onclick={() => edit(one.name)}
              >
                {m['ingredients.names.change']()}
              </Button>
            {/if}
          </li>
        {/each}
      </ul>
    {/if}
  {/if}
</div>

<style>
  .panel {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    max-width: var(--measure);
    min-width: 0;
  }

  .quiet {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2) var(--space-4);
    margin: 0;
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .skeletons {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
  }

  .rows {
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .row {
    display: grid;
    grid-template-columns: minmax(0, 1fr) auto auto;
    align-items: center;
    gap: var(--space-2) var(--space-4);
    min-height: var(--control-md);
    padding-block: var(--space-2);
    border-bottom: 1px solid var(--border);
  }

  .name {
    min-width: 0;
    overflow-wrap: anywhere;
  }

  .count {
    color: var(--text-subtle);
    font-size: var(--text-sm);
    font-variant-numeric: tabular-nums;
    white-space: nowrap;
  }

  .form {
    display: flex;
    flex-direction: column;
    grid-column: 1 / -1;
    gap: var(--space-3);
    padding-bottom: var(--space-2);
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
  }
</style>
