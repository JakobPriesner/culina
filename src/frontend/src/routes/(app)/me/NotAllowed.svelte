<script lang="ts">
  import { resolve } from '$app/paths';
  import { Button } from '$ds';
  import { m } from '$shell/i18n';
  import Olla from '$shell/olla/Olla.svelte';

  /**
   * A settings page that is the administrator's, opened by somebody who isn't.
   *
   * Told as a kitchen tells it: Olla on duty, holding the order, or — with
   * Olla turned off — the order itself on the rail, saying who cooks it. Both
   * are decoration; the heading and the line under it say it in words.
   */
  interface Props {
    /** The name of the page that was refused, as the settings rail calls it. */
    item: string;
  }

  let { item }: Props = $props();
</script>

<div class="refused">
  <Olla pose="onDuty" size="lg">
    {#snippet fallback()}
      <div class="pass" aria-hidden="true">
        <span class="rail"></span>
        <div class="ticket">
          <p class="order">{m['notAllowed.ticket.order']()}</p>
          <p>{m['notAllowed.ticket.table']()}</p>
          <hr />
          <p>1 × {item}</p>
          <hr />
          <p class="chef">{m['notAllowed.ticket.chef']()}</p>
        </div>
      </div>
    {/snippet}
  </Olla>

  <h1 class="title">{m['notAllowed.title']()}</h1>
  <p class="body">{m['notAllowed.body']()}</p>
  <Button variant="primary" href={resolve('/(app)/me')}>{m['notAllowed.back']()}</Button>
</div>

<style>
  .refused {
    display: grid;
    justify-items: center;
    gap: var(--space-3);
    max-width: 32rem;
    margin-inline: auto;
    padding-block: var(--space-4) var(--space-12);
    text-align: center;
  }

  .pass {
    display: grid;
    justify-items: center;
    width: min(100%, 16rem);
    margin-bottom: var(--space-6);
  }

  .rail {
    width: 100%;
    height: var(--space-2);
    border-radius: var(--radius-full);
    background: var(--border-strong);
  }

  /* Torn along the bottom like a ticket off the printer. A mask reads only
     alpha, so any opaque token draws the paper. */
  .ticket {
    --tooth: 0.5rem;

    width: 80%;
    margin-top: calc(var(--space-1) * -1);
    padding: var(--space-4) var(--space-4) var(--space-6);
    background: var(--surface-raised);
    box-shadow: var(--shadow-card);
    color: var(--text);
    font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
    font-size: var(--text-sm);
    line-height: var(--leading-normal);
    text-align: start;
    text-transform: uppercase;
    transform-origin: 50% 0;
    mask: conic-gradient(
        from -45deg at bottom,
        transparent,
        var(--text) 1deg 89deg,
        transparent 90deg
      )
      50% / calc(2 * var(--tooth)) 100%;
  }

  @media (prefers-reduced-motion: no-preference) {
    .ticket {
      animation: sway 5s ease-in-out infinite;
    }
  }

  @keyframes sway {
    0%,
    100% {
      transform: rotate(-2deg);
    }
    50% {
      transform: rotate(2.5deg);
    }
  }

  .order {
    font-size: var(--text-lg);
    font-weight: var(--weight-semibold);
    letter-spacing: 0.05em;
  }

  hr {
    margin-block: var(--space-2);
    border: 0;
    border-top: 1px dashed var(--border-strong);
  }

  .chef {
    color: var(--text-danger);
    font-weight: var(--weight-semibold);
    letter-spacing: 0.08em;
    text-align: center;
  }

  .title {
    font-family: var(--font-editorial);
    font-size: var(--text-2xl);
    font-weight: var(--weight-regular);
    letter-spacing: -0.025em;
    text-wrap: balance;
  }

  .body {
    margin-bottom: var(--space-2);
    color: var(--text-muted);
    text-wrap: pretty;
  }
</style>
