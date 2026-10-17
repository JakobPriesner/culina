<script lang="ts">
  import { Button, FilePicker } from '$ds';

  import { upcoming } from '$features/planning/upcoming.svelte';
  import { shopping } from '$features/shopping/stores/shopping.svelte';
  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';
  import { toaster } from '$shell/toaster.svelte';
  import { cookLog } from './stores/cookLog.svelte';

  /**
   * Straight after Finish, while the meal is in front of you: a photo of this cook, a line for next time, and the
   * planned meal's lines nobody bought taken off the list. All optional, gone with one tap, never asked again.
   */
  interface Props {
    recipeId: string;
    householdId: string | null;
  }

  let { recipeId, householdId }: Props = $props();

  let picker = $state<ReturnType<typeof FilePicker>>();
  let busy = $state(false);

  const entryId = $derived(
    cookLog.justRecorded?.recipeId === recipeId ? cookLog.justRecorded.entryId : null
  );
  const photographed = $derived(
    cookLog.items.some((item) => item.entryId === entryId && item.hasPhoto)
  );

  /** Today's planned meal of this recipe, if the plan has one. */
  const meal = $derived(upcoming.today.find((planned) => planned.recipeId === recipeId) ?? null);

  /** What that meal still asks of the list; bought lines are in the kitchen and stay. */
  const leftover = $derived(
    meal && shopping.householdId === householdId
      ? shopping.items.filter(
          (item) =>
            !item.isChecked &&
            (item.sources ?? []).some((source) => source.planEntryId === meal.entryId)
        ).length
      : 0
  );

  /** Checked here so an obviously hopeless upload fails at once, as on the attempt strip. */
  const maxBytes = 10 * 1024 * 1024;

  async function photograph(file: File) {
    if (!entryId) {
      return;
    }

    if (file.size > maxBytes) {
      toaster.show({ message: () => m['editor.photoTooLarge'](), tone: 'danger' });

      return;
    }

    busy = true;
    const ok = await cookLog.setPhoto(recipeId, entryId, file);
    busy = false;

    if (!ok) {
      toaster.show({ message: () => m['editor.photoFailed'](), tone: 'danger' });
    }
  }

  async function clearLeftover() {
    if (!householdId || !meal) {
      return;
    }

    busy = true;
    const failure = await shopping.withdrawMeal(householdId, meal.entryId);
    busy = false;

    toaster.show(
      failure
        ? { message: () => explain(failure), tone: 'danger' }
        : { message: () => m['plan.unplanned.withdrawn'](), tone: 'success' }
    );
  }
</script>

{#if entryId}
  <section class="wrap-up" aria-label={m['cooked.title']()}>
    <p class="title">{m['cooked.title']()}</p>
    <p class="hint">{m['cooked.hint']()}</p>

    <div class="actions">
      {#if !photographed}
        <Button size="sm" disabled={busy} onclick={() => picker?.open()}>
          {m['cooked.photo']()}
        </Button>
      {/if}
      <Button size="sm" href="#overall-note">{m['cooked.note']()}</Button>
      {#if leftover > 0}
        <Button size="sm" disabled={busy} onclick={() => void clearLeftover()}>
          {m['cooked.leftover']({ count: leftover })}
        </Button>
      {/if}
      <Button size="sm" variant="ghost" onclick={() => cookLog.settle()}
        >{m['cooked.done']()}</Button
      >
    </div>

    <FilePicker
      bind:this={picker}
      label={m['attempts.choose']()}
      accept="image/*"
      onpick={(file) => void photograph(file)}
    />
  </section>
{/if}

<style>
  .wrap-up {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    margin-bottom: var(--space-6);
    padding: var(--space-4);
    border-radius: var(--radius-lg);
    background: var(--surface-accent-subtle);
  }

  .title {
    margin: 0;
    font-family: var(--font-editorial);
    font-size: var(--text-lg);
  }

  .hint {
    margin: 0;
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
  }

  @media print {
    .wrap-up {
      display: none;
    }
  }
</style>
