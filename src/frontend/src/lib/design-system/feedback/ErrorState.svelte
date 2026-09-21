<script lang="ts">
  import type { Snippet } from 'svelte';

  /**
   * Something failed, said in plain language.
   *
   * The page frame stays: replacing a whole screen with an error throws away
   * the person's sense of where they are, and there is usually nothing wrong
   * with the navigation they were using.
   *
   * The request id is shown in small print because it is the only thing that
   * connects "it did not work" to a line in a log. Nobody reads it until it
   * matters, and then it is the whole conversation.
   */
  interface Props {
    title: string;
    /** What failed, not what the server called it. No codes, no stack. */
    body: string;
    /** The way to try again. Present unless retrying genuinely cannot help. */
    action?: Snippet;
    /** Labelled, so the id is not a bare string nobody can interpret. */
    requestIdLabel?: string;
    requestId?: string | null;
    /**
     * Which heading this title is.
     *
     * Two of these replace the whole screen rather than sitting inside a page —
     * the session-unreachable gate and the root error boundary — and there is
     * no other heading on the document when they do. A page with no level-one
     * heading is a page a screen reader cannot summarise, so those two say so.
     */
    level?: 1 | 2;
  }

  let { title, body, action, requestIdLabel, requestId, level = 2 }: Props = $props();
</script>

<div class="error" role="alert">
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
