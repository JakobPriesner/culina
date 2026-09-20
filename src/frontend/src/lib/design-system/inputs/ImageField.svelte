<script lang="ts">
  import type { Snippet } from 'svelte';
  import { MediaQuery } from 'svelte/reactivity';

  import Button, { type ButtonVariant } from '../actions/Button.svelte';
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

  /** The noise filter's own id, so two fields on a page do not share one. */
  const grainId = $props.id();

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
</script>

<div class="field">
  {#if showLabel}
    <p class="label">{label}</p>
  {/if}

  <div class="frame" class:filled={src}>
    {#if src}
      <Image {src} {srcset} {sizes} {alt} {ratio} />

      <!-- On the picture where there is a pointer to reveal it with. -->
      {#if !compact.current}
        <div class="overlay">{@render strip()}</div>
      {/if}
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
           spinner that size says "a moment".

           Four layers, none of which reports progress: three lights drifting
           at their own speeds, a band of specular crossing them, and a film of
           grain over the lot. What makes it read as something being made
           rather than as a page that has stopped is that no two frames repeat
           — the drifts are prime-ish against each other, so the pattern does
           not visibly loop. -->
      <div class="drawing" aria-hidden="true">
        <span class="light one"></span>
        <span class="light two"></span>
        <span class="light three"></span>
        <span class="sheen"></span>

        <!-- Real noise rather than a gradient pretending to be some: it is
             what keeps the blur from looking like a cheap radial fill, and it
             is the texture every photograph has before it resolves. -->
        <svg class="grain" preserveAspectRatio="none">
          <filter id={grainId}>
            <feTurbulence type="fractalNoise" baseFrequency="0.85" numOctaves="3" />
            <feColorMatrix type="saturate" values="0" />
          </filter>
          <rect width="100%" height="100%" filter="url(#{grainId})" />
        </svg>
      </div>

      <div class="caption">
        <!-- Four points rather than a ring: a spinner is what a page shows
             while it fetches something that already exists. -->
        <svg class="spark" viewBox="0 0 24 24" aria-hidden="true">
          <path
            d="M12 1.5c.9 5 3.6 7.7 8.6 8.6-5 .9-7.7 3.6-8.6 8.6-.9-5-3.6-7.7-8.6-8.6 5-.9 7.7-3.6 8.6-8.6Z"
          />
        </svg>

        {#if generatingLabel}
          <p class="drawing-label" role="status">{generatingLabel}</p>
        {/if}
      </div>
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

  /*
   * A picture being drawn.
   *
   * Everything here is composited — transform, opacity and filter only — so
   * four moving layers cost the compositor and not the main thread. The frame
   * is at most thirty centimetres of screen; this is not a background worth a
   * repaint per frame.
   */
  .drawing {
    position: absolute;
    inset: 0;
    overflow: hidden;
    border-radius: var(--radius-lg);
    background: var(--generating-ground);
  }

  /*
   * The three lights.
   *
   * Each is one soft disc, blurred far past its own edge and drifting on its
   * own clock. Durations that do not divide into each other, so the three
   * never line up twice in the same way — a loop you can see is a loop that
   * tells you nothing is really happening.
   */
  .light {
    position: absolute;
    width: 70%;
    height: 85%;
    border-radius: var(--radius-full);
    filter: blur(28px);
    opacity: 0.85;
    will-change: transform;
  }

  .one {
    top: -15%;
    left: -10%;
    background: var(--generating-1);
    animation: drift-one 13s ease-in-out infinite;
  }

  .two {
    right: -15%;
    bottom: -20%;
    background: var(--generating-2);
    animation: drift-two 17s ease-in-out infinite;
  }

  .three {
    top: 20%;
    left: 30%;
    width: 55%;
    height: 60%;
    background: var(--generating-3);
    opacity: 0.6;
    animation: drift-three 11s ease-in-out infinite;
  }

  /*
   * The pass of specular.
   *
   * Steeper than the frame's diagonal and narrow, so it reads as light moving
   * across a surface rather than as a bar crossing a box. Soft-light keeps it
   * light rather than paint: it brightens what it crosses instead of covering
   * it.
   */
  .sheen {
    position: absolute;
    inset: -20%;
    background: linear-gradient(
      115deg,
      transparent 42%,
      var(--generating-sheen) 50%,
      transparent 58%
    );
    background-size: 250% 100%;
    opacity: 0.35;
    mix-blend-mode: soft-light;
    animation: sheen 3.8s cubic-bezier(0.4, 0, 0.2, 1) infinite;
  }

  .grain {
    position: absolute;
    inset: 0;
    width: 100%;
    height: 100%;
    opacity: 0.22;
    mix-blend-mode: overlay;
    animation: grain 4.5s steps(1) infinite;
  }

  /*
   * What it says, over the middle of it.
   *
   * Centred rather than along the bottom edge: the bottom edge is where this
   * field keeps the things you can press, and a sentence there while they are
   * unreachable reads as one of them.
   */
  .caption {
    position: absolute;
    top: 50%;
    right: 0;
    left: 0;
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: var(--space-2);
    transform: translateY(-50%);
    pointer-events: none;
  }

  .spark {
    width: var(--space-6);
    height: var(--space-6);
    fill: var(--text);
    opacity: 0.75;
    animation: spark 2.6s ease-in-out infinite;
  }

  .drawing-label {
    color: var(--text);
    font-size: var(--text-sm);
    font-weight: var(--weight-medium);
    text-align: center;
    animation: pulse 2.6s ease-in-out infinite;
  }

  @keyframes drift-one {
    0%,
    100% {
      transform: translate3d(0, 0, 0) scale(1);
    }

    50% {
      transform: translate3d(18%, 22%, 0) scale(1.18);
    }
  }

  @keyframes drift-two {
    0%,
    100% {
      transform: translate3d(0, 0, 0) scale(1.1);
    }

    50% {
      transform: translate3d(-22%, -16%, 0) scale(0.92);
    }
  }

  @keyframes drift-three {
    0%,
    100% {
      transform: translate3d(0, 0, 0) scale(0.9);
    }

    50% {
      transform: translate3d(-14%, 18%, 0) scale(1.25);
    }
  }

  @keyframes sheen {
    0% {
      background-position: 130% 0;
    }

    /* Held at the far edge for the second half, so the pass is an event with
       a pause after it rather than a belt going round. */
    60%,
    100% {
      background-position: -30% 0;
    }
  }

  @keyframes grain {
    0% {
      transform: translate3d(0, 0, 0);
    }

    25% {
      transform: translate3d(-2%, 1%, 0);
    }

    50% {
      transform: translate3d(1%, -2%, 0);
    }

    75% {
      transform: translate3d(-1%, -1%, 0);
    }

    100% {
      transform: translate3d(0, 0, 0);
    }
  }

  @keyframes spark {
    0%,
    100% {
      transform: scale(0.85) rotate(0deg);
      opacity: 0.55;
    }

    50% {
      transform: scale(1.1) rotate(45deg);
      opacity: 0.95;
    }
  }

  @keyframes pulse {
    0%,
    100% {
      opacity: 0.6;
    }

    50% {
      opacity: 1;
    }
  }

  /* The wait is still a wait, so the frame still says so — it simply stops
     moving. */
  @media (prefers-reduced-motion: reduce) {
    .overlay {
      transition: none;
    }

    /* The wait is still a wait, so the frame still shows the lights and still
       says what it is doing. It simply stops moving. */
    .light,
    .sheen,
    .grain,
    .spark,
    .drawing-label {
      animation: none;
    }

    .spark {
      opacity: 0.75;
    }
  }

  .failure {
    color: var(--text-danger);
    font-size: var(--text-sm);
  }
</style>
