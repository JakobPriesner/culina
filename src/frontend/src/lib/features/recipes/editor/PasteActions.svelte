<script lang="ts">
  import { Button } from '$ds';
  import { m } from '$shell/i18n';

  interface Props {
    busy: boolean;
    nothingFound: boolean;
    reading: boolean;
    assistantAvailable: boolean;
    assisting: boolean;
    /** Nothing to hand the assistant, or too much. */
    assistantDisabled: boolean;
    /** Photos held but no assistant to read them. */
    photosUnreadable: boolean;
    tooLong: boolean;
    onpreview: () => void;
    onassist: () => void;
    oncancel: () => void;
  }

  let {
    busy,
    nothingFound,
    reading,
    assistantAvailable,
    assisting,
    assistantDisabled,
    photosUnreadable,
    tooLong,
    onpreview,
    onassist,
    oncancel
  }: Props = $props();
</script>

<div class="actions">
  <Button variant="primary" loading={busy} disabled={nothingFound || reading} onclick={onpreview}>
    {m['import.review.preview']()}
  </Button>

  {#if assistantAvailable}
    <Button loading={assisting} disabled={assisting || assistantDisabled} onclick={onassist}>
      {m['import.media.read']()}
    </Button>
  {/if}

  <Button variant="ghost" onclick={oncancel}>{m['import.paste.cancel']()}</Button>
</div>

{#if photosUnreadable}
  <p class="hint">{m['import.media.unavailable']()}</p>
{/if}
{#if tooLong}<p class="hint">{m['import.media.tooLong']()}</p>{/if}

<style>
  .actions {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-3);
  }

  .hint {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }
</style>
