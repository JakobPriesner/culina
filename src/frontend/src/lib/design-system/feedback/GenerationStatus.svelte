<script lang="ts">
  /** Plain, legible status copy with one optional quiet activity light. */
  interface Props {
    label: string;
    tone?: 'default' | 'on-media';
    align?: 'start' | 'center';
    /** Hidden when a larger work scene already explains the activity. */
    indicator?: boolean;
  }

  let { label, tone = 'default', align = 'start', indicator = true }: Props = $props();
</script>

<div
  class="status"
  class:on-media={tone === 'on-media'}
  class:centered={align === 'center'}
  role="status"
  aria-live="polite"
  aria-atomic="true"
>
  {#if indicator}<span class="orb" aria-hidden="true"></span>{/if}
  <span class="label">{label}</span>
</div>

<style>
  .status {
    display: inline-flex;
    align-items: center;
    gap: var(--space-3);
    max-width: 100%;
    color: var(--text-muted);
    font-size: var(--text-sm);
    font-weight: var(--weight-medium);
    line-height: var(--leading-normal);
  }
  .centered {
    flex-direction: column;
    gap: var(--space-2);
    justify-content: center;
    text-align: center;
  }
  .on-media {
    color: var(--text-on-media);
  }
  .orb {
    flex: none;
    width: 0.625rem;
    height: 0.625rem;
    border-radius: var(--radius-full);
    background: linear-gradient(135deg, var(--generating-1), var(--generating-3));
    animation: breathe 2.4s ease-in-out 2;
  }
  @keyframes breathe {
    50% {
      opacity: 0.45;
    }
  }
  @media (prefers-reduced-motion: reduce) {
    .orb {
      animation: none;
    }
  }
  @media (forced-colors: active) {
    .orb {
      background: CanvasText;
    }
  }
</style>
