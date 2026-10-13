<script lang="ts">
  import { tick } from 'svelte';

  import { ErrorCodes, type AppError } from '$api';
  import { Button, IconButton, Sheet, Skeleton } from '$ds';
  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';

  import { moveLine, readShelfOrder, type ShelfLine } from './shelfOrder';
  import { cookbooks } from './stores/cookbooks.svelte';

  /**
   * Arranging a chosen shelf: every recipe on it as a line with Move up and Move down, native buttons so a
   * keyboard and a screen reader arrange it the same way a finger does (dragging alone would leave them out).
   * Nothing is sent until Save, so a slip is undone by Cancel, and the whole order goes at once.
   */
  interface Props {
    open: boolean;
    householdId: string;
    cookbookId: string;
    onclose: () => void;
    /** After a saved order, so the page reads the shelf again. */
    onarranged: () => void;
  }

  let { open, householdId, cookbookId, onclose, onarranged }: Props = $props();

  let lines = $state<readonly ShelfLine[]>([]);
  let original: readonly string[] = [];
  let status = $state<'loading' | 'ready' | 'failed'>('loading');
  let saving = $state(false);
  let failure = $state<string | null>(null);
  let announcement = $state('');
  let list = $state<HTMLOListElement | null>(null);

  const changed = $derived(lines.some((line, index) => line.id !== original[index]));

  async function load() {
    status = 'loading';

    const read = await readShelfOrder(householdId, cookbookId);

    if (!read.ok) {
      status = 'failed';

      return;
    }

    lines = read.lines;
    original = read.lines.map((line) => line.id);
    status = 'ready';
  }

  // Read afresh on every open: the shelf may have changed since, and a stale order would only earn a 412.
  $effect(() => {
    if (open && householdId && cookbookId) {
      failure = null;
      announcement = '';
      void load();
    }
  });

  async function move(index: number, by: -1 | 1) {
    const next = moveLine(lines, index, by);

    if (next === lines) {
      return;
    }

    lines = next;

    const to = index + by;
    const line = lines[to]!;

    announcement = m['cookbooks.arrange.moved']({
      title: line.title,
      position: to + 1,
      count: lines.length
    });

    // Focus follows the recipe, so pressing again keeps moving it; at an end, the way back.
    await tick();

    const row = list?.querySelector<HTMLElement>(`[data-line="${line.id}"]`);
    const same = row?.querySelector<HTMLButtonElement>(`[data-move="${by}"] button`);
    const other = row?.querySelector<HTMLButtonElement>(`[data-move="${-by}"] button`);

    (same && !same.disabled ? same : other)?.focus();
  }

  async function save() {
    if (!changed || saving) {
      return;
    }

    saving = true;
    failure = null;

    const refused: AppError | null = await cookbooks.arrange(
      cookbookId,
      lines.map((line) => line.id)
    );

    saving = false;

    if (!refused) {
      onarranged();
      onclose();

      return;
    }

    // Changed elsewhere: show the shelf as it is now rather than an order that can no longer be saved.
    if (refused.code === ErrorCodes.versionMismatch || refused.status === 412) {
      failure = m['cookbooks.arrange.changed']();
      await cookbooks.load(cookbookId);
      await load();

      return;
    }

    failure = explain(refused);
  }
</script>

<Sheet
  {open}
  title={m['cookbooks.arrange.title']()}
  closeLabel={m['cookbooks.arrange.close']()}
  {onclose}
>
  <div class="body">
    <p class="intro">{m['cookbooks.arrange.intro']()}</p>

    {#if status === 'loading'}
      <div class="lines" aria-busy="true">
        {#each [0, 1, 2, 3] as row (row)}
          <Skeleton shape="block" height="var(--control-md)" />
        {/each}
      </div>
    {:else if status === 'failed'}
      <p class="failure" role="alert">{m['cookbooks.arrange.failed']()}</p>
      <div>
        <Button onclick={() => void load()}>{m['cookbooks.arrange.retry']()}</Button>
      </div>
    {:else}
      <ol class="lines" bind:this={list}>
        {#each lines as line, index (line.id)}
          <li class="line" data-line={line.id}>
            <span class="place" aria-hidden="true">{index + 1}</span>
            <span class="title">{line.title}</span>
            <span class="moves">
              <span data-move="-1">
                <IconButton
                  size="sm"
                  label={m['cookbooks.arrange.up']({ title: line.title })}
                  disabled={index === 0}
                  onclick={() => void move(index, -1)}
                >
                  <svg
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    stroke-width="1.8"
                    aria-hidden="true"
                  >
                    <path d="m6 15 6-6 6 6" stroke-linecap="round" stroke-linejoin="round" />
                  </svg>
                </IconButton>
              </span>
              <span data-move="1">
                <IconButton
                  size="sm"
                  label={m['cookbooks.arrange.down']({ title: line.title })}
                  disabled={index === lines.length - 1}
                  onclick={() => void move(index, 1)}
                >
                  <svg
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    stroke-width="1.8"
                    aria-hidden="true"
                  >
                    <path d="m6 9 6 6 6-6" stroke-linecap="round" stroke-linejoin="round" />
                  </svg>
                </IconButton>
              </span>
            </span>
          </li>
        {/each}
      </ol>
    {/if}

    {#if failure}
      <p class="failure" role="alert">{failure}</p>
    {/if}

    <p class="ds-clipped" aria-live="polite">{announcement}</p>
  </div>

  {#snippet footer()}
    <div class="footer">
      <Button onclick={onclose}>{m['cookbooks.arrange.cancel']()}</Button>
      <Button variant="primary" disabled={!changed} loading={saving} onclick={() => void save()}>
        {m['cookbooks.arrange.save']()}
      </Button>
    </div>
  {/snippet}
</Sheet>

<style>
  .body {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
  }

  .intro {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .lines {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .line {
    display: flex;
    align-items: center;
    gap: var(--space-3);
    min-height: var(--control-md);
    padding-inline: var(--space-3) var(--space-1);
    border-radius: var(--radius-md);
    background: var(--surface-sunken);
  }

  .place {
    flex: none;
    min-width: 1.5em;
    color: var(--text-subtle);
    font-size: var(--text-sm);
    font-variant-numeric: tabular-nums;
  }

  .title {
    flex: 1;
    min-width: 0;
  }

  .moves {
    display: flex;
    flex: none;
  }

  .failure {
    color: var(--text-danger);
  }

  .footer {
    display: flex;
    flex-wrap: wrap;
    justify-content: flex-end;
    gap: var(--space-2);
  }
</style>
