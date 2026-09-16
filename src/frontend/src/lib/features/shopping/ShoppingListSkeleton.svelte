<script lang="ts">
  import { Skeleton } from '$ds';
  import { m } from '$shell/i18n';

  /**
   * The list, before it has arrived.
   *
   * The same rhythm as the real thing — a short section label, then rows of a
   * tick, a name and an amount — so nothing moves when the answer lands. Two
   * sections of a few lines is roughly a real list's opening screen; guessing
   * higher only means more of the page rearranging itself later.
   */
  const sections = [
    ['70%', '45%', '60%'],
    ['55%', '65%']
  ];
</script>

<div aria-busy="true" aria-label={m['shopping.loading']()}>
  {#each sections as rows, section (section)}
    <div class="section">
      <div class="label"><Skeleton width="8rem" /></div>

      {#each rows as width, row (row)}
        <div class="row">
          <Skeleton width="var(--space-6)" height="var(--space-6)" />
          <Skeleton {width} />
        </div>
      {/each}
    </div>
  {/each}
</div>

<style>
  .section {
    margin-bottom: var(--space-8);
  }

  .label {
    /* The cap height of the uppercase label it stands in for, not its line box. */
    margin-bottom: var(--space-4);
  }

  .row {
    display: flex;
    align-items: center;
    gap: var(--space-3);
    min-height: var(--control-sm);
  }
</style>
