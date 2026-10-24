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
   * Today's diary in one line under the Today card, for people who keep one: where the meal is cooked, a tap from
   * logging it. Energy and protein only (docs/diary-design.md §9.6); the page has the rest.
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
  .line {
    display: flex;
    align-items: center;
    gap: var(--space-3);
    min-height: var(--control-sm);
    margin: calc(var(--space-6) * -1 + var(--space-2)) 0 var(--space-6);
    padding: var(--space-2) var(--space-4);
    border-radius: var(--radius-lg);
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
    width: var(--space-6);
    height: var(--space-6);
  }

  .what {
    flex: 1;
    min-width: 0;
  }
</style>
