<script lang="ts">
  import { resolve } from '$app/paths';
  import Brand from '$shell/Brand.svelte';
  import LocalePicker from '$shell/LocalePicker.svelte';
  import ThemeToggle from '$shell/ThemeToggle.svelte';
  import { m } from '$shell/i18n';
</script>

<div class="preview-note">
  <span>{m['preview.notice']()}</span><a href={resolve('/design')}>{m['preview.components']()} ↗</a>
</div>
<header class="header">
  <Brand /><span class="context">{m['preview.footer']()}</span>
  <div class="preferences"><LocalePicker compact /><ThemeToggle /></div>
</header>

<style>
  .preview-note {
    display: flex;
    justify-content: center;
    flex-wrap: wrap;
    gap: var(--space-4);
    padding: var(--space-2) var(--space-4);
    background: var(--surface-sunken);
    color: var(--text-muted);
    font-size: var(--text-xs);
  }

  .preview-note a {
    color: var(--text-muted);
  }

  .header {
    max-width: var(--layout-wide);
    margin-inline: auto;
    min-height: 5.5rem;
    padding: var(--space-4) var(--layout-gutter-end) var(--space-4) var(--layout-gutter-start);
    border-bottom: 1px solid var(--border);
    display: flex;
    align-items: center;
    gap: var(--space-8);
  }

  .context {
    color: var(--text-muted);
    font-size: var(--text-sm);
    font-family: var(--font-editorial);
    font-style: italic;
  }

  .preferences {
    margin-left: auto;
    display: flex;
    align-items: center;
    gap: var(--space-4);
  }

  @media (max-width: 63.999rem) {
    .context {
      display: none;
    }
  }

  @media (width < 64rem) {
    .preview-note {
      gap: var(--space-1);
    }

    .header {
      padding: var(--space-3) var(--layout-gutter-end) var(--space-3) var(--layout-gutter-start);
      gap: var(--space-2);
      flex-wrap: wrap;
    }

    .preferences {
      gap: var(--space-1);
    }
  }
</style>
