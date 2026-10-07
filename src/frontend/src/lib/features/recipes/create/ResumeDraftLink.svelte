<script lang="ts">
  import { resolve } from '$app/paths';
  import type { LastDraft } from '$features/recipes/editor/lastDraft';
  import { m } from '$shell/i18n';

  interface Props {
    draft: LastDraft;
  }

  let { draft }: Props = $props();
</script>

<!-- Above the field rather than beside it: somebody who left a recipe
     half-written is here to finish it far more often than to start a
     second one, and the row says which recipe rather than making them
     remember. -->
<a class="resume" href={resolve('/(app)/recipes/[recipeId]/edit', { recipeId: draft.recipeId })}>
  <span class="resume-text">
    <span class="resume-label">{m['editor.continueDraftLabel']()}</span>
    <span class="resume-title">{draft.title}</span>
  </span>

  <span class="resume-arrow" aria-hidden="true">→</span>
</a>

<style>
  .resume {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-4);
    min-width: 0;
    padding: var(--space-3) var(--space-4);
    border: 1px solid var(--border);
    border-radius: var(--radius-lg);
    background: var(--surface-sunken);
    color: inherit;
    text-decoration: none;
    transition:
      border-color var(--duration-fast) var(--ease-out),
      background-color var(--duration-fast) var(--ease-out);
  }

  .resume:hover {
    border-color: var(--border-strong);
    background: var(--surface-hover);
  }

  .resume-text {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
    min-width: 0;
  }

  .resume-label {
    color: var(--text-muted);
    font-size: var(--text-xs);
    letter-spacing: 0.08em;
    text-transform: uppercase;
  }

  .resume-title {
    font-family: var(--font-editorial);
    font-size: var(--text-lg);
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .resume-arrow {
    flex: none;
    color: var(--text-subtle);
    transition: transform var(--duration-fast) var(--ease-out);
  }

  .resume:hover .resume-arrow {
    color: var(--accent);
    transform: translateX(var(--space-1));
  }
</style>
