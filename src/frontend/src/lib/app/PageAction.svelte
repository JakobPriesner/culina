<script lang="ts">
  import { Button } from '$ds';

  /**
   * A page's one main action, at the end of its title row at every width: an accent pill with a plus and a short verb.
   * The short label is what fits beside a phone's title; `label` names it in full and starts with the visible words.
   */
  interface Props {
    /** On screen: "Rezept", "Kochbuch", "Eintragen". */
    text: string;
    /** For assistive technology: "Rezept erstellen". */
    label?: string;
    href?: string;
    onclick?: () => void;
  }

  let { text, label, href, onclick }: Props = $props();
</script>

<Button variant="primary" size="sm" {href} {label} {onclick}>
  {#snippet icon()}
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      stroke-width="1.8"
      aria-hidden="true"
    >
      <path d="M12 5v14M5 12h14" stroke-linecap="round" />
    </svg>
  {/snippet}
  {text}
</Button>
