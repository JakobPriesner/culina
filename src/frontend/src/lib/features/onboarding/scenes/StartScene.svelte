<script lang="ts">
  import { resolve } from '$app/paths';
  import { session } from '$features/auth/session.svelte';
  import { sourceAppNames } from '$features/import/sourceApps';
  import { m } from '$shell/i18n';

  /**
   * The tour ends with the first real step rather than a summary: the doors the new-recipe page offers
   * (`?start=` there, and a whole collection from another app), or a look round first.
   */
  interface Props {
    /** A door was taken: the tour is over. */
    onclose: () => void;
  }

  let { onclose }: Props = $props();

  const newRecipe = resolve('/(app)/recipes/new');
  const importPage = resolve('/(app)/recipes/import');

  // A draft from an idea only where this instance writes drafts; otherwise the plain form.
  const doors = $derived([
    {
      start: 'paste',
      query: '?start=paste',
      title: m['tour.start.paste'],
      what: m['tour.start.paste.what']
    },
    {
      start: 'photo',
      query: '?start=photo',
      title: m['tour.start.photo'],
      what: m['tour.start.photo.what']
    },
    session.user?.assistance.draft
      ? {
          start: 'idea',
          query: '?start=idea',
          title: m['tour.start.idea'],
          what: m['tour.start.idea.what']
        }
      : {
          start: 'write',
          query: '',
          title: m['tour.start.write'],
          what: m['tour.start.write.what']
        }
  ] as const);
</script>

<div class="start">
  <ul class="doors">
    {#each doors as door (door.start)}
      <li>
        <a class="door" href="{newRecipe}{door.query}" onclick={onclose}>
          <span class="icon" aria-hidden="true">
            <svg
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="1.8"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              {#if door.start === 'paste'}
                <path d="M10 14a4 4 0 0 0 5.7 0l3-3a4 4 0 0 0-5.7-5.7l-1 1" />
                <path d="M14 10a4 4 0 0 0-5.7 0l-3 3a4 4 0 0 0 5.7 5.7l1-1" />
              {:else if door.start === 'photo'}
                <path
                  d="M4 8.5A1.5 1.5 0 0 1 5.5 7h2l1.5-2h6l1.5 2h2A1.5 1.5 0 0 1 20 8.5v9a1.5 1.5 0 0 1-1.5 1.5h-13A1.5 1.5 0 0 1 4 17.5Z"
                />
                <circle cx="12" cy="13" r="3.5" />
              {:else if door.start === 'idea'}
                <path d="M9 18h6M10 21h4M12 3a6 6 0 0 0-3.5 10.9V16h7v-2.1A6 6 0 0 0 12 3Z" />
              {:else}
                <path d="M4 20h4L19 9a2.1 2.1 0 0 0-3-3L5 17v3Z" />
                <path d="m15 6 3 3" />
              {/if}
            </svg>
          </span>
          <span class="words">
            <strong>{door.title()}</strong>
            <span>{door.what()}</span>
          </span>
        </a>
      </li>
    {/each}
    <li>
      <a class="door" href={importPage} onclick={onclose}>
        <span class="icon" aria-hidden="true">
          <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="1.8"
            stroke-linecap="round"
            stroke-linejoin="round"
          >
            <path d="M12 4v10M8 10l4 4 4-4" />
            <path d="M5 15v3a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2v-3" />
          </svg>
        </span>
        <span class="words">
          <strong>{m['tour.start.import']()}</strong>
          <span>{m['import.source.hint']({ apps: sourceAppNames() })}</span>
        </span>
      </a>
    </li>
  </ul>

  <p class="again">{m['tour.start.again']()}</p>
</div>

<style>
  .start {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: var(--space-4);
  }

  .doors {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    width: min(26rem, 100%);
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .door {
    display: flex;
    align-items: center;
    gap: var(--space-3);
    padding: var(--space-3) var(--space-4);
    border: 1px solid var(--border);
    border-radius: var(--radius-lg);
    background: var(--surface-raised);
    box-shadow: var(--shadow-card);
    color: var(--text);
    text-decoration: none;
    transition:
      border-color var(--duration-fast) var(--ease-out),
      transform var(--duration-fast) var(--ease-out);
  }

  .door:hover {
    border-color: var(--border-accent);
    transform: translateY(-1px);
  }

  .door:focus-visible {
    outline: 2px solid var(--border-focus);
    outline-offset: 2px;
  }

  .icon {
    display: grid;
    flex: none;
    place-items: center;
    width: 2.75rem;
    height: 2.75rem;
    border-radius: var(--radius-full);
    background: var(--surface-accent-subtle);
    color: var(--accent);
  }

  .icon svg {
    width: 1.4rem;
    height: 1.4rem;
  }

  .words {
    display: flex;
    flex-direction: column;
    line-height: var(--leading-tight);
  }

  .words span {
    margin-top: var(--space-1);
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .again {
    margin: 0;
    color: var(--text-muted);
    font-size: var(--text-xs);
    text-align: center;
  }

  @media (prefers-reduced-motion: reduce) {
    .door {
      transition: none;
    }
  }
</style>
