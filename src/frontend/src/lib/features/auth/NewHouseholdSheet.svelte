<script lang="ts">
  import { onDestroy } from 'svelte';

  import { Button, Field, Select, Sheet, TextInput } from '$ds';

  import { m } from '$shell/i18n';
  import FormFailure from './FormFailure.svelte';
  import { createHousehold } from './households.svelte';
  import { session } from './session.svelte';
  import { createSubmission } from './submission.svelte';

  /** Starting another household while in one; a new kitchen can inherit another's recipes, which is asked here because this is when its purpose is known. */
  interface Props {
    open: boolean;
    onclose: () => void;
    oncreated: (householdId: string) => void;
  }

  let { open, onclose, oncreated }: Props = $props();

  let name = $state('');
  let inheritsFrom = $state('');

  const submission = createSubmission();

  onDestroy(() => submission.dispose());

  // Emptied on open, so a second household never starts with the first one's name.
  $effect(() => {
    if (open) {
      name = '';
      inheritsFrom = '';
      submission.clear();
    }
  });

  const options = $derived([
    { value: '', label: m['household.inherit.none']() },
    ...session.households.map((h) => ({ value: h.householdId, label: h.name }))
  ]);

  const ready = $derived(name.trim().length > 0);

  async function create() {
    if (!ready || submission.inFlight) {
      return;
    }

    let created: string | null = null;

    const succeeded = await submission.run(async () => {
      const outcome = await createHousehold(name.trim(), inheritsFrom || null);

      if (typeof outcome !== 'string') {
        return outcome;
      }

      // Re-read, not patched in: the role and inheritance chain are the server's to say.
      await session.refresh();
      session.selectHousehold(outcome);
      created = outcome;

      return null;
    });

    if (succeeded && created) {
      oncreated(created);
    }
  }
</script>

<Sheet {open} title={m['household.new.title']()} closeLabel={m['household.new.close']()} {onclose}>
  <form
    class="form"
    onsubmit={(event) => {
      event.preventDefault();
      void create();
    }}
    novalidate
  >
    <FormFailure failure={submission.failure} />

    <Field label={m['household.new.name']()} required error={submission.errorFor('name')}>
      {#snippet children({ id, describedBy, invalid })}
        <TextInput {id} {describedBy} {invalid} bind:value={name} maxlength={80} required />
      {/snippet}
    </Field>

    {#if session.households.length > 0}
      <Field label={m['household.inherit.field']()} hint={m['household.inherit.body']()}>
        {#snippet children({ id, describedBy, invalid })}
          <Select {id} {describedBy} {invalid} bind:value={inheritsFrom} {options} />
        {/snippet}
      </Field>
    {/if}

    <!-- Inside the form so Enter submits it. -->
    <Button type="submit" variant="primary" disabled={!ready} loading={submission.showingProgress}>
      {m['welcome.create']()}
    </Button>
  </form>
</Sheet>

<style>
  .form {
    display: flex;
    flex-direction: column;
    gap: var(--space-6);
  }
</style>
