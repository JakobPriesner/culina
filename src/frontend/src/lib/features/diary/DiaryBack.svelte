<script lang="ts">
  import { Chevron } from '$ds';
  import { m } from '$shell/i18n';

  /** The way back one step inside the "Eintragen" sheet: from own figures, a package, or an amount to the search. */
  interface Props {
    onclick: () => void;
  }

  let { onclick }: Props = $props();
</script>

<button class="back" type="button" {onclick}>
  <Chevron direction="back" />
  {m['diary.add.back']()}
</button>

<style>
  .back {
    display: inline-flex;
    align-items: center;
    align-self: flex-start;
    gap: var(--space-1);
    min-block-size: var(--control-sm);
    margin-inline-start: calc(-1 * var(--space-2));
    padding: 0 var(--space-2);
    border: 0;
    border-radius: var(--radius-md);
    background: transparent;
    color: var(--accent);
    font: inherit;
    font-weight: var(--weight-medium);
    cursor: pointer;
  }

  .back:focus-visible {
    outline: 2px solid var(--border-focus);
  }
</style>
