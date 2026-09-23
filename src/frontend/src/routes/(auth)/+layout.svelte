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
    <div class="preferences"><LocalePicker compact /><ThemeToggle /></div>
  </header>
  <main class="main">
    <aside class="story" aria-label={m['auth.story.label']()}>
      <!-- Lazy, because below 64rem this whole panel is display: none, and an
           eager image inside a hidden panel is still fetched: 260 kB that a
           phone downloaded to show nothing. A lazy one is only fetched when it
           could be seen, which on a wide screen is straight away. -->
      <img
        src="{base}/images/culina-orzo.webp"
        alt=""
        width="1536"
        height="1024"
        loading="lazy"
        decoding="async"
      />
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
    padding-inline: var(--layout-gutter-start) var(--layout-gutter-end);
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
    /* Wraps for the same reason the header around it does. These are two
       controls whose words grow with somebody's text size, and at 200% on a
       320px screen they are wider than the screen — which is a page that
       scrolls sideways, not a row that is slightly too long. */
    flex-wrap: wrap;
    justify-content: flex-end;
    align-items: center;
    gap: var(--space-4);
  }
  .main {
    display: grid;
    grid-template-columns: minmax(0, 1.1fr) minmax(0, 1fr);
    align-items: center;
    flex: 1;
    gap: var(--space-16);
    padding-block: var(--space-6);
  }
  .story {
    align-self: stretch;
    background: var(--surface-feature);
    color: var(--text-on-feature);
    border-radius: var(--radius-lg);
    overflow: hidden;
    display: flex;
    flex-direction: column;
    justify-content: flex-start;
  }
  .story img {
    width: 100%;
    height: clamp(16rem, 42vh, 28rem);
    object-fit: cover;
  }
  .story-copy {
    padding: var(--space-8) var(--space-12);
  }
  .eyebrow {
    font-size: var(--text-xs);
    letter-spacing: 0.12em;
    text-transform: uppercase;
    color: var(--text-on-feature);
    margin-bottom: var(--space-3);
  }
  h2 {
    font-family: var(--font-editorial);
    font-size: var(--text-3xl);
    font-weight: var(--weight-regular);
    letter-spacing: -0.04em;
  }
  .description {
    color: var(--text-on-feature);
    font-size: var(--text-sm);
    line-height: var(--leading-relaxed);
    max-width: 40ch;
    margin-top: var(--space-4);
  }
  .form-side {
    min-width: 0;
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
  @media (width < 64rem) {
    .frame {
      padding-inline: var(--layout-gutter-start) var(--layout-gutter-end);
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
      min-width: 0;
      padding: 0;
    }
    .footer {
      text-align: center;
    }
  }
</style>
