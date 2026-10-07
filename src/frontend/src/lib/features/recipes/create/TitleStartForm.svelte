<script lang="ts">
  import { Button, Field, TextInput } from '$ds';
  import FormFailure from '$features/auth/FormFailure.svelte';
  import type { AppError } from '$api';
  import { m } from '$shell/i18n';

  interface Props {
    title: string;
    failure: AppError | null;
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
  /* The page's one panel, so the eye has somewhere to land: "start here", not "here is a form". */
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

  /*
   * On phones the lone action spans the width; wider, a column-wide button would read as a banner.
   */
  @media (max-width: 30rem) {
    .start :global(.button) {
      width: 100%;
    }
  }
</style>
