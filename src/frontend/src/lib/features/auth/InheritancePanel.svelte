<script lang="ts">
  import type { AppError } from '$api';
  import { Field, Select } from '$ds';

  import { m } from '$shell/i18n';
  import { toaster } from '$shell/toaster.svelte';
  import FormFailure from './FormFailure.svelte';
  import { setInheritance } from './households.svelte';
  import { session } from './session.svelte';

  /**
   * Whose recipes this household sees besides its own.
   *
   * Says what is true first — which household, and through it which others —
   * because a chain is easy to set up and easy to forget, and a member who
   * cannot change it still wants to know why somebody else's recipes are in
   * their library. An owner gets the choice under it, saved the moment it is
   * made: it is one value, and a save button for one value is a second step
   * that only exists to be forgotten.
   */
  interface Props {
    householdId: string;
  }

  let { householdId }: Props = $props();

  let saving = $state(false);
  let failure = $state<AppError | null>(null);
  /** Bumped when a save fails, so the select goes back to what is really stored. */
  let attempt = $state(0);

  const household = $derived(session.households.find((h) => h.householdId === householdId));
  const chain = $derived(household?.inheritsFrom ?? []);
  const owner = $derived(household?.role === 'owner');

  /**
   * The households it could inherit from: the other ones this person is in.
   * The current one stays on the list even when they are not in it — somebody
   * else chose it — or the select would claim it inherits nothing.
   */
  const options = $derived.by(() => {
    const others = session.households
      .filter((h) => h.householdId !== householdId)
      .map((h) => ({ value: h.householdId, label: h.name }));
    const current = chain[0];

    if (current && !others.some((option) => option.value === current.householdId)) {
      others.push({ value: current.householdId, label: current.name });
    }

    return [{ value: '', label: m['household.inherit.none']() }, ...others];
  });

  async function change(value: string) {
    saving = true;
    failure = await setInheritance(householdId, value || null);

    if (failure) {
      attempt += 1;
    } else {
      // The chain it now carries names households only the server can name.
      await session.refresh();

      const parent = session.householdName(value);

      toaster.show({
        message: () =>
          value && parent
            ? m['household.inherit.current']({ name: parent })
            : m['household.inherit.nothing'](),
        tone: 'success'
      });
    }

    saving = false;
  }
</script>

<div class="panel">
  <p class="status">
    {chain[0]
      ? m['household.inherit.current']({ name: chain[0].name })
      : m['household.inherit.nothing']()}
    {#if chain.length > 1}
      {m['household.inherit.through']({
        names: chain
          .slice(1)
          .map((h) => h.name)
          .join(', ')
      })}
    {/if}
  </p>

  <FormFailure {failure} />

  {#if owner}
    <Field label={m['household.inherit.field']()}>
      {#snippet children({ id, describedBy, invalid })}
        {#key attempt}
          <Select
            {id}
            {describedBy}
            {invalid}
            value={chain[0]?.householdId ?? ''}
            {options}
            disabled={saving}
            onchange={(value) => void change(value)}
          />
        {/key}
      {/snippet}
    </Field>
  {:else}
    <p class="hint">{m['problem.households.not_owner']()}</p>
  {/if}
</div>

<style>
  .panel {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    max-width: var(--measure);
  }

  .hint {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }
</style>
