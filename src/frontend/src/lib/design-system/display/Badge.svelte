<script lang="ts">
  import type { Snippet } from 'svelte';

  /** A short factual label (tag, diet, count); not a button: if pressing it would do something, use `Button`. */
  interface Props {
    children: Snippet;
    tone?: 'neutral' | 'accent' | 'success' | 'warning' | 'danger';
  }

  let { children, tone = 'neutral' }: Props = $props();
</script>

<span class="badge {tone}">{@render children()}</span>

<style>
  .badge {
    display: inline-flex;
    align-items: center;
    padding: 0 var(--space-2);
    border-radius: var(--radius-full);
    font-size: var(--text-xs);
    font-weight: var(--weight-medium);
    line-height: var(--space-6);
    white-space: nowrap;
  }

  .neutral {
    background: var(--surface-sunken);
    color: var(--text-muted);
  }

  .accent {
    background: var(--surface-accent-subtle);
    color: var(--text);
  }

  .success {
    background: var(--success-subtle);
    color: var(--text);
  }

  .warning {
    background: var(--warning-subtle);
    color: var(--text);
  }

  .danger {
    background: var(--danger-subtle);
    color: var(--text);
  }
</style>
