<script lang="ts">
  import Olli from '$shell/olli/Olli.svelte';
  import { poses, type Pose } from '$shell/olli/poses';

  /* Olli in every pose, for inspecting motion: the big one moves between poses as chosen (what the springs are for); the row arrives in each pose once. */
  const names = Object.keys(poses) as Pose[];

  let pose = $state<Pose>('hello');
</script>

<div class="stage">
  <Olli {pose} size="lg" />
  <div class="choices">
    {#each names as name (name)}
      <button type="button" aria-pressed={pose === name} onclick={() => (pose = name)}>
        {name}
      </button>
    {/each}
  </div>
</div>

<div class="all">
  {#each names as name (name)}
    <figure>
      <Olli pose={name} />
      <figcaption>{name}</figcaption>
    </figure>
  {/each}
</div>

<div class="small">
  <Olli pose="celebrating" size="sm" still />
  <Olli pose="hello" size="sm" still />
  <span>Small and still, for toasts.</span>
</div>

<style>
  .stage {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-6);
  }

  .choices {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
    flex: 1;
    min-width: 12rem;
  }

  button {
    min-height: var(--control-sm);
    padding-inline: var(--space-3);
    border: 1px solid var(--border-strong);
    border-radius: var(--radius-full);
    background: var(--surface-raised);
    color: var(--text);
    font: inherit;
    cursor: pointer;
  }

  button[aria-pressed='true'] {
    border-color: var(--accent);
    background: var(--surface-accent-subtle);
  }

  .all {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(8rem, 1fr));
    gap: var(--space-4);
  }

  figure {
    display: grid;
    justify-items: center;
    gap: var(--space-1);
    margin: 0;
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .small {
    display: flex;
    align-items: center;
    gap: var(--space-3);
    color: var(--text-muted);
    font-size: var(--text-sm);
  }
</style>
