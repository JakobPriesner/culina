<script lang="ts">
  import { m } from './i18n';
</script>

<span class="brand" role="img" aria-label={m['app.name']()}>
  <svg viewBox="0 0 40 40" fill="currentColor" aria-hidden="true">
    <g transform="translate(20 20) scale(0.12) translate(-256 -255)">
      <path d="M128 222 C128 186 180 172 256 172 C332 172 384 186 384 222 Z" />
      <path d="M144 244 H368 V316 A68 68 0 0 1 300 384 H212 A68 68 0 0 1 144 316 Z" />
      <rect x="108" y="252" width="52" height="32" rx="16" />
      <rect x="352" y="252" width="52" height="32" rx="16" />
      <circle cx="256" cy="148" r="22" />
    </g>
  </svg>
  <span class="wordmark" aria-hidden="true">culina<span class="dot">.</span></span>
</span>

<style>
  .brand {
    display: inline-flex;
    align-items: center;
    gap: var(--space-2);
    color: var(--text);
    font-family: var(--font-editorial);
    font-size: 2rem;
    font-weight: var(--weight-regular);
    letter-spacing: -0.055em;
    line-height: 1;
  }
  svg {
    width: var(--control-sm);
    height: var(--control-sm);
    padding: var(--space-1);
    border-radius: var(--radius-md);
    background: var(--surface-accent-subtle);
    color: var(--accent);
  }
  /* A name, not a sentence: it never breaks, least of all before its dot. */
  .wordmark {
    white-space: nowrap;
  }
  .dot {
    color: var(--brand-accent);
  }
</style>
