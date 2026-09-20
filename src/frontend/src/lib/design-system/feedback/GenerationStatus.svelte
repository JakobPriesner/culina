<script lang="ts">
  /**
   * A quiet, recognisable sign that generated work is arriving.
   *
   * Kept separate from a spinner: a spinner means "nothing to see yet", while
   * Culina's assistant usually has a stream behind it and is already sending
   * useful pieces. The travelling points read as a live connection without
   * pretending to know a percentage that the model never reports.
   */
  interface Props {
    label: string;
    tone?: 'default' | 'on-media';
    align?: 'start' | 'center';
  }

  let { label, tone = 'default', align = 'start' }: Props = $props();
</script>

<div
  class="status"
  class:on-media={tone === 'on-media'}
  class:centered={align === 'center'}
  role="status"
  aria-live="polite"
  aria-atomic="true"
>
  <span class="mark" aria-hidden="true">
    <svg viewBox="0 0 24 24">
      <path
        d="M12 1.7c.8 4.8 3.5 7.5 8.3 8.3-4.8.8-7.5 3.5-8.3 8.3-.8-4.8-3.5-7.5-8.3-8.3 4.8-.8 7.5-3.5 8.3-8.3Z"
      />
    </svg>
  </span>

  <span class="copy">
    <span class="label">{label}</span>
    <span class="flow" aria-hidden="true">
      <span></span><span></span><span></span><span></span>
    </span>
  </span>
</div>

<style>
  .status {
    display: inline-flex;
    align-items: center;
    gap: var(--space-2);
    max-width: 100%;
    color: var(--text-muted);
    font-size: var(--text-sm);
    line-height: var(--leading-normal);
  }

  .centered {
    flex-direction: column;
    justify-content: center;
    text-align: center;
  }

  .on-media {
    color: var(--text-on-media);
  }

  .mark {
    position: relative;
    display: grid;
    flex: 0 0 auto;
    width: 1.5rem;
    height: 1.5rem;
    place-items: center;
  }

  .mark::before {
    position: absolute;
    width: 100%;
    height: 100%;
    border: 1px solid currentColor;
    border-radius: var(--radius-full);
    content: '';
    opacity: 0.2;
    animation: breathe 2.4s var(--ease-out) infinite;
  }

  .mark svg {
    width: 0.85rem;
    height: 0.85rem;
    fill: currentColor;
    animation: glint 2.4s var(--ease-out) infinite;
  }

  .copy {
    display: flex;
    flex-direction: column;
    gap: 0.2rem;
    min-width: 0;
  }

  .centered .copy {
    align-items: center;
  }

  .label {
    font-weight: var(--weight-medium);
  }

  .flow {
    display: flex;
    gap: 0.22rem;
    height: 0.25rem;
    align-items: center;
  }

  .flow span {
    width: 0.25rem;
    height: 0.25rem;
    border-radius: var(--radius-full);
    background: currentColor;
    opacity: 0.18;
    animation: travel 1.6s var(--ease-out) infinite;
  }

  .flow span:nth-child(2) {
    animation-delay: 0.16s;
  }

  .flow span:nth-child(3) {
    animation-delay: 0.32s;
  }

  .flow span:nth-child(4) {
    animation-delay: 0.48s;
  }

  @keyframes breathe {
    0%,
    100% {
      opacity: 0.14;
      transform: scale(0.82);
    }

    50% {
      opacity: 0.32;
      transform: scale(1.08);
    }
  }

  @keyframes glint {
    0%,
    100% {
      opacity: 0.65;
      transform: rotate(0deg) scale(0.9);
    }

    50% {
      opacity: 1;
      transform: rotate(45deg) scale(1.08);
    }
  }

  @keyframes travel {
    0%,
    65%,
    100% {
      opacity: 0.18;
      transform: translateY(0) scale(0.8);
    }

    28% {
      opacity: 0.9;
      transform: translateY(-0.08rem) scale(1);
    }
  }

  @media (prefers-reduced-motion: reduce) {
    .mark::before,
    .mark svg,
    .flow span {
      animation: none;
    }

    .flow span:nth-child(2) {
      opacity: 0.35;
    }

    .flow span:nth-child(3) {
      opacity: 0.55;
    }

    .flow span:nth-child(4) {
      opacity: 0.75;
    }
  }
</style>
