<script lang="ts">
  import { resolve } from '$app/paths';
  import { session } from '$features/auth/session.svelte';
  import { m } from '$shell/i18n';

  /** Without handlers (somewhere other than the new-recipe page) each door is a link that opens that form there. */
  interface Props {
    /** Paste and photograph open the same form; the photo door puts its photos first. */
    onpaste?: (intent: 'paste' | 'photo') => void;
    ondescribe?: () => void;
  }

  let { onpaste, ondescribe }: Props = $props();

  const newRecipe = resolve('/(app)/recipes/new');

  /** One stroke path each, drawn on the 24px grid the app's other icons use. */
  const icons = {
    paste:
      'M8 4.5H6.5a1.5 1.5 0 0 0-1.5 1.5v13a1.5 1.5 0 0 0 1.5 1.5h11a1.5 1.5 0 0 0 1.5-1.5V6a1.5 1.5 0 0 0-1.5-1.5H16M9 3h6v3H9zM9 11h6M9 15h6',
    source: 'M12 3.5v11M7.5 10l4.5 4.5 4.5-4.5M4.5 15v3a2 2 0 0 0 2 2h11a2 2 0 0 0 2-2v-3',
    idea: 'M9 18h6M10 21h4M12 3a6 6 0 0 0-3.6 10.8c.6.5 1 1.2 1 2v.2h5.2v-.2c0-.8.4-1.5 1-2A6 6 0 0 0 12 3Z',
    photo:
      'M4 8a2 2 0 0 1 2-2h2l1.5-2h5L16 6h2a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2ZM15.5 12.5a3.5 3.5 0 1 1-7 0 3.5 3.5 0 0 1 7 0Z'
  };
</script>

{#snippet way(icon: string, title: string, body: string)}
  <svg
    class="way-icon"
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    stroke-width="1.8"
    stroke-linecap="round"
    stroke-linejoin="round"
    aria-hidden="true"
  >
    <path d={icon} />
  </svg>
  <span class="way-text">
    <span class="way-title">{title}</span>
    <span class="way-body">{body}</span>
  </span>
  <svg
    class="way-chevron"
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    stroke-width="2"
    stroke-linecap="round"
    stroke-linejoin="round"
    aria-hidden="true"
  >
    <path d="m9 6 6 6-6 6" />
  </svg>
{/snippet}

<!-- Offered after the name field, as doors not forms, so quick creation doesn't feel like a migration. -->
<section class="others" aria-labelledby="other-ways">
  <h2 class="others-title" id="other-ways">{m['editor.otherWays']()}</h2>

  <div class="ways">
    <!-- The whole tile is the control, so there is one label to read. -->
    {#if onpaste}
      <button type="button" class="way" onclick={() => onpaste('paste')}>
        {@render way(icons.paste, m['import.paste.title'](), m['import.paste.hint']())}
      </button>
    {:else}
      <!-- eslint-disable-next-line svelte/no-navigation-without-resolve -- The route is resolved; only the form to open is appended. -->
      <a class="way" href="{newRecipe}?start=paste">
        {@render way(icons.paste, m['import.paste.title'](), m['import.paste.hint']())}
      </a>
    {/if}

    <!-- Second: a link or a page of a book is how most recipes arrive on a phone, which cannot share into the app on iOS. -->
    {#if session.user?.assistance.read}
      {#if onpaste}
        <button type="button" class="way" onclick={() => onpaste('photo')}>
          {@render way(icons.photo, m['assist.photo.title'](), m['assist.photo.hint']())}
        </button>
      {:else}
        <!-- eslint-disable-next-line svelte/no-navigation-without-resolve -- The route is resolved; only the form to open is appended. -->
        <a class="way" href="{newRecipe}?start=photo">
          {@render way(icons.photo, m['assist.photo.title'](), m['assist.photo.hint']())}
        </a>
      {/if}
    {/if}
    <a class="way" href={resolve('/(app)/recipes/import')}>
      {@render way(icons.source, m['import.source.title'](), m['import.source.hint']())}
    </a>

    <!-- Only where an assistant is connected. -->
    {#if session.user?.assistance.draft}
      {#if ondescribe}
        <button type="button" class="way" onclick={ondescribe}>
          {@render way(icons.idea, m['assist.idea.title'](), m['assist.idea.hint']())}
        </button>
      {:else}
        <!-- eslint-disable-next-line svelte/no-navigation-without-resolve -- The route is resolved; only the form to open is appended. -->
        <a class="way" href="{newRecipe}?start=idea">
          {@render way(icons.idea, m['assist.idea.title'](), m['assist.idea.hint']())}
        </a>
      {/if}
    {/if}
  </div>
</section>

<style>
  .others {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    min-width: 0;
  }

  /* Sentence case: letterspaced capitals would be louder than the tiles they introduce. */
  .others-title {
    color: var(--text-muted);
    font-size: var(--text-sm);
    font-weight: var(--weight-medium);
  }

  .ways {
    display: grid;
    gap: var(--space-3);
    min-width: 0;
  }

  /* A row like a settings cell: what it is (icon), what it does (words), and that it leads on (chevron). */
  .way {
    display: grid;
    grid-template-columns: auto minmax(0, 1fr) auto;
    align-items: center;
    gap: var(--space-3);
    min-width: 0;
    padding: var(--space-4);
    border: 1px solid var(--border);
    border-radius: var(--radius-lg);
    background: none;
    color: inherit;
    font: inherit;
    text-align: start;
    text-decoration: none;
    cursor: pointer;
    transition:
      border-color var(--duration-fast) var(--ease-out),
      background-color var(--duration-fast) var(--ease-out),
      transform var(--duration-fast) var(--ease-out);
  }

  @media (hover: hover) {
    .way:hover {
      border-color: var(--border-strong);
      background: var(--surface-raised);
    }
  }

  .way:active {
    background: var(--surface-selected);
    transform: scale(var(--press-scale));
  }

  .way-icon {
    width: var(--space-6);
    height: var(--space-6);
    color: var(--accent);
  }

  .way-text {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
    min-width: 0;
  }

  .way-chevron {
    width: var(--space-4);
    height: var(--space-4);
    color: var(--text-subtle);
  }

  .way-title {
    font-weight: var(--weight-medium);
  }

  .way-body {
    color: var(--text-muted);
    font-size: var(--text-sm);
    line-height: var(--leading-normal);
  }

  @media (min-width: 40rem) {
    .ways {
      grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
    }
  }
</style>
