<script lang="ts">
  import { m } from './i18n';
</script>

<span class="brand" role="img" aria-label={m['app.name']()}>
  <svg viewBox="0 0 40 40" fill="currentColor" aria-hidden="true">
    <rect x="17" y="9" width="6" height="2.5" rx="1.2" />
    <rect x="19" y="11" width="2" height="1.5" />
    <path
      d="M10 16.5 C11 13 15 12.5 20 12.5 C25 12.5 29 13 30 16.5 C30.2 17 29.8 17.5 29 17.5 H11 C10.2 17.5 9.8 17 10 16.5 Z"
    />
    <path
      fill-rule="evenodd"
      clip-rule="evenodd"
      d="M10.5 19 H7 C5.5 19 4.5 20 4.5 21.5 C4.5 23 5.5 24 7 24 H9.5 L10.5 27.5 C11.2 30.5 14 31.5 20 31.5 C26 31.5 28.8 30.5 29.5 27.5 L30.5 24 H33 C34.5 24 35.5 23 35.5 21.5 C35.5 20 34.5 19 33 19 H29.5 C29.5 19 10.5 19 10.5 19 Z M7 20.8 H9 V22.2 H7 C6.7 22.2 6.5 22 6.5 21.5 C6.5 21 6.7 20.8 7 20.8 Z M31 20.8 H33 C33.3 20.8 33.5 21 33.5 21.5 C33.5 22 33.3 22.2 33 22.2 H31 V20.8 Z"
    />
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
