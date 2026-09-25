<script lang="ts">
  import { resolve } from '$app/paths';
  import { Button, Checkbox, ProgressBar } from '$ds';

  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';

  import type { ImportRun } from './types';

  /**
   * An import, while it runs and once it has stopped.
   *
   * Counted in recipes, because recipes are what somebody asked for — the fact
   * that the server fetches four of them at a time is its business and not
   * theirs. And the progress is real rather than estimated: every number here
   * came from a recipe that is written, so the bar never jumps backwards and
   * never sits at 99%.
   *
   * The end of the flow is a link to the cookbook, which is the whole design in
   * one control. Four hundred recipes arriving into a library is invisible;
   * four hundred recipes on a shelf with a name is something you can open, look
   * through, show somebody, and — if it was a mistake — delete.
   *
   * That link is offered from the first second and not only at the end, because
   * the import is the server's: leaving this screen costs the progress bar and
   * nothing else.
   */
  interface Props {
    run: ImportRun;
    ondone: () => void;
    onlook: () => void;
    /** Brings over the held-back recipes somebody chose, after all. */
    onanyway: (externalIds: string[]) => void;
  }

  let { run, ondone, onlook, onanyway }: Props = $props();

  /**
   * Which of the held-back recipes to bring over after all.
   *
   * None to begin with, and that is the rule this whole list exists to keep: a
   * household may want two Bolognese, but only somebody who has looked at both
   * can say so.
   */
  let chosen = $state<string[]>([]);
</script>

<section class="run" aria-labelledby="run-heading">
  <h2 id="run-heading" class="heading">
    {run.finished ? m['import.run.done']() : m['import.run.working']()}
  </h2>

  <ProgressBar
    value={run.done}
    max={run.total}
    label={m['import.run.label']()}
    valueText={m['import.run.of']({ done: run.done, total: run.total })}
  />

  <p class="line" role="status">{m['import.run.of']({ done: run.done, total: run.total })}</p>

  {#if run.lost && !run.finished}
    <!-- What stopped is usually this page, not the import: the server keeps
         bringing the recipes over. So the reason is quoted rather than
         summarised — "the connection went" and "that import is gone" are two
         different situations and only one of them is worth waiting through. -->
    <div class="lost" role="status">
      <p>{m['import.run.lost']()}</p>
      <p class="why">{explain(run.lost)}</p>
      {#if run.lost.requestId}
        <p class="reference">{m['error.reference']()}: {run.lost.requestId}</p>
      {/if}
      <Button variant="ghost" onclick={onlook}>{m['import.run.lookAgain']()}</Button>
    </div>
  {/if}

  {#if run.finished}
    <dl class="tally">
      <div class="item">
        <dt>{m['import.run.imported']()}</dt>
        <dd>{run.imported}</dd>
      </div>

      {#if run.skipped > 0}
        <div class="item">
          <!-- Not a failure, and never counted as one: re-running an import is
               the ordinary way to catch up on what is new. -->
          <dt>{m['import.run.skipped']()}</dt>
          <dd>{run.skipped}</dd>
        </div>
      {/if}

      {#if run.held.length > 0}
        <div class="item">
          <!-- Not a failure either: each could have been written, and was held
               back only so that somebody could say whether they want two. -->
          <dt>{m['import.run.held']()}</dt>
          <dd>{run.held.length}</dd>
        </div>
      {/if}

      {#if run.failures.length > 0}
        <div class="item">
          <dt>{m['import.run.failed']()}</dt>
          <dd>{run.failures.length}</dd>
        </div>
      {/if}
    </dl>

    {#if run.failures.length > 0}
      <div class="failures">
        <p class="failuresLead">{m['import.run.failedLead']()}</p>

        <!-- By name, not as a number. Twelve that could not be read is a
             statistic; twelve titles is a list somebody can act on. -->
        <ul class="failureList">
          {#each run.failures as title, index (index)}
            <li>{title}</li>
          {/each}
        </ul>
      </div>
    {/if}

    {#if run.held.length > 0}
      <section class="held" aria-labelledby="held-heading">
        <h3 id="held-heading" class="heldLead">
          {m['import.held.lead']({ count: run.held.length })}
        </h3>
        <p class="hint">{m['import.held.hint']()}</p>

        <ul class="heldList">
          {#each run.held as recipe (recipe.externalId)}
            <li class="heldItem">
              <Checkbox
                checked={chosen.includes(recipe.externalId)}
                label={recipe.title}
                describedBy={`held-${recipe.externalId}`}
                onchange={(checked) =>
                  (chosen = checked
                    ? [...chosen, recipe.externalId]
                    : chosen.filter((one) => one !== recipe.externalId))}
              />
              <p class="like" id={`held-${recipe.externalId}`}>
                {[
                  m['import.held.like']({ title: recipe.looksLike.title }),
                  recipe.looksLike.cookCount > 0
                    ? m['recipes.meta.cooked']({ count: recipe.looksLike.cookCount })
                    : null,
                  recipe.looksLike.sharedIngredients > 0
                    ? m['import.held.shared']({ count: recipe.looksLike.sharedIngredients })
                    : null
                ]
                  .filter(Boolean)
                  .join(' · ')}
                <!-- Beside it rather than instead of it: the reader decides by
                     looking at the one they already have. -->
                <a
                  class="compare"
                  href={resolve('/(app)/recipes/[recipeId]', { recipeId: recipe.recipeId })}
                  target="_blank"
                  rel="noopener"
                >
                  {m['import.held.compare']()}
                </a>
              </p>
            </li>
          {/each}
        </ul>

        <div>
          <Button disabled={chosen.length === 0} onclick={() => onanyway(chosen)}>
            {m['import.held.anyway']({ count: chosen.length })}
          </Button>
        </div>
      </section>
    {/if}

    <div class="actions">
      {#if run.cookbookId}
        <Button
          variant="primary"
          href={resolve('/(app)/cookbooks/[cookbookId]', { cookbookId: run.cookbookId })}
        >
          {m['import.run.openCookbook']({ name: run.cookbookName ?? '' })}
        </Button>
      {/if}

      <Button variant="ghost" onclick={ondone}>{m['import.run.bringMore']()}</Button>
    </div>
  {:else if run.cookbookId}
    <div class="actions">
      <!-- The shelf exists before the recipes do, so somebody who does not want
           to watch four hundred of them arrive can go and wait there. -->
      <Button
        variant="ghost"
        href={resolve('/(app)/cookbooks/[cookbookId]', { cookbookId: run.cookbookId })}
      >
        {m['import.run.openCookbook']({ name: run.cookbookName ?? '' })}
      </Button>
    </div>
  {/if}
</section>

<style>
  .run {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
  }

  .heading {
    font-family: var(--font-editorial);
    font-size: var(--text-xl);
    font-weight: var(--weight-regular);
  }

  .line {
    color: var(--text-muted);
    font-size: var(--text-sm);
    font-variant-numeric: tabular-nums;
  }

  .tally {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-6);
    margin: 0;
  }

  .item {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
  }

  dt {
    color: var(--text-muted);
    font-size: var(--text-xs);
    letter-spacing: 0.08em;
    text-transform: uppercase;
  }

  dd {
    margin: 0;
    font-size: var(--text-xl);
    font-variant-numeric: tabular-nums;
  }

  .lost {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    gap: var(--space-2);
    padding: var(--space-3) var(--space-4);
    border-radius: var(--radius-md);
    background: var(--surface-sunken);
    font-size: var(--text-sm);
  }

  .why {
    color: var(--text-muted);
  }

  .reference {
    color: var(--text-muted);
    font-size: var(--text-xs);
    font-variant-numeric: tabular-nums;
  }

  .held {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    padding: var(--space-4);
    border-radius: var(--radius-md);
    background: var(--surface-sunken);
  }

  .heldLead {
    font-size: var(--text-base);
    font-weight: var(--weight-medium);
  }

  .hint,
  .like {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .heldList {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .heldItem {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
  }

  /* Under the checkbox's own label, so the line reads as about that recipe. */
  .like {
    padding-left: calc(var(--space-6) + var(--space-3));
  }

  .compare {
    margin-left: var(--space-2);
  }

  .failures {
    padding: var(--space-4);
    border-radius: var(--radius-md);
    background: var(--surface-sunken);
  }

  .failuresLead {
    font-size: var(--text-sm);
    color: var(--text-muted);
  }

  .failureList {
    margin: var(--space-2) 0 0;
    padding-inline-start: var(--space-4);
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-3);
  }
</style>
