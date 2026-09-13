<script lang="ts">
  import { Button, Field, TextArea, TextInput } from '$ds';

  import { http, request } from '$api';
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
    onimport: (parsed: ParsedRecipe) => void;
    busy?: boolean;
  }

  let { householdId, onimport, busy = false }: Props = $props();

  let text = $state('');
  let open = $state(false);
  let url = $state('');
  let reading = $state(false);
  let failure = $state<string | null>(null);

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

    if (!address) {
      return;
    }

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

    // A site that publishes nothing structured gives back its words, and those
    // go through the same parser a paste does — one set of heuristics, on the
    // side where the person correcting them is.
    if (draft.text) {
      published = null;
      text = draft.text;

      return;
    }

    published = {
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
    <h2 id="paste-heading" class="heading">{m['import.paste.title']()}</h2>
    <p class="hint">{m['import.paste.hint']()}</p>

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
          />
        {/snippet}
      </Field>

      <Button loading={reading} disabled={!url.trim()} onclick={() => void read()}>
        {m['import.url.read']()}
      </Button>
    </div>

    {#if failure}<p class="failure" role="alert">{failure}</p>{/if}

    <p class="or">{m['import.url.or']()}</p>

    <TextArea
      id="pasted"
      label={m['import.paste.label']()}
      bind:value={text}
      rows={8}
      oninput={(next) => {
        text = next;
        // Typing is somebody disagreeing with what the site said.
        published = null;
      }}
    />

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
        disabled={found === 0}
        loading={busy}
        onclick={() => onimport(parsed)}
      >
        {m['import.paste.create']()}
      </Button>

      <Button
        variant="ghost"
        onclick={() => {
          open = false;
          text = '';
        }}
      >
        {m['import.paste.cancel']()}
      </Button>
    </div>
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

  .from-url {
    display: flex;
    align-items: flex-end;
    gap: var(--space-3);
  }

  .from-url :global(> :first-child) {
    flex: 1;
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
    grid-template-columns: minmax(4rem, auto) 1fr;
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
    gap: var(--space-3);
  }
</style>
