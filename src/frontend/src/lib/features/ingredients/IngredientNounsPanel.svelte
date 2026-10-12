<script lang="ts">
  import { Button, Field, TextInput } from '$ds';
  import { nouns as store } from '$features/recipes/stores/nouns.svelte';
  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';
  import { toaster } from '$shell/toaster.svelte';

  /**
   * The household's own words in both numbers, for words Culina does not list: "1 Maultasche", "2 Maultaschen".
   * Saving a singular already listed changes its plural. The page loads the store.
   */
  interface Props {
    householdId: string;
  }

  let { householdId }: Props = $props();

  let one = $state('');
  let many = $state('');
  let saving = $state(false);

  async function save() {
    const noun = { one: one.trim(), many: many.trim() };

    if (!noun.one || !noun.many) {
      return;
    }

    saving = true;

    const failure = await store.set(householdId, noun);

    saving = false;

    if (failure) {
      toaster.show({ message: () => explain(failure), tone: 'danger' });

      return;
    }

    one = '';
    many = '';
    toaster.show({ message: () => m['ingredients.nouns.saved'](noun), tone: 'success' });
  }

  async function remove(word: string) {
    const failure = await store.remove(householdId, word);

    toaster.show(
      failure
        ? { message: () => explain(failure), tone: 'danger' }
        : { message: () => m['ingredients.nouns.removed']({ one: word }), tone: 'success' }
    );
  }
</script>

<div class="panel">
  {#if store.own.length === 0}
    <p class="quiet">{m['ingredients.nouns.empty']()}</p>
  {:else}
    <ul class="rows" aria-label={m['ingredients.nouns.title']()}>
      {#each store.own as noun (noun.one)}
        <li class="row">
          <span>{m['ingredients.nouns.pair'](noun)}</span>
          <Button
            size="sm"
            variant="ghost"
            label={m['ingredients.nouns.removeLabel']({ one: noun.one })}
            onclick={() => void remove(noun.one)}
          >
            {m['ingredients.nouns.remove']()}
          </Button>
        </li>
      {/each}
    </ul>
  {/if}

  <form
    class="form"
    onsubmit={(event) => {
      event.preventDefault();
      void save();
    }}
  >
    <Field label={m['ingredients.nouns.one']()}>
      {#snippet children({ id, describedBy, invalid })}
        <TextInput
          {id}
          {describedBy}
          {invalid}
          bind:value={one}
          autocomplete="off"
          maxlength={60}
        />
      {/snippet}
    </Field>
    <Field label={m['ingredients.nouns.many']()}>
      {#snippet children({ id, describedBy, invalid })}
        <TextInput
          {id}
          {describedBy}
          {invalid}
          bind:value={many}
          autocomplete="off"
          maxlength={60}
        />
      {/snippet}
    </Field>
    <span class="submit">
      <Button type="submit" size="sm" variant="primary" loading={saving}>
        {m['ingredients.nouns.save']()}
      </Button>
    </span>
  </form>
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
    margin: 0;
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .rows {
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .row {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-2) var(--space-4);
    min-height: var(--control-md);
    padding-block: var(--space-2);
    border-bottom: 1px solid var(--border);
    overflow-wrap: anywhere;
  }

  .form {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(10rem, 1fr));
    align-items: end;
    gap: var(--space-3);
  }

  .submit {
    display: flex;
  }
</style>
