<script lang="ts">
  import { resolve } from '$app/paths';
  import { goto } from '$app/navigation';
  import { intakes } from '$features/import/intakes.svelte';
  import { explain } from '$shell/explain';
  import { onDestroy, onMount } from 'svelte';
  import DraftReview from '$features/assistance/DraftReview.svelte';
  import { drafts } from '$features/assistance/stores/drafts.svelte';
  import type { Draft } from '$features/assistance/draftToRecipe';
  import { intakeBaseline, intakeDraft } from '$features/import/intakeDraft';
  import { sharedAddress, validSharedPhotos } from '$features/import/sharedRecipe';
  import { session } from '$features/auth/session.svelte';
  import { Button, Field, TextArea, TextInput } from '$ds';

  import { http, request, type AppError } from '$api';
  import { m } from '$shell/i18n';
  import { parseIngredientLine } from './parseIngredientLine';
  import { parseRecipeText, type ParsedRecipe } from './parseRecipeText';
  import { formatQuantity } from '../formatQuantity';
  import { quantityLabels } from '../quantityLabels';
  import { scaleQuantity } from '../scaling';
  import { units } from '../stores/units.svelte';
  import { preferences } from '$shell/preferences.svelte';

  /**
   * A recipe pasted in, read back before anything is made of it.
   *
   * Almost every recipe arrives as a block of text — a message from a friend, a
   * page copied out of a browser, something typed out of a book — and retyping
   * it line by line is why most of them never get written down.
   *
   * The preview is the feature, not the parser. What was understood is shown as
   * separate parts before a recipe exists, so a wrong reading is obvious and
   * costs a keystroke instead of a deletion. Nothing is applied silently.
   */
  interface Props {
    /** Whose kitchen, so a unit it has added is read as a unit. */
    householdId: string;
    onimport: (parsed: ParsedRecipe) => void | Promise<boolean | undefined>;
    busy?: boolean;
    saveError?: AppError | null;
    /**
     * Whether the box is open, readable by the page around it.
     *
     * The page offers a second way in beside this one, and two invitations
     * either side of an open editor is one invitation too many — so the caller
     * has to be able to see that this one has been taken.
     */
    open?: boolean;
    /**
     * A URL to fill in (from the OS share target, or a link to this page).
     * Filled in only: it is read when somebody taps the button beside it.
     */
    initialUrl?: string;
    /** An initial block of text to populate. */
    initialText?: string;
    initialPhotos?: File[];
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
    ondraft,
    oncancel,
    onqueued
  }: Props = $props();

  let submissionId: string | undefined;
  let submissionSignature: string | undefined;
  let text = $state('');
  let url = $state('');
  let sourceUrl = $state('');
  let transcript = $state('');
  let photos = $state<File[]>([]);
  let reviewing = $state(false);
  let assisted = $state(false);
  let reviewed = $state<ParsedRecipe | null>(null);
  let reviewDraft = $state<Draft | null>(null);
  const current = $derived(intakeBaseline(householdId));

  // One object URL per photo for as long as the photo is held: adding one
  // photo does not re-read and re-decode the others.
  const objectUrls = new WeakMap<File, string>();
  let withUrls: File[] = [];
  const photoUrls = $derived(
    photos.map((file) => {
      let url = objectUrls.get(file);

      if (!url) {
        url = URL.createObjectURL(file);
        objectUrls.set(file, url);
        withUrls.push(file);
      }

      return url;
    })
  );
  const revoke = (kept: readonly File[]) => {
    for (const file of withUrls) {
      if (!kept.includes(file)) {
        URL.revokeObjectURL(objectUrls.get(file)!);
        objectUrls.delete(file);
      }
    }

    withUrls = withUrls.filter((file) => kept.includes(file));
  };
  $effect(() => revoke(photos));
  onDestroy(() => {
    drafts.dismiss();
    revoke([]);
  });
  let reading = $state(false);
  let failure = $state<string | null>(null);
  let iphoneShareHelp = $state(false);

  const canPasteClipboard =
    typeof navigator !== 'undefined' &&
    'clipboard' in navigator &&
    typeof navigator.clipboard?.readText === 'function';

  /**
   * A link that arrived from outside, filled in and not yet read.
   *
   * Never read on arrival. Reading makes the server fetch the address, and any
   * page or message can link here with one in the query string — or post one
   * to the share target — so arriving must not be enough to make this server
   * fetch somebody's address or spend their import allowance. The link is put
   * in the field and the person decides, with the one button beside it.
   */
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
  });

  /**
   * What a website published, when a website published it.
   *
   * Held apart from the pasted text rather than folded into it: structured data
   * is what the site said, and rewriting it into a block of words only to read
   * it back with heuristics would lose the very thing that made it worth
   * fetching. Typing into the box takes over, because that is somebody
   * disagreeing with it.
   */
  let published = $state<ParsedRecipe | null>(null);

  const parsed = $derived(published ?? parseRecipeText(text, units.own));
  const found = $derived(parsed.ingredients.length + parsed.steps.length);

  /**
   * Reads a recipe from a web page.
   *
   * The server does the fetching — it has to, because a browser cannot read
   * another site — which is why it refuses every address that is not an
   * ordinary public page. What comes back is a draft, and it lands in the same
   * preview a paste does: nothing is created until somebody looks at it.
   */
  async function read() {
    const address = url.trim();

    if (!address || reading || drafts.asking) {
      return;
    }

    waiting = false;
    reading = true;
    failure = null;

    const result = await request(() =>
      http.POST('/api/v1/recipe-imports', { body: { url: address } })
    );

    reading = false;

    if (!result.ok) {
      failure = m['import.url.failed']();

      return;
    }

    const draft = result.value;
    sourceUrl = draft.sourceUrl;
    transcript = draft.transcript ?? '';

    // A site that publishes nothing structured gives back its words, and those
    // go through the same parser a paste does — one set of heuristics, on the
    // side where the person correcting them is.
    if (draft.text) {
      published = null;
      // Keep the words the person shared beside what the page publishes.
      text = [...new Set([text.trim(), draft.text.trim()].filter(Boolean))].join('\n\n');

      return;
    }

    published = {
      sourceUrl,
      title: draft.title ?? '',
      ingredients: draft.ingredientLines.map((line) => parseIngredientLine(line, units.own)),
      steps: [...draft.steps],
      ...(draft.servings === undefined || draft.servings === null
        ? {}
        : { servings: draft.servings }),
      ...(draft.totalMinutes === undefined || draft.totalMinutes === null
        ? {}
        : { totalMinutes: draft.totalMinutes })
    };
    if (!text.trim()) {
      text = [draft.title, ...draft.ingredientLines, ...draft.steps].filter(Boolean).join('\n');
    }
  }

  function reviewParsed() {
    reviewed = { ...parsed, sourceUrl: sourceUrl || sharedAddress(url, text) || undefined };
    assisted = false;
    reviewDraft = intakeDraft(reviewed);
    reviewing = true;
  }

  async function readWithAssistant() {
    if (intakes.submitting || drafts.asking || !ondraft || !session.user?.assistance.read) return;
    if (!validSharedPhotos(photos)) {
      failure = m['import.media.invalid']();
      return;
    }
    failure = null;
    const signature = JSON.stringify([
      householdId,
      preferences.locale,
      text,
      transcript,
      sourceUrl,
      url,
      photos.map((photo) => [photo.name, photo.size, photo.lastModified])
    ]);
    if (signature !== submissionSignature) {
      submissionSignature = signature;
      submissionId = crypto.randomUUID();
    }
    const job = await intakes.start({
      id: (submissionId ??= crypto.randomUUID()),
      householdId,
      language: preferences.locale,
      material: text,
      transcript,
      sourceUrl: sourceUrl || sharedAddress(url, text) || undefined,
      photos,
      fetchSource: !sourceUrl && Boolean(sharedAddress(url, text))
    });
    if (!job) {
      failure = intakes.error ? explain(intakes.error) : m['intake.failed']();
      return;
    }
    await onqueued?.();
    await goto(resolve('/(app)/recipes/imports/[intakeId]', { intakeId: job.id }));
  }

  function pickPhotos(event: Event) {
    const picked = Array.from((event.currentTarget as HTMLInputElement).files ?? []);
    if (!validSharedPhotos(picked)) {
      failure = m['import.media.invalid']();
      return;
    }
    photos = picked;
    failure = null;
  }

  async function acceptReviewed() {
    if (busy) return;
    const outcome = reviewed
      ? await onimport(reviewed)
      : drafts.draft && !drafts.asking && !drafts.error
        ? await ondraft?.(drafts.draft, sourceUrl || sharedAddress(url, text))
        : false;
    if (outcome !== false) reviewing = false;
  }

  async function pasteFromClipboard() {
    if (!canPasteClipboard) return;
    try {
      const clipboardText = await navigator.clipboard.readText();
      const trimmed = clipboardText.trim();
      const match = trimmed.match(/https?:\/\/[^\s]+/);
      text = trimmed;
      if (match) {
        url = match[0];
        void read();
      } else if (trimmed) {
        text = trimmed;
      }
    } catch {
      // Clipboard access denied or unavailable
    }
  }

  const shown = (quantity: Parameters<typeof scaleQuantity>[0]) =>
    formatQuantity(scaleQuantity(quantity, 1), preferences.locale, quantityLabels).text;

  $effect(() => {
    if (open) {
      void units.load(householdId);
    }
  });
</script>

{#if open}
  <section class="paste" aria-labelledby="paste-heading">
    <h2 id="paste-heading" class="heading">{m['import.intake.source']()}</h2>
    <p class="hint">{m['import.intake.hint']()}</p>
    {#if iphoneShareHelp}<p class="hint">{m['import.intake.iphone']()}</p>{/if}

    <div class="from-url">
      <Field label={m['import.url.label']()}>
        {#snippet children({ id, describedBy, invalid })}
          <TextInput
            {id}
            {describedBy}
            {invalid}
            type="url"
            inputmode="url"
            placeholder="https://"
            bind:value={url}
            disabled={intakes.submitting || reading || drafts.asking}
            oninput={() => {
              sourceUrl = '';
              transcript = '';
              published = null;
              waiting = false;
            }}
          />
        {/snippet}
      </Field>

      {#if !url.trim() && canPasteClipboard}
        <Button variant="ghost" onclick={() => void pasteFromClipboard()}>
          {m['import.url.pasteClipboard']()}
        </Button>
      {/if}

      <Button loading={reading} disabled={!url.trim() || drafts.asking} onclick={() => void read()}>
        {m['import.url.read']()}
      </Button>
    </div>

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
      disabled={intakes.submitting || reading || drafts.asking}
      rows={8}
      oninput={(next) => {
        text = next;
        // Typing is somebody disagreeing with what the site said.
        published = null;
      }}
    />

    {#if transcript}
      <TextArea
        id="spoken-captions"
        label={m['import.review.transcript']()}
        bind:value={transcript}
        rows={4}
        disabled={intakes.submitting || reading || drafts.asking}
      />
    {/if}

    {#if ondraft}
      <div class="media">
        <label for="recipe-screenshots">{m['import.media.label']()}</label>
        <input
          id="recipe-screenshots"
          type="file"
          accept="image/jpeg,image/png,image/webp"
          multiple
          disabled={intakes.submitting || reading || drafts.asking}
          onchange={pickPhotos}
        />
        <p class="hint">{m['import.media.hint']()}</p>
        {#if photos.length}
          <div class="photos">
            {#each photoUrls as photo, index (photo)}
              <img
                src={photo}
                alt={photos[index]?.name ?? m['import.review.photo']()}
                loading="lazy"
                decoding="async"
              />
            {/each}
          </div>
          <Button variant="ghost" onclick={() => (photos = [])}>{m['import.media.remove']()}</Button
          >
        {/if}
      </div>
    {/if}

    {#if text.trim() || published}
      <!-- Polite: it reports what was understood while somebody is still
           looking at what they pasted, and must not interrupt them. -->
      <p class="count" role="status">
        {m['import.paste.found']({
          ingredients: parsed.ingredients.length,
          steps: parsed.steps.length
        })}
      </p>
    {/if}

    {#if found > 0}
      <div class="preview">
        {#if parsed.title}
          <p class="title">{parsed.title}</p>
        {/if}

        {#if parsed.ingredients.length > 0}
          <h3 class="section">{m['editor.ingredients']()}</h3>
          <ul class="list">
            {#each parsed.ingredients as ingredient, index (index)}
              <li class="row">
                <span class="amount">{shown(ingredient.quantity)}</span>
                <span
                  >{ingredient.name}{#if ingredient.note}<span class="note"
                      >, {ingredient.note}</span
                    >{/if}</span
                >
              </li>
            {/each}
          </ul>
        {/if}

        {#if parsed.steps.length > 0}
          <h3 class="section">{m['editor.steps']()}</h3>
          <ol class="steps">
            {#each parsed.steps as step, index (index)}
              <li>{step}</li>
            {/each}
          </ol>
        {/if}
      </div>
    {/if}

    <div class="actions">
      <Button
        variant="primary"
        loading={busy}
        disabled={found === 0 || reading}
        onclick={reviewParsed}
      >
        {m['import.review.preview']()}
      </Button>

      {#if ondraft && session.user?.assistance.read}
        <Button
          loading={intakes.submitting}
          disabled={intakes.submitting ||
            (!text.trim() && !transcript && !photos.length && !url.trim()) ||
            text.length + transcript.length > 20000}
          onclick={() => void readWithAssistant()}
        >
          {m['import.media.read']()}
        </Button>
      {/if}

      <Button
        variant="ghost"
        onclick={() => {
          open = false;
          text = '';
          photos = [];
          drafts.dismiss();
          oncancel?.();
        }}
      >
        {m['import.paste.cancel']()}
      </Button>
    </div>
    {#if photos.length && !session.user?.assistance.read}
      <p class="hint">{m['import.media.unavailable']()}</p>
    {/if}
    {#if text.length + transcript.length > 20000}<p class="hint">
        {m['import.media.tooLong']()}
      </p>{/if}
  </section>
{:else}
  <div>
    <Button onclick={() => (open = true)}>{m['import.paste.open']()}</Button>
  </div>
{/if}

{#if reviewing}
  <DraftReview
    bind:open={reviewing}
    draft={assisted ? drafts.draft : reviewDraft}
    {current}
    writing={assisted && drafts.asking}
    error={assisted ? drafts.error : null}
    source={{
      text,
      transcript,
      url: sourceUrl || sharedAddress(url, text),
      photos: photoUrls,
      assisted
    }}
    saving={busy}
    {saveError}
    onaccept={() => void acceptReviewed()}
    onclose={() => {
      reviewing = false;
      drafts.dismiss();
    }}
  />
{/if}

<style>
  .media {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
  }
  .media input {
    max-width: 100%;
    font: inherit;
  }
  .photos {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(6rem, 1fr));
    gap: var(--space-2);
  }
  .photos img {
    width: 100%;
    max-height: 12rem;
    object-fit: contain;
    border-radius: var(--radius-md);
  }

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

  .from-url {
    display: flex;
    flex-wrap: wrap;
    align-items: flex-end;
    gap: var(--space-3);
  }

  .from-url :global(> :first-child) {
    flex: 1 1 16rem;
    min-width: 0;
  }

  .or {
    color: var(--text-subtle);
    font-size: var(--text-xs);
    letter-spacing: 0.08em;
    text-transform: uppercase;
  }

  .failure {
    color: var(--text-danger);
    font-size: var(--text-sm);
  }

  .count {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  /* Sunken, so the preview reads as a quotation of what was pasted rather than
     as a form that has already been filled in. */
  .preview {
    padding: var(--space-4);
    border-radius: var(--radius-md);
    background: var(--surface-sunken);
  }

  .title {
    font-family: var(--font-editorial);
    font-size: var(--text-lg);
    margin-bottom: var(--space-3);
  }

  .section {
    margin-top: var(--space-3);
    color: var(--text-subtle);
    font-size: var(--text-xs);
    letter-spacing: 0.08em;
    text-transform: uppercase;
  }

  .list {
    margin: var(--space-2) 0 0;
    padding: 0;
    list-style: none;
  }

  .row {
    display: grid;
    grid-template-columns: minmax(4rem, auto) minmax(0, 1fr);
    gap: var(--space-3);
    padding-block: var(--space-1);
  }

  .amount {
    font-variant-numeric: tabular-nums;
    font-weight: var(--weight-medium);
    white-space: nowrap;
  }

  .note {
    color: var(--text-muted);
  }

  .steps {
    /* Numbers inside, so they line up with the headings above rather than
       hanging into the panel's padding. */
    list-style-position: inside;
    margin: var(--space-2) 0 0;
    padding: 0;
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-3);
  }
</style>
