<script lang="ts">
  import type { Snippet } from 'svelte';
  import { MediaQuery } from 'svelte/reactivity';

  import Button, { type ButtonVariant } from '../actions/Button.svelte';
  import Image from '../display/Image.svelte';
  import FilePicker from './FilePicker.svelte';
  import ImageDropOverlay from './ImageDropOverlay.svelte';
  import ImageGenerating from './ImageGenerating.svelte';
  import ImageTemplate from './ImageTemplate.svelte';
  import { createImageDrop } from './imageDrop.svelte';

  /**
   * One picture, chosen and shown in the same place; an empty field is an outline of the same size
   * so the form doesn't jump.
   * Knows nothing about uploads: it reports the chosen (or dropped) file and is told whether that
   * worked.
   */
  interface Props {
    label: string;
    showLabel?: boolean;
    hint: string;
    chooseLabel: string;
    replaceLabel: string;
    removeLabel: string;
    dropLabel: string;
    src?: string | undefined;
    srcset?: string | undefined;
    sizes?: string | undefined;
    alt?: string;
    ratio?: number;
    accept?: string;
    busy?: boolean;
    /** A picture being generated, not uploaded: a separate, longer wait than `busy`. */
    generating?: boolean;
    generatingLabel?: string;
    generatingArt?: Snippet;
    animateGeneration?: boolean;
    /** One more action from the caller, rendered with the built-in ones in the same variant. */
    extraAction?: Snippet<[ButtonVariant]>;
    failure?: string | null;
    onpick: (file: File) => void;
    onremove: () => void;
  }

  let {
    label,
    showLabel = true,
    hint,
    chooseLabel,
    replaceLabel,
    removeLabel,
    dropLabel,
    src,
    srcset,
    sizes,
    alt = '',
    ratio = 4 / 3,
    accept = 'image/jpeg,image/png,image/webp',
    busy = false,
    generating = false,
    generatingLabel,
    generatingArt,
    animateGeneration = true,
    extraAction,
    failure = null,
    onpick,
    onremove
  }: Props = $props();

  let picker = $state<ReturnType<typeof FilePicker>>();

  /**
   * Actions go under the picture on screens that cannot hover; true during SSR so the laid-out
   * version is the default.
   */
  const compact = new MediaQuery('(max-width: 63.999rem)', true);

  const actionVariant = $derived<ButtonVariant>(compact.current ? 'secondary' : 'media');

  const drop = createImageDrop({
    accept: () => accept,
    canDrop: () => !busy && !generating,
    onpick: (file) => onpick(file)
  });
</script>

<div class="field">
  {#if showLabel}
    <p class="label">{label}</p>
  {/if}

  <!-- Group named by the field for screen readers; dropping is a shortcut, the buttons stay
       keyboard-reachable. -->
  <div
    class="frame"
    class:filled={src}
    role="group"
    aria-label={label}
    ondragenter={drop.dragenter}
    ondragover={drop.dragover}
    ondragleave={drop.dragleave}
    ondrop={drop.drop}
  >
    {#if src}
      <Image {src} {srcset} {sizes} {alt} {ratio} />

      {#if !compact.current}
        <div class="overlay">{@render strip()}</div>
      {/if}
    {:else}
      <ImageTemplate {hint} {ratio} />
    {/if}

    {#if generating}
      <ImageGenerating label={generatingLabel} art={generatingArt} animate={animateGeneration} />
    {/if}

    {#if drop.dropping}
      <ImageDropOverlay label={dropLabel} />
    {/if}
  </div>

  {#if !src}
    <div class="actions">
      <Button loading={busy} disabled={generating} onclick={() => picker?.open()}>
        {chooseLabel}
      </Button>

      {#if extraAction}{@render extraAction('secondary')}{/if}
    </div>
  {:else if compact.current}
    <div class="actions filled-actions">{@render strip()}</div>
  {/if}

  {#if failure}
    <p class="failure" role="alert">{failure}</p>
  {/if}

  <FilePicker bind:this={picker} label={chooseLabel} {accept} {onpick} />
</div>

<!-- The picture's actions, all the same weight; disabled while drawing so pointer and keyboard
     behave alike. -->
{#snippet strip()}
  <Button
    variant={actionVariant}
    size="sm"
    loading={busy}
    disabled={generating}
    onclick={() => picker?.open()}
  >
    {replaceLabel}
  </Button>

  {#if extraAction}{@render extraAction(actionVariant)}{/if}

  <Button variant={actionVariant} size="sm" disabled={busy || generating} onclick={onremove}>
    {removeLabel}
  </Button>
{/snippet}

<style>
  .field {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    gap: var(--space-3);
  }

  /* The picture's own width, so the strip matches it rather than the form. */
  .filled-actions {
    width: min(30rem, 100%);
  }

  .label {
    font-size: var(--text-sm);
    font-weight: var(--weight-medium);
  }

  .frame {
    position: relative;
    width: min(30rem, 100%);
    container-type: inline-size;
  }

  .frame.filled {
    overflow: hidden;
    border-radius: var(--radius-lg);
  }

  /*
   * Hidden by opacity, not `display`, so the buttons stay in tab order and `:focus-within` reveals
   * them.
   */
  .overlay {
    position: absolute;
    right: 0;
    bottom: 0;
    left: 0;
    display: flex;
    flex-wrap: wrap;
    justify-content: flex-end;
    gap: var(--space-2);
    padding: var(--space-3);
    background: linear-gradient(to top, var(--scrim), transparent 90%);
    opacity: 0;
    transition:
      opacity var(--duration-base) var(--ease-out),
      transform var(--duration-base) var(--ease-out);
    transform: translateY(var(--space-2));
  }

  .frame:hover .overlay,
  .frame:focus-within .overlay {
    opacity: 1;
    transform: translateY(0);
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-3);
  }

  .filled-actions {
    align-self: stretch;
    gap: var(--space-2);
  }

  /*
   * Zero flex-basis keeps the buttons equal when wrapping, so the delete button never becomes the
   * biggest target.
   */
  .filled-actions :global(.button) {
    flex: 1 1 0;
    min-width: 0;
  }

  @media (prefers-reduced-motion: reduce) {
    .overlay {
      transition: none;
    }
  }

  .failure {
    color: var(--text-danger);
    font-size: var(--text-sm);
  }
</style>
