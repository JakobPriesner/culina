<script lang="ts">
  import { Button, Select, Sheet } from '$ds';
  import type { RecipeReading } from '$features/recipes/types';
  import { m } from '$shell/i18n';
  import CookingPipToggle from './CookingPipToggle.svelte';
  import CookingRemoteToggle from './CookingRemoteToggle.svelte';
  import { kitchenWakeLock as wakeLock } from './kitchen.svelte';
  import { kitchenLighting } from './lighting.svelte';

  /** The kitchen's own settings: lighting, staying awake, and where cooking shows up; and the way to stop. */
  interface Props {
    open: boolean;
    /** Nothing but the title until the recipe being cooked has arrived. */
    recipe: RecipeReading | null;
    onclose: () => void;
    /**
     * Ends the session without a record ("made it" is the controls' last step). Here rather than at the recipe's end,
     * where the step controls covered it until the very bottom of the page.
     */
    onstop?: () => void;
  }

  let { open, recipe, onclose, onstop }: Props = $props();
</script>

<Sheet {open} title={m['kitchen.controls']()} closeLabel={m['picker.close']()} {onclose}>
  {#if recipe}
    <p class="auto-scroll-hint">{m['cooking.autoScroll.hint']()}</p>
    <div class="kitchen-display">
      <label for="kitchen-lighting">{m['kitchen.lighting']()}</label>
      <Select
        id="kitchen-lighting"
        value={kitchenLighting.mode}
        inline
        options={[
          { value: 'normal', label: m['kitchen.normal']() },
          { value: 'glare', label: m['kitchen.glare']() },
          { value: 'oled', label: m['kitchen.oled']() }
        ]}
        onchange={(value) => kitchenLighting.choose(value)}
      />
      <span class="wake-status" class:held={wakeLock.held}>
        <span aria-hidden="true">{wakeLock.held ? '◉' : '○'}</span>
        {wakeLock.held ? m['kitchen.awake']() : m['kitchen.canSleep']()}
      </span>
      <CookingPipToggle {recipe} />
      <CookingRemoteToggle {recipe} />
    </div>
    {#if onstop}
      <div class="stop">
        <Button variant="ghost" onclick={onstop}>{m['cooking.abandon']()}</Button>
      </div>
    {/if}
  {/if}
</Sheet>

<style>
  /* Apart from the settings above it: it ends the session rather than adjusting it. */
  .stop {
    padding-block-start: var(--space-3);
    border-top: 1px solid var(--border);
  }

  .auto-scroll-hint {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .kitchen-display {
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: var(--space-4);
    padding-block: var(--space-3);
    color: var(--text);
    font-size: var(--text-sm);
  }
  .wake-status {
    flex-basis: 100%;
    color: var(--text-muted);
  }
  .wake-status.held {
    color: var(--text-success);
  }
</style>
