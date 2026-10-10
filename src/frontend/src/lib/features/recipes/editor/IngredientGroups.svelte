<script lang="ts">
  import { tick } from 'svelte';

  import { Button, IconButton, TextInput } from '$ds';
  import { m } from '$shell/i18n';

  import type { IngredientGroup, Step } from '../types';
  import IngredientEditor from './IngredientEditor.svelte';

  /**
   * Every ingredient group, one list each. A recipe with one unnamed group is just the list; with
   * more, or a named one, each group is headed by its name, which is written in place.
   * Only the last group keeps its add row open: a long recipe would otherwise be a column of forms.
   */
  interface Props {
    groups: readonly IngredientGroup[];
    steps: readonly Step[];
    onchange: (index: number, ingredients: IngredientGroup['ingredients'][number][]) => void;
    onrename: (index: number, name: string) => void;
    onadd: () => void;
    onremove: (index: number) => void;
    householdId: string;
    language: string;
    focusId?: string | null;
  }

  let {
    groups,
    steps,
    onchange,
    onrename,
    onadd,
    onremove,
    householdId,
    language,
    focusId = null
  }: Props = $props();

  const headed = $derived(groups.length > 1 || groups[0]?.name != null);

  async function add() {
    const at = groups.length;

    onadd();

    await tick();
    document.getElementById(`group-${at}-name`)?.focus();
  }
</script>

<div class="groups">
  {#each groups as group, index (index)}
    <div class="group">
      {#if headed}
        <div class="head">
          <div class="name">
            <TextInput
              id="group-{index}-name"
              quiet
              label={m['editor.groupName']({ number: index + 1 })}
              placeholder={m['editor.groupNamePlaceholder']()}
              maxlength={80}
              value={group.name ?? ''}
              oninput={(name) => onrename(index, name)}
            />
          </div>

          <!-- Only an empty group can go, so removing one never loses a line. -->
          {#if groups.length > 1 && group.ingredients.length === 0}
            <IconButton
              label={m['editor.removeGroup']({ number: index + 1 })}
              size="sm"
              onclick={() => onremove(index)}
            >
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <path d="m6 6 12 12M18 6 6 18" stroke-linecap="round" />
              </svg>
            </IconButton>
          {/if}
        </div>
      {/if}

      <IngredientEditor
        ingredients={group.ingredients}
        {steps}
        onchange={(ingredients) => onchange(index, ingredients)}
        {householdId}
        {language}
        {focusId}
        scope="g{index}-"
        compact={index < groups.length - 1}
        moreLabel={m['editor.addIngredientToGroup']({ number: index + 1 })}
      />
    </div>
  {/each}

  <div class="addGroup">
    <Button variant="ghost" size="sm" onclick={() => void add()}>
      {m['editor.addGroup']()}
    </Button>
  </div>
</div>

<style>
  .groups {
    display: flex;
    flex-direction: column;
    gap: var(--space-6);
    min-width: 0;
  }

  .group {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    min-width: 0;
  }

  /* Like a step's title: a heading you can type in, drawn only when you reach for it. */
  .head {
    display: flex;
    align-items: center;
    gap: var(--space-2);
    min-width: 0;
  }

  .name {
    flex: 1;
    min-width: 0;
    font-weight: var(--weight-medium);
  }

  .addGroup {
    display: flex;
  }
</style>
