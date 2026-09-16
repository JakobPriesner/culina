<script lang="ts">
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
   */
  interface Props {
    label: string;
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
    /** What went wrong, already in words the reader can act on. */
    failure?: string | null;
    onpick: (file: File) => void;
    onremove: () => void;
  }

  let {
    label,
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
    failure = null,
    onpick,
    onremove
  }: Props = $props();

  let picker = $state<ReturnType<typeof FilePicker>>();
</script>

<div class="field">
  <p class="label">{label}</p>

  <div class="frame">
    {#if src}
      <Image {src} {srcset} {sizes} {alt} {ratio} />
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
  </div>

  <div class="actions">
    <Button loading={busy} onclick={() => picker?.open()}>
      {src ? replaceLabel : chooseLabel}
    </Button>

    {#if src}
      <Button variant="ghost" disabled={busy} onclick={onremove}>{removeLabel}</Button>
    {/if}
  </div>

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
    width: min(30rem, 100%);
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
    gap: var(--space-3);
  }

  .failure {
    color: var(--text-danger);
    font-size: var(--text-sm);
  }
</style>
