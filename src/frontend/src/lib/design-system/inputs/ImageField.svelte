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
   * One picture, chosen and looked at in the same place.
   *
   * Two states and one box. With a picture it is the picture, at the size it
   * will really be seen; without one it is a template of exactly those
   * dimensions — an outline, not a gap — so choosing a photo does not shove the
   * rest of the form down the page, and a form that has not been filled in
   * still shows its shape.
   *
   * Knows nothing about what is being photographed. Where the bytes go is the
   * caller's business: this reports the file that was chosen and is told
   * whether that worked.
   *
   * Once there is a picture, what can be done to it belongs on it. Three
   * buttons stacked underneath made the field look like a form about a
   * photograph rather than the photograph itself, and two of the three are
   * things nobody does twice. They are revealed by pointing at the picture,
   * by tabbing into it, and unconditionally where there is no pointer to
   * hover with.
   *
   * A file dragged onto the frame is the same as one chosen, in either state:
   * the frame is where the picture goes, so it is where you drop it.
   */
  interface Props {
    label: string;
    /**
     * Whether to print the caption above the frame.
     *
     * Off where a heading already says the same word: a section called "Photo"
     * with a caption called "Photo" under it is the form talking to itself. The
     * label is still what the file picker announces, so nothing is lost by not
     * drawing it.
     */
    showLabel?: boolean;
    /** What the empty template says. One line, under the outline. */
    hint: string;
    chooseLabel: string;
    replaceLabel: string;
    removeLabel: string;
    /** What the frame says while a file is held over it. */
    dropLabel: string;
    /** The picture, when there is one. Absent means the template. */
    src?: string | undefined;
    srcset?: string | undefined;
    sizes?: string | undefined;
    /** Decorative by default: the caption is the form around it. */
    alt?: string;
    /** The shape both states hold, so neither moves when the other arrives. */
    ratio?: number;
    accept?: string;
    /** An upload or a removal in flight. */
    busy?: boolean;
    /**
     * A picture being made rather than sent.
     *
     * Its own state and not `busy`, because it is its own wait: a minute of
     * somebody else's machine drawing, where an upload is a few seconds of
     * this one's network. The frame says so itself instead of a spinner beside
     * a button saying it.
     */
    generating?: boolean;
    /** What the frame says while it draws. */
    generatingLabel?: string;
    /** Optional decoration shown only during generation, beside the status. */
    generatingArt?: Snippet;
    /** Whether the decorative generation background may move. */
    animateGeneration?: boolean;
    /**
     * One more thing that can be done to the picture, from the caller.
     *
     * Rendered with the other two, so a field with three ways to fill it looks
     * like one control rather than a control with a button loose beside it.
     * Handed the variant the other two are using, because where the strip is
     * decides how it has to look: glass on a photograph, an ordinary button on
     * the page below one.
     */
    extraAction?: Snippet<[ButtonVariant]>;
    /** What went wrong, already in words the reader can act on. */
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
   * Whether the actions go under the picture rather than on it.
   *
   * A strip revealed by hovering is a strip that does not exist on a phone or
   * a tablet: there is nothing to hover with, and putting it there permanently
   * covers the bottom of every photograph. Under it instead, filling the
   * width, where a thumb can reach it.
   *
   * The same breakpoint the rest of the app calls desktop, and true while the
   * server renders — the laid-out version is the one that is right when
   * nothing has measured anything yet.
   */
  const compact = new MediaQuery('(max-width: 63.999rem)', true);

  const actionVariant = $derived<ButtonVariant>(compact.current ? 'secondary' : 'media');

  /** Nothing can land while the frame is already busy being filled. */
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

  <!-- A group named by the field, which is what a screen reader passing through
       the picture and its buttons hears. Dropping is a shortcut for the buttons
       inside it, not a replacement: a keyboard still has every one of them. -->
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

      <!-- On the picture where there is a pointer to reveal it with. -->
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
      <Button loading={busy} onclick={() => picker?.open()}>{chooseLabel}</Button>

      <!-- An empty field is on the page rather than on a photograph, so what
           the caller puts here is an ordinary button whichever screen it is. -->
      {#if extraAction}{@render extraAction('secondary')}{/if}
    </div>
  {:else if compact.current}
    <!-- Under the picture, filling the line: a phone has no hover, and a strip
         left permanently on the photograph covers the bottom of every one. -->
    <div class="actions filled-actions">{@render strip()}</div>
  {/if}

  {#if failure}
    <p class="failure" role="alert">{failure}</p>
  {/if}

  <FilePicker bind:this={picker} label={chooseLabel} {accept} {onpick} />
</div>

<!--
  The three things you can do to a picture, in the one place they are written.

  All three the same weight, whichever variant they are wearing: this is a
  strip of things you can do, not a hierarchy, and the one that deletes is the
  last that should be hard to read. Disabled while a picture is being drawn —
  on the photograph the drawing covers them, and a control a keyboard can still
  reach but a pointer cannot is a control that behaves differently for
  different people.
-->
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

  /* The picture's own width, so the strip under it is exactly as wide as the
     thing it acts on rather than as wide as the form. */
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
   * The strip of things you can do to the picture.
   *
   * Sits on it rather than under it: what is being edited is the photograph,
   * and three buttons in a row beneath it made the field read as a form about
   * a photograph. Hidden by opacity rather than by `display`, so the buttons
   * stay in the tab order and `:focus-within` brings them into view the moment
   * somebody tabs to one.
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

  /*
   * The strip under a picture, on a screen that cannot hover.
   *
   * Full width and shared equally, because it is the whole line's worth of
   * what you came here to do — and because a thumb aiming at a button wants
   * the button to be the size of the row rather than the size of its word.
   * They wrap on a narrow phone and each row still fills.
   */
  .filled-actions {
    align-self: stretch;
    gap: var(--space-2);
  }

  /*
   * One line, shared equally.
   *
   * A basis of zero rather than of a word's width, so the three stay the same
   * size as each other: let them wrap onto separate rows and the one that gets
   * a row to itself becomes the biggest target on the screen — which, in this
   * strip, would be the one that deletes the photograph. Labels wrap inside
   * the buttons instead, which the button is already built for.
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
