<script lang="ts">
  import { onMount, untrack } from 'svelte';
  import { resolve } from '$app/paths';
  import { session } from '$features/auth/session.svelte';
  import { intakes, running } from './intakes.svelte';
  import { importPush } from './push.svelte';
  import { m } from '$shell/i18n';
  const active = $derived(intakes.jobs.filter(running));
  const ready = $derived(intakes.jobs.filter((job) => job.stage === 'ready'));
  const failed = $derived(intakes.jobs.filter((job) => job.stage === 'failed'));
  $effect(() => {
    const owner = session.user?.userId ?? null;
    untrack(() => {
      intakes.own(owner);
      if (owner) void intakes.refresh();
    });
  });
  onMount(() => void importPush.restore());
  const busy = $derived(active.length > 0);
  // Keeps up after the owner effect's first request: fast while importing, slow otherwise, paused in a hidden tab.
  $effect(() => {
    const delay = busy ? 2000 : 15000;
    let timer: ReturnType<typeof setTimeout> | undefined;
    let disposed = false;
    let polling = false;
    const schedule = () => {
      clearTimeout(timer);
      if (!disposed && !document.hidden) timer = setTimeout(() => void poll(), delay);
    };
    const poll = async () => {
      if (disposed || polling || document.hidden) return;
      polling = true;
      try {
        await intakes.refresh();
      } finally {
        polling = false;
        schedule();
      }
    };
    const resume = () => void poll();
    schedule();
    document.addEventListener('visibilitychange', resume);
    window.addEventListener('online', resume);
    return () => {
      disposed = true;
      clearTimeout(timer);
      document.removeEventListener('visibilitychange', resume);
      window.removeEventListener('online', resume);
    };
  });
</script>

{#if active.length || ready.length || failed.length}
  <aside class="intake-status" aria-label={m['intake.activity']()}>
    {#each [...active, ...ready, ...failed].slice(0, 3) as job (job.id)}
      <a href={resolve('/(app)/recipes/imports/[intakeId]', { intakeId: job.id })}>
        <span
          class:active={running(job)}
          class:ready={job.stage === 'ready'}
          class="dot"
          aria-hidden="true"
        ></span>
        <span
          >{job.stage === 'ready'
            ? m['intake.ready']()
            : job.stage === 'failed'
              ? m['intake.failed']()
              : m['intake.running']()}</span
        >
        {#if job.draft?.title}<span class="title">{job.draft.title}</span>{/if}
      </a>
    {/each}
  </aside>
{/if}

<style>
  .intake-status {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
    padding: var(--space-2) var(--space-4);
    justify-content: center;
    border-top: 1px solid var(--border);
    background: var(--surface-raised);
  }
  a {
    display: flex;
    align-items: center;
    gap: var(--space-2);
    font-size: var(--text-sm);
    color: var(--text);
    border-radius: var(--radius-md);
    padding: var(--space-1) var(--space-2);
    text-decoration: none;
  }
  a:hover {
    background: var(--surface-sunken);
  }
  .dot {
    width: 0.45rem;
    height: 0.45rem;
    border-radius: 50%;
    background: var(--text-danger);
    flex: none;
  }
  .dot.active {
    background: var(--mascot-cheek);
  }
  .dot.ready {
    background: var(--text);
  }
  .title {
    max-width: 12rem;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    color: var(--text-muted);
  }
  @media (width < 30rem) {
    .title {
      display: none;
    }
  }
  @media print {
    .intake-status {
      display: none;
    }
  }
</style>
