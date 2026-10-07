<script lang="ts">
  import { goto } from '$app/navigation';
  import { resolve } from '$app/paths';
  import { Button } from '$ds';
  import { session } from '$features/auth/session.svelte';
  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';
  import Olli from '$shell/olli/Olli.svelte';
  import { stageLabel } from './intakeLabels';
  import { poseFor, stepOf } from './intakeStages';
  import IntakeStages from './IntakeStages.svelte';
  import { intakes, running, type IntakeJob } from './intakes.svelte';
  import { importPush } from './push.svelte';

  interface Props {
    job: IntakeJob;
  }

  let { job }: Props = $props();

  const working = $derived(running(job));

  let retrying = $state(false);
  let retryId: string | undefined;

  async function finish() {
    const recipeId = job.recipeId;

    if (!recipeId) return;
    if (await intakes.reviewed(job.id))
      await goto(resolve('/(app)/recipes/[recipeId]', { recipeId }));
  }

  async function retry() {
    if (retrying) return;

    retrying = true;

    try {
      // Photos remain server-side on failure, rather than requiring another upload.
      const next = await intakes.retry(job.id, (retryId ??= crypto.randomUUID()));

      if (next) await goto(resolve('/(app)/recipes/imports/[intakeId]', { intakeId: next.id }));
    } finally {
      retrying = false;
    }
  }
</script>

<section class="status">
  <div class="mascot">
    <Olli pose={poseFor(job.stage)} size="lg" {working} />
  </div>
  <div class="status-copy">
    <p class="eyebrow">{m['intake.activity']()}</p>
    <h1>
      {job.stage === 'ready' || job.stage === 'reviewed'
        ? (job.draft?.title ?? m['intake.ready']())
        : m['intake.title']()}
    </h1>
    <p class="stage" role="status">{stageLabel(job.stage)}</p>
    <p class="hint">
      {working
        ? m['intake.continues']()
        : job.stage === 'failed'
          ? m['intake.failedBody']()
          : m['intake.saved']()}
    </p>
    {#if working}
      <IntakeStages step={stepOf(job.stage)} />
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
        <a
          class="primary"
          href={resolve('/(app)/recipes/[recipeId]/edit', { recipeId: job.recipeId })}
          >{m['intake.review']()}</a
        >
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

<style>
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
  }
</style>
