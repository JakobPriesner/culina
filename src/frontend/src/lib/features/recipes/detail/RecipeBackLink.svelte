<script lang="ts">
  import { resolve } from '$app/paths';
  import { m } from '$shell/i18n';
</script>

<p class="back">
  <a href={resolve('/(app)')} aria-label={m['recipe.back']()}>
    <span aria-hidden="true">←</span>
    <span class="back-label">{m['recipe.back']()}</span>
  </a>
</p>

<style>
  .back {
    margin-bottom: var(--space-6);
    font-size: var(--text-sm);
  }

  .back a {
    color: var(--text-muted);
    text-decoration: none;
  }

  .back a:hover {
    color: var(--text);
  }

  /* Keep the explicit way back that preserves the library's search state, but
     let the arrow carry it on a phone. The link's aria-label remains the full
     name, so compact is only visual. */
  @media (width < 52rem) {
    .back {
      margin-bottom: var(--space-2);
    }

    .back a {
      display: inline-grid;
      place-items: center;
      width: var(--control-sm);
      min-height: var(--control-sm);
      border: 1px solid var(--border);
      border-radius: var(--radius-full);
      background: var(--surface);
      font-size: var(--text-lg);
    }

    .back-label {
      display: none;
    }
  }

  /* Paper cannot be navigated. */
  @media print {
    .back {
      display: none;
    }
  }
</style>
