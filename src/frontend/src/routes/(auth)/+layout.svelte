<script lang="ts">
  import type { Snippet } from 'svelte';

  import { m } from '$shell/i18n';
  import LocalePicker from '$shell/LocalePicker.svelte';
  import ThemeToggle from '$shell/ThemeToggle.svelte';

  /**
   * The frame for the pages you see before you are anyone.
   *
   * No navigation: there is nowhere to go yet, and a disabled nav bar on a
   * sign-in screen is three dead ends and a decision.
   *
   * Language and appearance *are* here, though. Before you have an account
   * there is no stored preference to follow, only the device's — and someone
   * whose browser guessed wrong would otherwise have no way to correct it until
   * after they had read an English form and signed in.
   */
  interface Props {
    children: Snippet;
  }

  let { children }: Props = $props();
</script>

<main class="frame">
  <div class="card">
    <p class="brand">{m['app.name']()}</p>
    {@render children()}

    <footer class="preferences">
      <LocalePicker />
      <ThemeToggle />
    </footer>
  </div>
</main>

<style>
  .frame {
    display: flex;
    align-items: center;
    justify-content: center;
    min-height: 100dvh;
    padding: var(--space-6) var(--space-4);
  }

  .card {
    width: min(24rem, 100%);
    display: flex;
    flex-direction: column;
    gap: var(--space-6);
  }

  .preferences {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-3);
    margin-top: var(--space-4);
  }

  .brand {
    font-size: var(--text-xl);
    font-weight: var(--weight-semibold);
    letter-spacing: -0.01em;
  }
</style>
