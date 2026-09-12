<script lang="ts">
  import type { Snippet } from 'svelte';

  /**
   * An icon at a known size.
   *
   * Decorative by default — `aria-hidden` — because an icon almost always sits
   * beside the words it illustrates, and announcing both says everything twice.
   * Give it a `label` only when it is genuinely the only thing carrying the
   * meaning, which usually means it should have been text.
   */
  interface Props {
    children: Snippet;
    size?: 'sm' | 'md' | 'lg';
    label?: string;
  }

  let { children, size = 'md', label }: Props = $props();
</script>

<span
  class="icon {size}"
  role={label ? 'img' : undefined}
  aria-label={label}
  aria-hidden={label ? undefined : 'true'}
>
  {@render children()}
</span>

<style>
  .icon {
    display: inline-block;
    flex: none;
    color: inherit;
  }

  .icon :global(svg) {
    display: block;
    width: 100%;
    height: 100%;
  }

  .sm {
    width: var(--space-4);
    height: var(--space-4);
  }

  .md {
    width: var(--space-6);
    height: var(--space-6);
  }

  .lg {
    width: var(--space-8);
    height: var(--space-8);
  }
</style>
