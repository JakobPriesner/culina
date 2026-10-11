<script lang="ts">
  import { Popover } from '$ds';
  import { m } from '$shell/i18n';

  interface Props {
    atLeast: boolean;
    estimated?: boolean;
    basis?: string;
    detail?: string | null;
  }

  let { atLeast, estimated = false, basis, detail = null }: Props = $props();
</script>

<span class="info">
  <Popover>
    {#snippet trigger(attributes)}
      <button type="button" {...attributes} aria-label={m['nutrition.calories.info']()}>
        <svg
          aria-hidden="true"
          viewBox="0 0 20 20"
          fill="none"
          stroke="currentColor"
          stroke-width="1.4"
        >
          <circle cx="10" cy="10" r="7.2" />
          <path d="M10 9v5" stroke-linecap="round" />
          <circle cx="10" cy="6.3" r=".8" fill="currentColor" stroke="none" />
        </svg>
      </button>
    {/snippet}
    <div class="explanation" role="region" aria-label={m['nutrition.calories.info']()}>
      {#if basis}<p class="basis">{basis}</p>{/if}
      <p>{atLeast ? m['nutrition.calories.lowerBound']() : m['nutrition.calories.calculated']()}</p>
      {#if estimated}<p>{m['nutrition.calories.estimated']()}</p>{/if}
      {#if detail}<p>{detail}</p>{/if}
    </div>
  </Popover>
</span>

<style>
  .info {
    position: relative;
    z-index: 1;
    display: inline-flex;
    vertical-align: middle;
  }
  button {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    width: 2rem;
    height: 2rem;
    padding: 0.4rem;
    border: 0;
    border-radius: var(--radius-full);
    background: none;
    color: inherit;
    cursor: pointer;
    opacity: 0.7;
  }
  button:hover {
    opacity: 1;
    background: var(--surface-sunken);
    color: var(--text);
  }
  svg {
    width: 1.1rem;
    height: 1.1rem;
  }
  .explanation {
    display: grid;
    gap: var(--space-2);
    max-width: 18rem;
    padding: var(--space-2);
    font-size: var(--text-sm);
    line-height: var(--leading-normal);
  }
  p {
    margin: 0;
  }
  .basis {
    font-weight: var(--weight-medium);
  }
  @media print {
    .info {
      display: none;
    }
  }
</style>
