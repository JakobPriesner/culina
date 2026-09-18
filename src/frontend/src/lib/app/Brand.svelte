<script lang="ts">
  import { m } from './i18n';
</script>

<span class="brand" role="img" aria-label={m['app.name']()}>
  <svg viewBox="0 0 40 40" fill="none" aria-hidden="true">
    <path
      d="M16 16.5C14.2 13.2 17.8 10.8 16 7.5C15.2 6.2 14.5 5.5 14 4.8M23.5 16.5C21.7 13.2 25.3 10.8 23.5 7.5C22.7 6.2 22 5.5 21.5 4.8M7 20.5H33M8.5 20.5C9.2 27.8 14.2 32 20 32C25.8 32 30.8 27.8 31.5 20.5M16 34H24"
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
    border-radius: var(--radius-full);
    background: var(--surface-accent-subtle);
    stroke: var(--accent);
    stroke-width: 1.8;
    stroke-linecap: round;
    stroke-linejoin: round;
  }
  .dot {
    color: var(--accent);
  }
</style>
