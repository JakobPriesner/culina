<script lang="ts">
  import type { Snippet } from 'svelte';

  import { resolve } from '$app/paths';
  import Brand from '$shell/Brand.svelte';
  import { m } from '$shell/i18n';
  import LocalePicker from '$shell/LocalePicker.svelte';
  import ThemeToggle from '$shell/ThemeToggle.svelte';

  /**
   * The shell for pages that work without an account.
   *
   * Neither of the other two fits. `(app)` guards on a session and would send
   * a visitor to the sign-in page; `(auth)` is built around the story panel and
   * a form the width of a phone, which is not a recipe.
   *
   * What is here is what a stranger needs and nothing else: who is showing them
   * this, a way to read it in their own language and their own brightness, and
   * one honest line at the bottom saying where they are. No navigation — there
   * is nowhere else they may go.
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
    justify-content: space-between;
    gap: var(--space-4);
    min-height: var(--space-24);
  }

  .home {
    text-decoration: none;
  }

  .preferences {
    display: flex;
    align-items: center;
    gap: var(--space-4);
  }

  .main {
    flex: 1;
    min-width: 0;
  }

  /* The one place the visitor is told what Culina is, and it is a line rather
     than a banner: they came for a recipe, and the recipe is the page. */
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

  /* Paper is not a place you can navigate away from. */
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
