<script lang="ts">
  import { resolve } from '$app/paths';
  import { goto } from '$app/navigation';
  import { intakes } from '$features/import/intakes.svelte';
  import { onDestroy, onMount, tick } from 'svelte';
  import { drafts } from '$features/assistance/stores/drafts.svelte';
  import type { Draft } from '$features/assistance/draftToRecipe';
  import FormFailure from '$features/auth/FormFailure.svelte';
  import { sharedAddress, validSharedPhotos } from '$features/import/sharedRecipe';
  import { session } from '$features/auth/session.svelte';
  import { Button, TextArea } from '$ds';

  import type { AppError } from '$api';
  import { m } from '$shell/i18n';
  import { readRecipePage } from './importedRecipe';
  import { createAssistedIntake } from './assistedIntake';
  import { canReadClipboard, readClipboardRecipe } from './clipboard';
  import PasteActions from './PasteActions.svelte';
  import PastePhotos from './PastePhotos.svelte';
  import PastePreview from './PastePreview.svelte';
  import PasteUrlField from './PasteUrlField.svelte';
  import { createPhotoUrls } from './photoUrls.svelte';
  import { parseRecipeText, type ParsedRecipe } from './parseRecipeText';
  import { units } from '../stores/units.svelte';

  /**
   * A pasted recipe shown as a preview of what was understood before anything is created; nothing is
   * applied silently. The preview is the review: saving opens the editor, not a second read-only copy.
   */
  interface Props {
    /** Whose kitchen, so a unit it has added is read as a unit. */
    householdId: string;
    onimport: (parsed: ParsedRecipe) => void | Promise<boolean | undefined>;
    busy?: boolean;
    saveError?: AppError | null;
    /** Bindable so the page can hide its own second way in while this is open. */
    open?: boolean;
    /** A shared link to fill in; it is only read when the person taps the button beside it. */
    initialUrl?: string;
    initialText?: string;
    initialPhotos?: File[];
    /** Opened from "photograph a recipe": the photos come first and take the focus. */
    intent?: 'paste' | 'photo';
    ondraft?: (draft: Draft, sourceUrl: string) => void | Promise<boolean>;
    oncancel?: () => void;
    onqueued?: () => Promise<void>;
  }

  let {
    householdId,
    onimport,
    busy = false,
    saveError = null,
    open = $bindable(false),
    initialUrl = '',
    initialText = '',
    initialPhotos = [],
    intent = 'paste',
    ondraft,
    oncancel,
    onqueued
  }: Props = $props();

  const submitToAssistant = createAssistedIntake();
  let text = $state('');
  let url = $state('');
  let sourceUrl = $state('');
  let transcript = $state('');
  let photos = $state<File[]>([]);

  const photoUrls = createPhotoUrls(() => photos);
  onDestroy(() => drafts.dismiss());
  let reading = $state(false);
  let failure = $state<string | null>(null);
  let iphoneShareHelp = $state(false);

  /** An external link filled in but never auto-read: anyone can link here, and reading makes the server fetch it and spend the import allowance. */
  let waiting = $state(false);

  onMount(() => {
    iphoneShareHelp =
      /iPhone|iPad|iPod/.test(navigator.userAgent) ||
      (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
    if (initialText) {
      text = initialText;
      open = true;
    }
    photos = initialPhotos;
    if (initialPhotos.length) open = true;
    if (initialUrl.trim()) {
      open = true;
      url = initialUrl.trim();
      waiting = true;
    }
    if (intent === 'photo') void focusPhotos();
  });

  /** The picker cannot be opened without a tap, so the next best thing: in view and focused, one tap away. */
  async function focusPhotos() {
    open = true;
    await tick();
    const picker = document.getElementById('recipe-screenshots');
    picker?.scrollIntoView({ block: 'center' });
    picker?.focus({ preventScroll: true });
  }

  /** A site's structured data, kept apart from the text so it is not re-parsed heuristically; typing in the box overrides it. */
  let published = $state<ParsedRecipe | null>(null);

  const parsed = $derived(published ?? parseRecipeText(text, units.own));
  const locked = $derived(intakes.submitting || reading || drafts.asking);
  const tooLong = $derived(text.length + transcript.length > 20000);
  const found = $derived(parsed.ingredients.length + parsed.steps.length);

  async function read() {
    const address = url.trim();

    if (!address || reading || drafts.asking) {
      return;
    }

    waiting = false;
    reading = true;
    failure = null;

    const page = await readRecipePage(address, units.own);

    reading = false;

    if (!page) {
      failure = m['import.url.failed']();

      return;
    }

    sourceUrl = page.sourceUrl;
    transcript = page.transcript;

    if ('words' in page) {
      published = null;
      text = [...new Set([text.trim(), page.words.trim()].filter(Boolean))].join('\n\n');

      return;
    }

    published = page.recipe;

    if (!text.trim()) {
      text = page.outline;
    }
  }

  function save() {
    if (busy) return;
    void onimport({ ...parsed, sourceUrl: sourceUrl || sharedAddress(url, text) || undefined });
  }

  async function readWithAssistant() {
    if (intakes.submitting || drafts.asking || !ondraft || !session.user?.assistance.read) return;
    if (!validSharedPhotos(photos)) {
      failure = m['import.media.invalid']();
      return;
    }
    failure = null;
    const outcome = await submitToAssistant({
      householdId,
      text,
      transcript,
      sourceUrl,
      url,
      photos
    });
    if ('failure' in outcome) {
      failure = outcome.failure;
      return;
    }
    await onqueued?.();
    await goto(resolve('/(app)/recipes/imports/[intakeId]', { intakeId: outcome.jobId }));
  }

  function pickPhotos(picked: File[]) {
    if (!validSharedPhotos(picked)) {
      failure = m['import.media.invalid']();
      return;
    }
    photos = picked;
    failure = null;
  }

  async function pasteFromClipboard() {
    const copied = await readClipboardRecipe();

    if (!copied) {
      return;
    }

    text = copied.text;

    if (copied.url) {
      url = copied.url;
      void read();
    }
  }

  $effect(() => {
    if (open) {
      void units.load(householdId);
    }
  });
</script>

{#snippet photoPicker()}
  {#if ondraft}
    <PastePhotos
      {photos}
      urls={photoUrls.urls}
      disabled={locked}
      onpick={pickPhotos}
      onremove={() => (photos = [])}
    />
  {/if}
{/snippet}

{#if open}
  <section class="paste" aria-labelledby="paste-heading">
    <h2 id="paste-heading" class="heading">{m['import.intake.source']()}</h2>
    <p class="hint">{m['import.intake.hint']()}</p>
    {#if iphoneShareHelp}<p class="hint">{m['import.intake.iphone']()}</p>{/if}

    {#if intent === 'photo'}
      {@render photoPicker()}
      <p class="or">{m['import.photo.or']()}</p>
    {/if}

    <PasteUrlField
      bind:url
      {locked}
      {reading}
      asking={drafts.asking}
      canPasteClipboard={canReadClipboard}
      onedit={() => {
        sourceUrl = '';
        transcript = '';
        published = null;
        waiting = false;
      }}
      onread={() => void read()}
      onpasteclipboard={() => void pasteFromClipboard()}
    />

    {#if waiting}
      <p class="hint" role="status">
        {m['import.url.waiting']({ action: m['import.url.read']() })}
      </p>
    {/if}

    {#if failure}<p class="failure" role="alert">{failure}</p>{/if}

    <p class="or">{m['import.url.or']()}</p>

    <TextArea
      id="pasted"
      label={m['import.paste.label']()}
      bind:value={text}
      disabled={locked}
      rows={8}
      oninput={(next) => {
        text = next;
        published = null;
      }}
    />

    {#if transcript}
      <TextArea
        id="spoken-captions"
        label={m['import.review.transcript']()}
        bind:value={transcript}
        rows={4}
        disabled={locked}
      />
    {/if}

    {#if intent !== 'photo'}{@render photoPicker()}{/if}

    {#if text.trim() || published}
      <!-- role=status (polite): must not interrupt someone still reading their paste. -->
      <p class="count" role="status">
        {m['import.paste.found']({
          ingredients: parsed.ingredients.length,
          steps: parsed.steps.length
        })}
      </p>
    {/if}

    {#if found > 0}
      <PastePreview {parsed} />
    {/if}

    <FormFailure failure={saveError} />

    <PasteActions
      {busy}
      nothingFound={found === 0}
      {reading}
      assistantAvailable={Boolean(ondraft && session.user?.assistance.read)}
      assisting={intakes.submitting}
      assistantDisabled={(!text.trim() && !transcript && !photos.length && !url.trim()) || tooLong}
      photosUnreadable={photos.length > 0 && !session.user?.assistance.read}
      {tooLong}
      onsave={save}
      onassist={() => void readWithAssistant()}
      oncancel={() => {
        open = false;
        text = '';
        photos = [];
        drafts.dismiss();
        oncancel?.();
      }}
    />
  </section>
{:else}
  <div>
    <Button onclick={() => (open = true)}>{m['import.paste.open']()}</Button>
  </div>
{/if}

<style>
  .paste {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    padding-top: var(--space-4);
    border-top: 1px solid var(--border);
  }

  .heading {
    font-size: var(--text-lg);
    font-weight: var(--weight-medium);
  }

  .hint {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .or {
    color: var(--text-subtle);
    font-size: var(--text-sm);
    font-weight: var(--weight-semibold);
  }

  .failure {
    color: var(--text-danger);
    font-size: var(--text-sm);
  }

  .count {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }
</style>
