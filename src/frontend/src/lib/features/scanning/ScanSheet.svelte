<script lang="ts">
  import type { Snippet } from 'svelte';

  import type { AppError } from '$api';
  import { Button, Field, Sheet, TextInput, VisuallyHidden } from '$ds';
  import { haptics } from '$shell/haptics';
  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';

  import { readGtin } from './gtin';
  import { lookupProduct, nameProduct, type Product } from './products';
  import Viewfinder from './Viewfinder.svelte';

  /**
   * Scans a barcode, or takes its digits, and says what the household calls it (`docs/barcode-scanning-design.md`
   * §3, §6). A known code shows its name; an unknown one asks for a name once, with one text field, and keeps the
   * answer in the household's memory. What happens with a known product is the page's business, given as `actions`
   * by the purpose the page registered (`purpose.svelte.ts`): the sheet resolves, it does not decide.
   * Typing the digits is always offered, for a crumpled code, a denied camera or a laptop with no rear camera.
   */
  interface Props {
    open: boolean;
    householdId: string;
    /** The page's buttons for a known product, shown under its name. */
    actions?: Snippet<[Product]>;
    onresolved?: (product: Product) => void;
    onclose?: () => void;
  }

  let { open = $bindable(), householdId, actions, onresolved, onclose }: Props = $props();

  type Phase =
    | { readonly kind: 'scanning' }
    | { readonly kind: 'looking' }
    | { readonly kind: 'known'; readonly product: Product }
    | { readonly kind: 'unknown'; readonly gtin: string }
    | { readonly kind: 'failed'; readonly gtin: string; readonly error: AppError };

  let phase = $state<Phase>({ kind: 'scanning' });
  let typed = $state('');
  let invalid = $state(false);
  let name = $state('');
  let saving = $state(false);
  let failure = $state<string | null>(null);

  // A fresh start each time it opens.
  $effect(() => {
    if (open) {
      phase = { kind: 'scanning' };
      typed = '';
      invalid = false;
    }
  });

  async function resolve(code: string) {
    const gtin = readGtin(code);

    if (gtin === null) {
      // A misread; the camera was stopped by the lock, so it starts again.
      phase = { kind: 'scanning' };
      invalid = true;

      return;
    }

    phase = { kind: 'looking' };

    const result = await lookupProduct(householdId, gtin);

    if (!result.ok) {
      phase = { kind: 'failed', gtin, error: result.error };

      return;
    }

    if (result.value === null) {
      name = '';
      failure = null;
      phase = { kind: 'unknown', gtin };

      return;
    }

    known(result.value);
  }

  function known(product: Product) {
    haptics.step();
    phase = { kind: 'known', product };
    onresolved?.(product);
  }

  function detected(code: string) {
    typed = code;
    void resolve(code);
  }

  function submitDigits(event: SubmitEvent) {
    event.preventDefault();

    if (readGtin(typed) === null) {
      invalid = true;

      return;
    }

    void resolve(typed);
  }

  async function submitName(event: SubmitEvent) {
    event.preventDefault();

    if (phase.kind !== 'unknown') {
      return;
    }

    const text = name.trim();

    if (!text) {
      failure = m['scan.unknown.needed']();

      return;
    }

    const { gtin } = phase;

    saving = true;

    const result = await nameProduct(householdId, gtin, text);

    saving = false;

    if (result.ok) {
      failure = null;
      known(result.value);

      return;
    }

    // Another phone named it first: what the household calls it now is what to show.
    if (result.error.code === 'products.already_named') {
      await resolve(gtin);

      return;
    }

    failure = explain(result.error);
  }

  function again() {
    typed = '';
    invalid = false;
    phase = { kind: 'scanning' };
  }

  const announcement = $derived(
    phase.kind === 'known' ? m['scan.found']({ name: phase.product.name }) : ''
  );
</script>

<Sheet bind:open title={m['scan.title']()} closeLabel={m['scan.close']()} {onclose}>
  <div class="scan">
    <VisuallyHidden><span role="status" aria-live="polite">{announcement}</span></VisuallyHidden>

    {#if phase.kind === 'scanning'}
      <Viewfinder ondetect={detected} />

      <form class="digits" onsubmit={submitDigits} novalidate>
        <Field
          label={m['scan.digits.label']()}
          hint={m['scan.digits.hint']()}
          error={invalid ? m['scan.digits.invalid']() : undefined}
        >
          {#snippet children({ id, describedBy, invalid: bad })}
            <TextInput
              {id}
              {describedBy}
              invalid={bad}
              bind:value={typed}
              inputmode="numeric"
              autocomplete="off"
              oninput={() => (invalid = false)}
            />
          {/snippet}
        </Field>
        <Button type="submit" variant="primary">{m['scan.digits.look']()}</Button>
      </form>
    {:else if phase.kind === 'looking'}
      <p class="quiet" role="status">{m['scan.looking']()}</p>
    {:else if phase.kind === 'known'}
      <section class="known" aria-label={phase.product.name}>
        <h3>{phase.product.name}</h3>
        {#if phase.product.ingredient && phase.product.ingredient !== phase.product.name}
          <p class="quiet">{m['scan.known.counts']({ ingredient: phase.product.ingredient })}</p>
        {/if}
        {#if phase.product.cookMinutes !== null}
          <p class="quiet">{m['scan.known.cook']({ minutes: phase.product.cookMinutes })}</p>
        {/if}
        {#if actions}
          <div class="actions">{@render actions(phase.product)}</div>
        {/if}
      </section>
    {:else if phase.kind === 'unknown'}
      <form class="naming" onsubmit={submitName} novalidate>
        <h3>{m['scan.unknown.title']()}</h3>
        <p class="quiet">{m['scan.unknown.hint']({ code: phase.gtin })}</p>
        <Field label={m['scan.unknown.name']()}>
          {#snippet children({ id, describedBy, invalid: bad })}
            <TextInput
              {id}
              {describedBy}
              invalid={bad}
              bind:value={name}
              autocomplete="off"
              maxlength={200}
            />
          {/snippet}
        </Field>
        {#if failure}
          <p class="failure" role="alert">{failure}</p>
        {/if}
        <Button type="submit" variant="primary" loading={saving}>{m['scan.unknown.save']()}</Button>
      </form>
    {:else}
      <div class="naming">
        <p class="failure" role="alert">{explain(phase.error)}</p>
        <Button onclick={() => phase.kind === 'failed' && void resolve(phase.gtin)}>
          {m['scan.retry']()}
        </Button>
      </div>
    {/if}
  </div>

  {#snippet footer()}
    {#if phase.kind === 'known' || phase.kind === 'unknown' || phase.kind === 'failed'}
      <Button onclick={again}>{m['scan.another']()}</Button>
    {/if}
    <Button
      variant={phase.kind === 'known' ? 'primary' : 'secondary'}
      onclick={() => (open = false)}
    >
      {m['scan.done']()}
    </Button>
  {/snippet}
</Sheet>

<style>
  .scan {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    min-width: 0;
  }

  .digits,
  .naming,
  .known {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
  }

  h3 {
    margin: 0;
    font-size: var(--text-lg);
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
  }

  .quiet {
    margin: 0;
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .failure {
    margin: 0;
    color: var(--text-danger);
    font-size: var(--text-sm);
  }
</style>
