<script lang="ts">
  import { Button, Field, TextInput } from '$ds';
  import FormFailure from '$features/auth/FormFailure.svelte';
  import type { AppError } from '$api';
  import { m } from '$shell/i18n';

  interface Props {
    title: string;
    failure: AppError | null;
    /** Whether a slow create has gone on long enough to show progress. */
    loading: boolean;
    onsubmit: () => void;
  }

  let { title = $bindable(), failure, loading, onsubmit }: Props = $props();

  function submit(event: SubmitEvent) {
    event.preventDefault();
    onsubmit();
  }
</script>

<form class="start" onsubmit={submit} novalidate>
  <FormFailure {failure} />

  <Field label={m['editor.title']()}>
    {#snippet children({ id, describedBy, invalid })}
      <TextInput
        {id}
        {describedBy}
        {invalid}
        size="display"
        placeholder={m['editor.titlePlaceholder']()}
        bind:value={title}
      />
    {/snippet}
  </Field>

  <Button type="submit" variant="primary" size="lg" {loading}>
    {m['editor.create']()}
  </Button>
</form>

<style>
  /*
   * The one panel on the page, so the eye has somewhere to land.
   *
   * A card is not the default wrapper for a block of content, and this is not
   * decoration: it is the difference between "here is a form" and "start
   * here". Everything below it is deliberately outside.
   */
  .start {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    gap: var(--space-4);
    min-width: 0;
    padding: var(--space-6);
    border: 1px solid var(--border);
    border-radius: var(--radius-lg);
    background: var(--surface-raised);
  }

  .start :global(.field) {
    width: 100%;
  }

  /* The page's one action, across the thumb's reach. Wide enough and it sizes
     to its own words again, where a button the width of a column would be a
     banner. */
  @media (max-width: 30rem) {
    .start :global(.button) {
      width: 100%;
    }
  }
</style>
