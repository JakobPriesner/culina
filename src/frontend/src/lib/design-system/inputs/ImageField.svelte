<script lang="ts">
  import type { Snippet } from 'svelte';

  import Button from '../actions/Button.svelte';
  import Image from '../display/Image.svelte';
  import FilePicker from './FilePicker.svelte';

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
    /**
     * One more thing that can be done to the picture, from the caller.
     *
     * Rendered with the other two, on the picture and under it, so a field
     * with three ways to fill it looks like one control rather than a control
     * with a button loose beside it.
     */
    extraAction?: Snippet;
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
    src,
    srcset,
    sizes,
    alt = '',
    ratio = 4 / 3,
    accept = 'image/jpeg,image/png,image/webp',
    busy = false,
    generating = false,
    generatingLabel,
    extraAction,
    failure = null,
    onpick,
    onremove
  }: Props = $props();

  let picker = $state<ReturnType<typeof FilePicker>>();
</script>

<div class="field">
  {#if showLabel}
    <p class="label">{label}</p>
  {/if}

  <div class="frame" class:filled={src}>
    {#if src}
      <Image {src} {srcset} {sizes} {alt} {ratio} />

      <!-- On the picture, and only once there is one. Revealed by pointing at
           it or tabbing into it; always there where nothing can hover. -->
      <div class="overlay">
        <!-- Disabled while a picture is being drawn: the drawing covers this,
             so a pointer cannot reach it, and a control a keyboard can still
             get to but a mouse cannot is a control that behaves differently
             for different people. -->
        <Button
          variant="secondary"
          size="sm"
          loading={busy}
          disabled={generating}
          onclick={() => picker?.open()}
        >
          {replaceLabel}
        </Button>

        {#if extraAction}{@render extraAction()}{/if}

        <Button variant="ghost" size="sm" disabled={busy || generating} onclick={onremove}>
          {removeLabel}
        </Button>
      </div>
    {:else}
      <!-- The same box the picture will occupy, drawn rather than left blank:
           an empty field that shows its own dimensions is a form saying what it
           wants, and a form that does not jump when it gets it. -->
      <div class="template" style:aspect-ratio={ratio}>
        <svg
          class="glyph"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="1.5"
          stroke-linecap="round"
          stroke-linejoin="round"
          aria-hidden="true"
        >
          <rect x="3" y="5" width="18" height="14" rx="2" />
          <circle cx="8.5" cy="10" r="1.5" />
          <!-- A horizon rising out of the frame: the shape of a photograph
               rather than of a broken one. -->
          <path d="m4 17 5-5 4 4 3-2 4 3" />
        </svg>

        <p class="hint">{hint}</p>
      </div>
    {/if}

    {#if generating}
      <!-- Over whatever is underneath, because a picture being drawn is about
           to replace it. The frame does the waiting rather than a spinner in a
           button: this is a minute of somebody else's machine working, and a
           spinner that size says "a moment". -->
      <div class="drawing" aria-hidden="true">
        <span class="sweep"></span>
        <span class="grain"></span>
      </div>

      {#if generatingLabel}
        <p class="drawing-label" role="status">{generatingLabel}</p>
      {/if}
    {/if}
  </div>

  {#if !src}
    <div class="actions">
      <Button loading={busy} onclick={() => picker?.open()}>{chooseLabel}</Button>

      {#if extraAction}{@render extraAction()}{/if}
    </div>
  {/if}

  {#if failure}
    <p class="failure" role="alert">{failure}</p>
  {/if}

  <FilePicker bind:this={picker} label={chooseLabel} {accept} {onpick} />
</div>

<style>
  .field {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    gap: var(--space-3);
  }

  .label {
    font-size: var(--text-sm);
    font-weight: var(--weight-medium);
  }

  .frame {
    position: relative;
    width: min(30rem, 100%);
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
    gap: var(--space-2);
    padding: var(--space-3);
    background: linear-gradient(to top, var(--scrim), transparent);
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

  /* A touch screen has nothing to hover with, and a control that only appears
     on hover is a control that does not exist there. */
  @media (hover: none) {
    .overlay {
      opacity: 1;
      transform: none;
    }
  }

  .template {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: var(--space-2);
    padding: var(--space-4);
    /* Dashed, which is the one border convention everyone already reads as
       "this is where something goes". */
    border: 1px dashed var(--border-strong);
    border-radius: var(--radius-lg);
    background: var(--surface-sunken);
    color: var(--text-muted);
    text-align: center;
  }

  .glyph {
    width: var(--space-8);
    height: var(--space-8);
  }

  .hint {
    max-width: var(--measure);
    font-size: var(--text-sm);
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-3);
  }

  /*
   * A picture being drawn.
   *
   * Two things at once, because that is what makes it read as work rather than
   * as a stalled page: a band of light travelling across the frame, and a
   * grain that breathes underneath it. Neither reports progress, because
   * nothing here knows any — a provider says nothing until it has finished —
   * and a bar that invents its own is a bar that lies.
   */
  .drawing {
    position: absolute;
    inset: 0;
    overflow: hidden;
    border-radius: var(--radius-lg);
    background: var(--surface-sunken);
  }

  .sweep,
  .grain {
    position: absolute;
    inset: 0;
  }

  .sweep {
    background: linear-gradient(
      105deg,
      transparent 30%,
      var(--surface-accent-subtle) 45%,
      var(--surface-highlight) 50%,
      var(--surface-accent-subtle) 55%,
      transparent 70%
    );
    background-size: 300% 100%;
    animation: sweep 2.4s var(--ease-spatial) infinite;
  }

  .grain {
    background:
      radial-gradient(40% 55% at 30% 35%, var(--surface-accent-subtle), transparent 70%),
      radial-gradient(45% 45% at 70% 65%, var(--surface-highlight), transparent 70%);
    opacity: 0.55;
    animation: breathe 3.6s ease-in-out infinite;
  }

  .drawing-label {
    position: absolute;
    right: 0;
    bottom: 0;
    left: 0;
    padding: var(--space-3);
    color: var(--text-muted);
    font-size: var(--text-sm);
    text-align: center;
  }

  @keyframes sweep {
    from {
      background-position: 150% 0;
    }

    to {
      background-position: -150% 0;
    }
  }

  @keyframes breathe {
    0%,
    100% {
      opacity: 0.45;
      transform: scale(1);
    }

    50% {
      opacity: 0.7;
      transform: scale(1.06);
    }
  }

  /* The wait is still a wait, so the frame still says so — it simply stops
     moving. */
  @media (prefers-reduced-motion: reduce) {
    .overlay {
      transition: none;
    }

    .sweep,
    .grain {
      animation: none;
    }
  }

  .failure {
    color: var(--text-danger);
    font-size: var(--text-sm);
  }
</style>
