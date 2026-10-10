<script lang="ts">
  import type { Snippet } from 'svelte';

  import { resolve } from '$app/paths';
  import Brand from '$shell/Brand.svelte';
  import { m } from '$shell/i18n';
  import LocalePicker from '$shell/LocalePicker.svelte';
  import ThemeToggle from '$shell/ThemeToggle.svelte';

  /**
   * The shell for pages that work without an account: who is showing this, language and brightness,
   * one line saying where they are, no navigation.
   */
  interface Props {
    children: Snippet;
  }

  let { children }: Props = $props();
</script>

<div class="frame">
  <header class="header">
    <a class="home" href={resolve('/(app)')}><Brand /></a>
    <div class="preferences"><LocalePicker compact /><ThemeToggle /></div>
  </header>

  <main class="main">{@render children()}</main>

  <footer class="footer">
    <span>{m['shared.footer']()}</span>
    <a href={resolve('/(app)')}>{m['shared.cta']()}</a>
  </footer>
</div>

<style>
  .frame {
    display: flex;
    flex-direction: column;
    min-height: 100dvh;
    max-width: var(--layout-wide);
    margin-inline: auto;
    padding-inline: var(--layout-gutter-start) var(--layout-gutter-end);
  }

  .header {
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    justify-content: space-between;
    gap: var(--space-4);
    min-height: var(--space-24);
  }

  .home {
    text-decoration: none;
  }

  /* Wraps like the header: the language picker and the toggle are wider than a 320 px screen's gutters. */
  .preferences {
    display: flex;
    flex-wrap: wrap;
    justify-content: flex-end;
    align-items: center;
    gap: var(--space-4);
  }

  .main {
    flex: 1;
    min-width: 0;
  }

  /*
   * The one place a visitor is told what Culina is; a line, not a banner, since the recipe is the
   * page.
   */
  .footer {
    display: flex;
    flex-wrap: wrap;
    align-items: baseline;
    gap: var(--space-2) var(--space-4);
    padding-block: var(--space-6) var(--space-12);
    border-top: 1px solid var(--border);
    margin-top: var(--space-12);
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .footer a {
    color: var(--text);
  }

  @media print {
    .header,
    .footer {
      display: none;
    }
  }

  @media (width < 64rem) {
    .preferences {
      gap: var(--space-1);
    }
  }
</style>
