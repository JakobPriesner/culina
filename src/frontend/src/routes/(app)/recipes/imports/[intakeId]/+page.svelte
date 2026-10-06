<script lang="ts">
  import { onMount } from 'svelte';
  import { page } from '$app/state';
  import { goto } from '$app/navigation';
  import { Button } from '$ds';
  import Olli from '$shell/olli/Olli.svelte';
  import type { Pose } from '$shell/olli/poses';
  import { m } from '$shell/i18n';
  import { explain } from '$shell/explain';
  import DraftWriting from '$features/assistance/DraftWriting.svelte';
  import { intakes, running } from '$features/import/intakes.svelte';
  import { importPush } from '$features/import/push.svelte';
  import { session } from '$features/auth/session.svelte';
  const id = $derived(page.params.intakeId ?? '');
  const job = $derived(intakes.jobs.find((job) => job.id === id));
  const working = $derived(job ? running(job) : false);
  const pose = $derived<Pose>(
    job?.stage === 'ready'
      ? 'idea'
      : job?.stage === 'failed'
        ? 'puzzled'
        : job?.stage === 'writing' || job?.stage === 'saving'
          ? 'writing'
          : job?.stage === 'thinking'
            ? 'thinking'
            : 'watching'
  );
  const labels = $derived<Record<string, string>>({
    queued: m['intake.queued'](),
    reading: m['intake.reading'](),
    thinking: m['intake.thinking'](),
    writing: m['intake.writing'](),
    saving: m['intake.saving'](),
    ready: m['intake.ready'](),
    reviewed: m['intake.saved'](),
    failed: m['intake.failed']()
  });
  const stages = ['reading', 'thinking', 'writing', 'ready'];
  const step = $derived(
    job?.stage === 'queued' ? -1 : job?.stage === 'saving' ? 2 : stages.indexOf(job?.stage ?? '')
  );
  let loaded = $state(false);
  let retrying = $state(false);
  let retryId: string | undefined;
  onMount(() => {
    void intakes.get(id).finally(() => (loaded = true));
  });
  async function finish() {
    const recipeId = job?.recipeId;
    if (!recipeId) return;
    if (await intakes.reviewed(id)) await goto(`/recipes/${recipeId}`);
  }
  async function retry() {
    if (!job || retrying) return;
    retrying = true;
    try {
      // Photos remain server-side on failure, rather than requiring another upload.
      const next = await intakes.retry(id, (retryId ??= crypto.randomUUID()));
      if (next) await goto(`/recipes/imports/${next.id}`);
    } finally {
      retrying = false;
    }
  }
</script>

<svelte:head><title>{m['intake.activity']()} · {m['app.name']()}</title></svelte:head>
<div class="intake-page">
  <a class="back" href="/recipes">← {m['intake.back']()}</a>
  {#if job}
    <section class="status">
      <div class="mascot">
        <Olli {pose} size="lg" {working} />
      </div>
      <div class="status-copy">
        <p class="eyebrow">{m['intake.activity']()}</p>
        <h1>
          {job.stage === 'ready' || job.stage === 'reviewed'
            ? (job.draft?.title ?? m['intake.ready']())
            : m['intake.title']()}
        </h1>
        <p class="stage" role="status">{labels[job.stage]}</p>
        <p class="hint">
          {working
            ? m['intake.continues']()
            : job.stage === 'failed'
              ? m['intake.failedBody']()
              : m['intake.saved']()}
        </p>
        {#if working}
          <ol class="stages" aria-label={m['intake.activity']()}>
            {#each stages as stage, index (stage)}
              <li class:complete={index < step} aria-current={index === step ? 'step' : undefined}>
                <span aria-hidden="true">{index < step ? '✓' : index + 1}</span>{labels[stage]}
              </li>
            {/each}
          </ol>
          <div class="actions">
            {#if importPush.supported && !importPush.enabled}
              <Button onclick={() => void importPush.enable()} loading={importPush.busy}
                >{m['intake.notify']()}</Button
              >
            {/if}
          </div>
          {#if importPush.enabled}<p class="hint">
              {m['intake.notificationsOn']()}
              <button class="text-button" onclick={() => void importPush.disable()}
                >{m['intake.notificationsOff']()}</button
              >
            </p>{/if}
          {#if importPush.failed}<p role="alert" class="hint">
              {m['intake.notificationFailed']()}
            </p>{/if}
        {:else if job.recipeId}
          <div class="actions">
            <a class="primary" href="/recipes/{job.recipeId}/edit">{m['intake.review']()}</a>
            {#if job.stage === 'ready'}<Button onclick={() => void finish()}
                >{m['intake.finishReview']()}</Button
              >{/if}
          </div>
        {:else if job.stage === 'failed'}
          <Button
            onclick={() => void retry()}
            loading={retrying}
            disabled={!session.user?.assistance.read}>{m['intake.retry']()}</Button
          >
          {#if intakes.error}<p role="alert">{explain(intakes.error)}</p>{/if}
        {/if}
        {#if intakes.error && working}<p class="hint" role="status">
            {explain(intakes.error)}
          </p>{/if}
      </div>
    </section>
    <div class="comparison">
      <section class="source">
        <h2>{m['import.review.source']()}</h2>
        {#if job.sourceUrl}<a
            class="source-link"
            href={job.sourceUrl}
            target="_blank"
            rel="noopener noreferrer">{job.sourceUrl}</a
          >{/if}
        {#if job.material}<pre>{job.material}</pre>{/if}
        {#if job.transcript}<h3>{m['import.review.transcript']()}</h3>
          <pre>{job.transcript}</pre>{/if}
        {#if job.photoCount}<div class="photos">
            {#each Array(job.photoCount) as _, index (index)}<img
                src="/api/v1/recipe-intakes/{id}/photos/{index}"
                alt={m['import.review.photo']()}
              />{/each}
          </div>{/if}
      </section>
      <section class="recipe">
        <h2>{m['import.review.title']()}</h2>
        <DraftWriting draft={job.draft ?? null} writing={working} showProgress={false} />
      </section>
    </div>
  {:else if loaded}<p role="alert">{m['intake.missing']()}</p>
    {#if intakes.error}<p>{explain(intakes.error)}</p>{/if}
  {:else}<p role="status">{m['intake.queued']()}</p>{/if}
</div>

<style>
  .intake-page {
    max-width: 64rem;
    margin: 0 auto;
    padding: var(--space-4);
  }
  .back {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }
  .status {
    display: flex;
    gap: var(--space-8);
    align-items: center;
    padding-block: var(--space-6);
  }
  .mascot {
    flex: none;
    display: grid;
    place-items: center;
    padding: var(--space-3);
    border-radius: var(--radius-full);
    background: radial-gradient(ellipse, var(--surface-accent-subtle), transparent 72%);
  }
  .mascot:empty {
    display: none;
  }
  .mascot :global(.olli) {
    width: 13rem;
    height: 13rem;
  }
  .status-copy {
    flex: 1;
    min-width: 0;
  }
  h1 {
    font-family: var(--font-editorial);
    font-size: clamp(1.7rem, 4vw, 2.5rem);
    line-height: 1.15;
  }
  .eyebrow {
    color: var(--text-subtle);
    font-size: var(--text-xs);
    text-transform: uppercase;
    letter-spacing: 0.08em;
    margin-bottom: var(--space-2);
  }
  .stage {
    font-weight: var(--weight-medium);
    margin-top: var(--space-3);
  }
  .hint {
    color: var(--text-muted);
    line-height: 1.6;
    margin-block: var(--space-2);
  }
  .actions {
    display: flex;
    gap: var(--space-3);
    flex-wrap: wrap;
    margin-block: var(--space-3);
    align-items: center;
  }
  .primary {
    border-radius: var(--radius-md);
    background: var(--text);
    color: var(--surface-raised);
    text-decoration: none;
    padding: var(--space-2) var(--space-4);
    font-weight: var(--weight-medium);
  }
  .text-button {
    color: inherit;
    text-decoration: underline;
    cursor: pointer;
    background: none;
    border: 0;
    font: inherit;
  }
  .stages {
    list-style: none;
    padding: 0;
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2) var(--space-4);
    font-size: var(--text-xs);
    margin-block: var(--space-4);
    color: var(--text-muted);
  }
  .stages li {
    display: flex;
    align-items: center;
    gap: var(--space-1);
  }
  .stages span {
    flex: none;
    width: 1.5rem;
    height: 1.5rem;
    border: 1px solid var(--border);
    display: grid;
    place-items: center;
    border-radius: 50%;
    transition:
      background-color var(--duration-base) var(--ease-out),
      border-color var(--duration-base) var(--ease-out),
      color var(--duration-base) var(--ease-out);
  }
  .stages [aria-current] {
    color: var(--text);
    font-weight: var(--weight-medium);
  }
  .complete span {
    background: var(--surface-accent-subtle);
    color: var(--accent);
    border-color: var(--accent);
  }
  .stages [aria-current] span {
    background: var(--accent);
    border-color: var(--accent);
    color: var(--accent-contrast);
  }
  .comparison {
    display: grid;
    grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
    border-top: 1px solid var(--border);
  }
  .comparison section {
    min-width: 0;
    padding: var(--space-6) var(--space-4);
  }
  .source {
    background: var(--surface-sunken);
    border-radius: var(--radius-md);
  }
  h2 {
    font-size: var(--text-lg);
    font-weight: var(--weight-medium);
    margin-bottom: var(--space-4);
  }
  h3 {
    margin-block: var(--space-3);
    font-size: var(--text-sm);
  }
  pre {
    white-space: pre-wrap;
    overflow-wrap: anywhere;
    font: inherit;
    font-size: var(--text-sm);
    line-height: 1.65;
    margin-block: var(--space-3);
  }
  .source-link {
    overflow-wrap: anywhere;
    font-size: var(--text-sm);
  }
  .photos {
    display: grid;
    gap: var(--space-3);
  }
  .photos img {
    width: 100%;
    height: auto;
    border-radius: var(--radius-md);
  }
  @media (width < 42rem) {
    .status {
      flex-direction: column;
      align-items: flex-start;
      gap: var(--space-2);
    }
    .mascot {
      align-self: center;
    }
    .mascot :global(.olli) {
      width: 11rem;
      height: 11rem;
    }
    .stages {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
    }
    .comparison {
      grid-template-columns: 1fr;
    }
  }
</style>
