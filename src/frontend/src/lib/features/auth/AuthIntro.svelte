<script lang="ts">
  import { SectionLabel } from '$ds';

  /** The heading of every signed-out form, so signing in, registering and resetting read as one place. */
  interface Props {
    title: string;
    eyebrow?: string;
    subtitle?: string;
    /** A longer title (a moment's message) steps down a size so it stays on two lines. */
    long?: boolean;
  }

  let { title, eyebrow, subtitle, long = false }: Props = $props();
</script>

<header class="intro">
  {#if eyebrow}<SectionLabel tone="accent" spacing="tight">{eyebrow}</SectionLabel>{/if}
  <h1 class="title" class:long>{title}</h1>
  {#if subtitle}<p class="subtitle">{subtitle}</p>{/if}
</header>

<style>
  .intro {
    margin-bottom: var(--space-6);
  }

  .title {
    font-size: var(--text-display);
    letter-spacing: -0.04em;
  }

  .title.long {
    font-size: var(--text-3xl);
  }

  .subtitle {
    margin-top: var(--space-3);
    color: var(--text-muted);
    font-size: var(--text-sm);
    line-height: var(--leading-relaxed);
  }
</style>
