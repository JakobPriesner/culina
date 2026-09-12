<script lang="ts">
  /**
   * A person, at a glance.
   *
   * Falls back to initials rather than to a generic silhouette: in a household
   * of four, four identical grey heads identify nobody. The initials are
   * derived here rather than asked for, so every caller gets the same answer.
   */
  interface Props {
    /** The full name. Also the accessible name. */
    name: string;
    src?: string | null;
    size?: 'sm' | 'md';
  }

  let { name, src, size = 'md' }: Props = $props();

  const initials = $derived(
    name
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => [...part][0]?.toUpperCase() ?? '')
      .join('')
  );
</script>

{#if src}
  <img class="avatar {size}" {src} alt={name} loading="lazy" decoding="async" />
{:else}
  <span class="avatar {size} initials" role="img" aria-label={name}>{initials}</span>
{/if}

<style>
  .avatar {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    flex: none;
    border-radius: var(--radius-full);
    object-fit: cover;
    background: var(--surface-accent-subtle);
    color: var(--text);
    font-weight: var(--weight-semibold);
    /* Never larger than the box, whatever the source image's shape. */
    overflow: hidden;
  }

  .sm {
    width: var(--space-6);
    height: var(--space-6);
    font-size: var(--text-xs);
  }

  .md {
    width: var(--space-12);
    height: var(--space-12);
    font-size: var(--text-base);
  }
</style>
