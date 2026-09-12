<script lang="ts">
  import type { Snippet } from 'svelte';
  import { base } from '$app/paths';
  import Brand from '$shell/Brand.svelte';
  import { m } from '$shell/i18n';
  import LocalePicker from '$shell/LocalePicker.svelte';
  import ThemeToggle from '$shell/ThemeToggle.svelte';
  interface Props {
    children: Snippet;
  }
  let { children }: Props = $props();
</script>

<div class="frame">
  <header class="header">
    <Brand />
    <div class="preferences"><LocalePicker /><ThemeToggle /></div>
  </header>
  <main class="main">
    <aside class="story" aria-label={m['auth.story.label']()}>
      <img src="{base}/images/culina-orzo.webp" alt="" width="1536" height="1024" />
      <div class="story-copy">
        <p class="eyebrow">{m['auth.story.eyebrow']()}</p>
        <h2>{m['auth.story.title']()}</h2>
        <p class="description">{m['auth.story.body']()}</p>
      </div>
    </aside>
    <div class="form-side"><div class="form-content">{@render children()}</div></div>
  </main>
  <footer class="footer">{m['auth.story.footer']()}</footer>
</div>

<style>
  .frame {
    min-height: 100dvh;
    display: flex;
    flex-direction: column;
    max-width: var(--layout-wide);
    margin-inline: auto;
    padding-inline: var(--space-8);
  }
  .header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    min-height: var(--space-24);
    gap: var(--space-4);
  }
  .preferences {
    display: flex;
    align-items: center;
    gap: var(--space-4);
  }
  .main {
    display: grid;
    grid-template-columns: 1.1fr 1fr;
    align-items: center;
    flex: 1;
    gap: var(--space-16);
    padding-block: var(--space-6);
  }
  .story {
    align-self: stretch;
    background: var(--surface-sunken);
    border-radius: var(--radius-lg);
    overflow: hidden;
    display: flex;
    flex-direction: column;
    justify-content: center;
  }
  .story img {
    width: 100%;
    height: clamp(14rem, 34vh, 24rem);
    object-fit: cover;
  }
  .story-copy {
    padding: var(--space-8);
  }
  .eyebrow {
    font-size: var(--text-xs);
    letter-spacing: 0.12em;
    text-transform: uppercase;
    color: var(--text-muted);
    margin-bottom: var(--space-3);
  }
  h2 {
    font-family: var(--font-editorial);
    font-size: var(--text-3xl);
    font-weight: var(--weight-regular);
    letter-spacing: -0.04em;
  }
  .description {
    color: var(--text-muted);
    max-width: 40ch;
    margin-top: var(--space-4);
  }
  .form-side {
    padding-block: var(--space-8);
  }
  .form-content {
    width: min(24rem, 100%);
    margin-inline: auto;
  }
  .footer {
    padding-block: var(--space-6);
    font-size: var(--text-xs);
    color: var(--text-muted);
  }
  @media (max-width: 63.999rem) {
    .main {
      gap: var(--space-8);
    }
    .story-copy {
      padding: var(--space-6);
    }
  }
  @media (max-width: 47.999rem) {
    .frame {
      padding-inline: var(--space-6);
    }
    .header {
      min-height: var(--space-24);
      flex-wrap: wrap;
      padding-block: var(--space-4);
    }
    .preferences {
      gap: var(--space-1);
    }
    .main {
      display: block;
      align-content: center;
      padding-block: var(--space-8);
    }
    .story {
      display: none;
    }
    .form-side {
      padding: 0;
    }
    .footer {
      text-align: center;
    }
  }
</style>
