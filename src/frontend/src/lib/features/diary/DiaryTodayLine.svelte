<script lang="ts">
  import { resolve } from '$app/paths';
  import { Chevron } from '$ds';
  import { m } from '$shell/i18n';
  import NavIcon from '$shell/NavIcon.svelte';

  import { gramsText, kcalText } from './format';
  import { diary } from './stores/diary.svelte';
  import { diarySettings } from './stores/diarySettings.svelte';
  import { currentDate } from './today.svelte';

  /**
   * Today's diary in one line inside Woche's today, for people who keep one: where the meal is cooked, a tap from
   * logging it, and no row of its own above the week. Energy and protein only (docs/diary-design.md §9.6); the page
   * has the rest.
   */
  const today = $derived(currentDate());

  $effect(() => {
    if (diarySettings.enabled) {
      void diary.load(today);
    }
  });

  const day = $derived(diary.day(today));
  const kcal = $derived(day?.totals.kcal ?? null);
  const protein = $derived(day?.totals.protein ?? null);
</script>

{#if diarySettings.enabled}
  <a class="line" href={resolve('/(app)/diary')}>
    <span class="icon" aria-hidden="true"><NavIcon icon="diary" current={false} /></span>
    <span class="what">
      {#if day && day.entries.length > 0 && kcal !== null}
        {m['diary.line.today']({ kcal: kcalText(kcal, day) })}{#if protein !== null}
          · {m['diary.line.protein']({ grams: gramsText(protein) })}{/if}
      {:else}
        {m['diary.line.nothing']()}
      {/if}
    </span>
    <Chevron />
  </a>
{/if}

<style>
  /* Flush with the day's own lines; a hover tint shows it is a way to the diary. */
  .line {
    display: flex;
    align-items: center;
    gap: var(--space-2);
    min-height: var(--control-sm);
    margin-inline: calc(var(--space-2) * -1);
    padding: var(--space-1) var(--space-2);
    border-radius: var(--radius-md);
    color: var(--text-muted);
    font-size: var(--text-sm);
    text-decoration: none;
    transition: background-color var(--duration-fast) var(--ease-out);
  }

  .line:hover {
    background: var(--surface-hover);
    color: var(--text);
  }

  .icon {
    flex-shrink: 0;
    width: var(--space-4);
    height: var(--space-4);
  }

  .what {
    flex: 1;
    min-width: 0;
  }
</style>
