<script lang="ts">
  import { resolve } from '$app/paths';
  import { session } from '$features/auth/session.svelte';
  import { m } from '$shell/i18n';

  interface Props {
    onpaste: () => void;
    ondescribe: () => void;
  }

  let { onpaste, ondescribe }: Props = $props();
</script>

<!-- Offered after the name field, as doors not forms, so quick creation doesn't feel like a migration. -->
<section class="others" aria-labelledby="other-ways">
  <h2 class="others-title" id="other-ways">{m['editor.otherWays']()}</h2>

  <div class="ways">
    <!-- The whole tile is the control, so there is one label to read. -->
    <button type="button" class="way" onclick={onpaste}>
      <span class="way-title">{m['import.paste.title']()}</span>
      <span class="way-body">{m['import.paste.hint']()}</span>
    </button>

    <a class="way" href={resolve('/(app)/recipes/import')}>
      <span class="way-title">{m['import.source.title']()}</span>
      <span class="way-body">{m['import.source.hint']()}</span>
    </a>

    <!-- Only where an assistant is connected. -->
    {#if session.user?.assistance.draft}
      <button type="button" class="way" onclick={ondescribe}>
        <span class="way-title">{m['assist.idea.title']()}</span>
        <span class="way-body">{m['assist.idea.hint']()}</span>
      </button>
    {/if}

    {#if session.user?.assistance.read}
      <button type="button" class="way" onclick={onpaste}>
        <span class="way-title">{m['assist.photo.title']()}</span>
        <span class="way-body">{m['assist.photo.hint']()}</span>
      </button>
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

  .way {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    gap: var(--space-1);
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
      background-color var(--duration-fast) var(--ease-out);
  }

  .way:hover {
    border-color: var(--border-strong);
    background: var(--surface-raised);
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
