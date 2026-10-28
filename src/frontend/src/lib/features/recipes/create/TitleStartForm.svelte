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

  const titleId = 'new-recipe-title';

  /** Set by submitting nothing; cleared by typing. */
  let missing = $state(false);

  function submit(event: SubmitEvent) {
    event.preventDefault();

    if (!title.trim()) {
      missing = true;
      globalThis.document?.getElementById(titleId)?.focus();

      return;
    }

    onsubmit();
  }
</script>

<form class="start" onsubmit={submit} novalidate>
  <FormFailure {failure} />

  <Field
    id={titleId}
    label={m['editor.title']()}
    error={missing && !title.trim() ? m['editor.titleRequired']() : undefined}
  >
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
    --field-surface: var(--field-surface-inset);
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
