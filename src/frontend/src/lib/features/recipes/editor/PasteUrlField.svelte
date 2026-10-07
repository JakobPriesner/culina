<script lang="ts">
  import { Button, Field, TextInput } from '$ds';
  import { m } from '$shell/i18n';

  /** The address of a recipe page, with the buttons that read it. */
  interface Props {
    url: string;
    /** Something else is in flight, so the field holds still. */
    locked: boolean;
    reading: boolean;
    /** The assistant is already writing a draft; reading would race it. */
    asking: boolean;
    canPasteClipboard: boolean;
    /** The address was typed over, so whatever was read from the old one is stale. */
    onedit: () => void;
    onread: () => void;
    onpasteclipboard: () => void;
  }

  let {
    url = $bindable(),
    locked,
    reading,
    asking,
    canPasteClipboard,
    onedit,
    onread,
    onpasteclipboard
  }: Props = $props();
</script>

<div class="from-url">
  <Field label={m['import.url.label']()}>
    {#snippet children({ id, describedBy, invalid })}
      <TextInput
        {id}
        {describedBy}
        {invalid}
        type="url"
        inputmode="url"
        placeholder="https://"
        bind:value={url}
        disabled={locked}
        oninput={onedit}
      />
    {/snippet}
  </Field>

  {#if !url.trim() && canPasteClipboard}
    <Button variant="ghost" onclick={onpasteclipboard}>
      {m['import.url.pasteClipboard']()}
    </Button>
  {/if}

  <Button loading={reading} disabled={!url.trim() || asking} onclick={onread}>
    {m['import.url.read']()}
  </Button>
</div>

<style>
  .from-url {
    display: flex;
    flex-wrap: wrap;
    align-items: flex-end;
    gap: var(--space-3);
  }

  .from-url :global(> :first-child) {
    flex: 1 1 16rem;
    min-width: 0;
  }
</style>
