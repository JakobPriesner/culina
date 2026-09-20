<script lang="ts">
  import { Skeleton } from '$ds';
  import { m } from '$shell/i18n';

  /**
   * The list, before it has arrived.
   *
   * The same rhythm as the real thing — a sticky section label with its count,
   * then rows of a checkbox, a name and an amount — so nothing moves when the
   * answer lands.
   */
  const sections = [
    [
      { name: '55%', amount: '3rem' },
      { name: '40%', amount: '4rem' },
      { name: '65%', amount: '2.5rem' }
    ],
    [
      { name: '45%', amount: '3.5rem' },
      { name: '60%', amount: '2rem' }
    ]
  ];
</script>

<div aria-busy="true" aria-label={m['shopping.loading']()}>
  {#each sections as rows, section (section)}
    <section class="section">
      <div class="heading-row">
        <div class="label"><Skeleton width="6rem" height="0.875rem" /></div>
        <Skeleton width="1.5rem" height="0.875rem" />
      </div>

      <ul class="list">
        {#each rows as item, row (row)}
          <li class="row">
            <div class="check">
              <Skeleton width="var(--space-6)" height="var(--space-6)" shape="text" />
            </div>
            <Skeleton width={item.name} height="1rem" />
            <span class="amount">
              <Skeleton width={item.amount} height="0.875rem" />
            </span>
          </li>
        {/each}
      </ul>
    </section>
  {/each}
</div>

<style>
  .section {
    margin-bottom: var(--space-8);
  }

  .heading-row {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-3);
    margin-bottom: var(--space-3);
    padding-block: var(--space-2);
  }

  .list {
    display: flex;
    flex-direction: column;
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .row {
    display: grid;
    grid-template-columns: auto minmax(0, 1fr) auto;
    align-items: center;
    gap: var(--space-3);
    min-height: var(--control-sm);
    margin-inline: calc(var(--space-3) * -1);
    padding-inline: var(--space-3);
    border-radius: var(--radius-md);
  }

  .check {
    display: flex;
    align-items: center;
  }

  .amount {
    display: flex;
    justify-content: flex-end;
  }
</style>
