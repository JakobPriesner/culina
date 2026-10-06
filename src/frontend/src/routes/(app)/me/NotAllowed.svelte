<script lang="ts">
  import { resolve } from '$app/paths';
  import { Button } from '$ds';
  import { m } from '$shell/i18n';
  import Olli from '$shell/olli/Olli.svelte';
</script>

<div class="refused">
  <Olli pose="onDuty" size="lg" />

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
