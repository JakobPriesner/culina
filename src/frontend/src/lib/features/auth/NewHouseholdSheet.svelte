<script lang="ts">
  import { onDestroy } from 'svelte';

  import { Button, Field, Select, Sheet, TextInput } from '$ds';

  import { m } from '$shell/i18n';
  import FormFailure from './FormFailure.svelte';
  import { createHousehold } from './households.svelte';
  import { session } from './session.svelte';
  import { createSubmission } from './submission.svelte';

  /**
   * Starting another household while already in one.
   *
   * Two questions, and the second is the reason this is more than the welcome
   * screen's form: a new kitchen can start out seeing every recipe of one you
   * already cook in — the flat share that should have the family's recipes, the
   * test kitchen that should not clutter the real one. It is asked here because
   * the moment a household is made is the moment somebody knows what it is for.
   */
  interface Props {
    open: boolean;
    onclose: () => void;
    /** Once it exists and is the household being looked at. */
    oncreated: (householdId: string) => void;
  }

  let { open, onclose, oncreated }: Props = $props();

  let name = $state('');
  /** The household to inherit from, or '' for none. */
  let inheritsFrom = $state('');

  const submission = createSubmission();

  onDestroy(() => submission.dispose());

  // Emptied each time it opens, so a second household never starts with the
  // first one's name in it.
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

      // Read again rather than patched in: the new household arrives with a
      // role, and with the chain of households it now inherits from, and both
      // are the server's to say.
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
