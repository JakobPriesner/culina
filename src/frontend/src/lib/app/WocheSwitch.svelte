<script lang="ts">
  import { onMount, type Snippet } from 'svelte';
  import { MediaQuery } from 'svelte/reactivity';

  import { goto } from '$app/navigation';
  import { resolve } from '$app/paths';
  import { SegmentedControl } from '$ds';
  import { session } from '$features/auth/session.svelte';
  import { shopping } from '$features/shopping/stores/shopping.svelte';

  import { headerAccessory } from './headerAccessory.svelte';
  import { m } from './i18n';
  import { woche, type WocheView } from './woche.svelte';

  /**
   * Flips Woche between its plan and its list, and remembers which was last so the Woche tab returns there. From
   * 80rem the week shows the list beside it, so the switch is not drawn over the plan there.
   * It rides in the header bar (see headerAccessory), so it stays in reach and the header's fade ends below it.
   */
  interface Props {
    view: WocheView;
    /** The view's own controls on the same bar, at its end: the plan's back, today and forward. */
    trailing?: Snippet;
  }

  let { view, trailing }: Props = $props();

  const listBeside = new MediaQuery('(min-width: 80rem)', false);

  $effect(() => {
    if (view === 'plan' && listBeside.current) return;

    return headerAccessory.show(bar);
  });

  const householdId = $derived(session.activeHouseholdId);

  $effect(() => {
    woche.visit(view);
  });

  // The list's segment says what is still to find, so "anything to buy?" needs no switching to look.
  $effect(() => {
    if (householdId) void shopping.load(householdId);
  });

  const listLabel = $derived(
    shopping.remaining > 0
      ? m['woche.switch.listCount']({ count: shopping.remaining })
      : m['woche.switch.list']()
  );

  function show(id: string) {
    if (id === view) return;
    const to = id === 'list' ? 'list' : 'plan';
    const returning = woche.leave(view, to, window.scrollY);
    void goto(to === 'list' ? resolve('/(app)/shopping') : resolve('/(app)/plan'), {
      noScroll: returning
    });
  }

  // Back through the switch: the view opens where it was left, not at its top (or, for the week, at today).
  onMount(() => {
    const y = woche.arrive(view);
    if (y === null) return;

    requestAnimationFrame(() => {
      window.scrollTo({ top: y });
      woche.arrived();
    });
  });
</script>

{#snippet bar()}
  <div class="switch" class:reading={view === 'list'}>
    <SegmentedControl
      label={m['woche.switch.label']()}
      selected={view}
      segments={[
        { id: 'plan', label: m['woche.switch.plan']() },
        { id: 'list', label: listLabel }
      ]}
      onselect={show}
    />
    {@render trailing?.()}
  </div>
{/snippet}

<style>
  /* Plan | Liste fills the bar; the view's own controls keep their size at its end, and wrap under it on the narrowest phones. */
  .switch {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-2);
  }

  .switch > :global(:first-child) {
    flex: 1 1 10rem;
    max-width: 22rem;
  }

  .switch > :global(:first-child .segment) {
    flex: 1;
  }

  /* The list is read in a column of its own measure; the bar lines up with it. */
  @media (min-width: 48rem) {
    .reading {
      max-width: var(--measure);
      margin-inline: auto;
    }
  }
</style>
