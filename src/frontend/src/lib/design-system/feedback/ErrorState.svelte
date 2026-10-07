<script lang="ts">
  import type { Snippet } from 'svelte';

  /**
   * Something failed, in plain language, inside the page frame (replacing the screen loses the person's
   * place). The request id is small print: the only thing connecting "it did not work" to a log line.
   */
  interface Props {
    title: string;
    /** What failed, not what the server called it. No codes, no stack. */
    body: string;
    /** The way to try again. Present unless retrying genuinely cannot help. */
    action?: Snippet;
    /** A character above the title, when the app has one to show. */
    art?: Snippet;
    /** Labelled, so the id is not a bare string nobody can interpret. */
    requestIdLabel?: string;
    requestId?: string | null;
    /** Heading level; the session gate and root error boundary replace the whole screen and need a level-one heading. */
    level?: 1 | 2;
  }

  let { title, body, action, art, requestIdLabel, requestId, level = 2 }: Props = $props();
</script>

<div class="error" role="alert">
  {#if art}{@render art()}{/if}
  <svelte:element this={level === 1 ? 'h1' : 'h2'} class="title">{title}</svelte:element>
  <p class="body">{body}</p>

  {#if action}
    <div class="action">{@render action()}</div>
  {/if}

  {#if requestId && requestIdLabel}
    <p class="reference">
      {requestIdLabel}
      <code>{requestId}</code>
    </p>
  {/if}
</div>

<style>
  .error {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: var(--space-3);
    max-width: 32rem;
    margin-inline: auto;
    padding: var(--space-12) var(--space-4);
    text-align: center;
  }

  .title {
    font-size: var(--text-xl);
  }

  .body {
    color: var(--text-muted);
    text-wrap: pretty;
  }

  .action {
    margin-top: var(--space-2);
  }

  .reference {
    color: var(--text-subtle);
    font-size: var(--text-xs);
  }

  code {
    font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
    user-select: all;
  }
</style>
