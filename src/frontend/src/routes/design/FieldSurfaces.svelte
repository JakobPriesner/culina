<script lang="ts">
  import { Button, Field, SearchField, Select, TextInput } from '$ds';

  /*
   * Field surfaces on the three grounds a field sits on (decided 28 Oct, culina-v2-6ilt.12): white on the warm page and
   * in a cream group, the inset fill inside a white card or sheet, which swaps one token the way Card and Dialog do.
   * Specimens only, exempt from "no hard-coded text" like the rest of the gallery.
   */
  type Mode = 'light' | 'dark';
  type FieldState = 'rest' | 'focus' | 'error' | 'disabled';

  let mode = $state<Mode>('light');

  const states: readonly FieldState[] = ['rest', 'focus', 'error', 'disabled'];
  const grounds = [
    { id: 'page', label: 'On the warm page' },
    { id: 'sheet', label: 'Inside a white card or sheet' },
    { id: 'group', label: 'Inside a cream group' }
  ] as const;
  const kinds = [
    'Search',
    'Login email',
    'Recipe ingredient',
    'Diary amount',
    'Settings unit'
  ] as const;

  const unitOptions = [
    { value: 'g', label: 'Grams' },
    { value: 'ml', label: 'Millilitres' }
  ];

  const sampleValue = (kind: (typeof kinds)[number]) =>
    kind === 'Diary amount' ? '250' : kind === 'Login email' ? 'cook@example.org' : 'Aubergine';

  let counter = 0;
  const nextId = () => `field-surface-${counter++}`;
</script>

<div class="fields" data-theme="warm-paper" data-mode={mode}>
  <div class="head">
    <p>Appearance of this section only. Focus is drawn with the same tokens as a real focus.</p>
    <Button
      variant="secondary"
      size="sm"
      onclick={() => (mode = mode === 'light' ? 'dark' : 'light')}
    >
      {mode === 'light' ? 'Show dark' : 'Show light'}
    </Button>
  </div>

  {#each grounds as ground (ground.id)}
    <h3>{ground.label}</h3>
    <div class="ground {ground.id}">
      <div class="variant">
        {#each kinds as kind (kind)}
          <div class="states">
            {#each states as state (state)}
              {@const disabled = state === 'disabled'}
              {@const error = state === 'error' ? 'Check this one.' : undefined}
              {#if kind === 'Search'}
                {#if state === 'error'}
                  <p class="none">Search has no error state.</p>
                {:else}
                  <div class:forced={state === 'focus'}>
                    <SearchField
                      id={nextId()}
                      label="Search"
                      clearLabel="Clear search"
                      placeholder="Search recipes"
                      value=""
                      {disabled}
                    />
                  </div>
                {/if}
              {:else}
                <div class:forced={state === 'focus'}>
                  <Field label="{kind}, {state}" {error}>
                    {#snippet children({ id, describedBy, invalid })}
                      {#if kind === 'Settings unit'}
                        <Select
                          {id}
                          {describedBy}
                          {invalid}
                          {disabled}
                          value="g"
                          options={unitOptions}
                        />
                      {:else}
                        <TextInput
                          {id}
                          {describedBy}
                          {invalid}
                          {disabled}
                          type={kind === 'Login email' ? 'email' : 'text'}
                          inputmode={kind === 'Diary amount' ? 'decimal' : undefined}
                          value={sampleValue(kind)}
                        />
                      {/if}
                    {/snippet}
                  </Field>
                </div>
              {/if}
            {/each}
          </div>
        {/each}
      </div>
    </div>
  {/each}
</div>

<style>
  .fields {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    padding: var(--space-4);
    border-radius: var(--radius-lg);
    background: var(--surface);
    color: var(--text);
  }

  .head {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-4);
  }

  .ground {
    display: grid;
    /* Never wider than the ground itself, so the gallery reflows at 320px. */
    grid-template-columns: repeat(auto-fit, minmax(min(20rem, 100%), 1fr));
    gap: var(--space-4);
    padding: var(--space-4);
    border-radius: var(--radius-lg);
  }

  .ground.page {
    background: var(--surface);
  }

  .ground.sheet {
    --field-surface: var(--field-surface-inset);
    background: var(--surface-raised);
    border: 1px solid var(--border);
  }

  .ground.group {
    background: var(--surface-sunken);
  }

  .variant {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    min-width: 0;
  }

  .states {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: var(--space-3);
    align-items: start;
  }

  .none {
    color: var(--text-subtle);
    font-size: var(--text-sm);
  }

  /* A specimen of focus without taking focus: the same two declarations as the real rule. */
  .forced :global(.ds-control) {
    border-color: var(--field-border-focus);
    outline: 2px solid var(--field-border-focus);
  }
</style>
