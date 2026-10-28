<script lang="ts">
  import { resolve } from '$app/paths';
  import { http, request, type AppError } from '$api';
  import { Button, Field, Modal, SegmentedControl, Switch, TextInput } from '$ds';
  import { diary } from '$features/diary/stores/diary.svelte';
  import { diarySettings } from '$features/diary/stores/diarySettings.svelte';
  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';
  import { toaster } from '$shell/toaster.svelte';

  import SettingsRow from '../SettingsRow.svelte';
  import SettingsSection from '../SettingsSection.svelte';

  /**
   * The food diary's settings (docs/diary-design.md §5, §8): one switch whose single sentence is the agreement, the
   * person's own targets, and how the day's headline reads. Everything saves as it is set, like every setting outside
   * Mail and Server (docs/design-system.md): a target when its field is left or Enter pressed, with a quiet "Saved"
   * or the error and a retry under the group. Switching off asks whether to delete the entries too; deleting
   * everything otherwise takes a typed word, the one place an undo is not offered instead.
   */
  const settings = $derived(diarySettings.settings);

  $effect(() => {
    void diarySettings.load();
  });

  const fieldId = $props.id();

  let kcal = $state('');
  let protein = $state('');
  let saving = $state(false);
  /** What the last save from the targets group did, said under it. */
  let targets = $state<'idle' | 'saved' | AppError>('idle');
  let askingOff = $state(false);
  let erasing = $state(false);
  let confirmWord = $state('');

  // The fields follow what was stored, each time it changes.
  $effect(() => {
    kcal = settings?.kcalTarget ? String(settings.kcalTarget) : '';
    protein = settings?.proteinTarget ? String(settings.proteinTarget) : '';
  });

  const whole = (text: string): number | null => {
    const parsed = Number.parseInt(text.trim(), 10);

    return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
  };

  const lowTarget = $derived((whole(kcal) ?? Infinity) < 1200);

  const stored = (target: number | null | undefined) => (target ? String(target) : '');

  /** Leaving a target's field saves it, unless nothing changed. */
  function commit() {
    if (
      kcal.trim() === stored(settings?.kcalTarget) &&
      protein.trim() === stored(settings?.proteinTarget)
    ) {
      return;
    }

    void save({}, true);
  }

  function enterCommits(event: KeyboardEvent) {
    if (event.key === 'Enter' && event.target instanceof HTMLInputElement) {
      event.target.blur();
    }
  }

  async function save(
    change: {
      enabled?: boolean;
      headline?: 'eaten' | 'remaining';
      eraseEntries?: boolean;
    },
    inTargets = false
  ) {
    saving = true;

    const failure = await diarySettings.save({
      enabled: change.enabled ?? diarySettings.enabled,
      kcalTarget: whole(kcal),
      proteinTarget: whole(protein),
      headline: change.headline ?? (settings?.headline === 'remaining' ? 'remaining' : 'eaten'),
      eraseEntries: change.eraseEntries
    });

    saving = false;

    if (inTargets) {
      targets = failure ?? 'saved';
    } else if (failure) {
      toaster.show({ message: () => explain(failure), tone: 'danger' });
    } else if (change.eraseEntries) {
      diary.reset();
    }
  }

  function switched(on: boolean) {
    if (on) {
      void save({ enabled: true });
    } else {
      askingOff = true;
    }
  }

  async function off(eraseEntries: boolean) {
    askingOff = false;
    await save({ enabled: false, eraseEntries });
  }

  async function eraseAll() {
    const result = await request(() => http.DELETE('/api/v1/users/me/diary'));

    erasing = false;
    confirmWord = '';

    if (result.ok) {
      diary.reset();
      toaster.show({ message: () => m['me.diary.erased'](), tone: 'success' });
    } else {
      toaster.show({ message: () => explain(result.error), tone: 'danger' });
    }
  }
</script>

<svelte:head><title>{m['me.diary']()}</title></svelte:head>

<SettingsSection>
  <SettingsRow label={m['me.diary.switch']()} description={m['me.diary.consent']()}>
    <Switch
      hideLabel
      checked={diarySettings.enabled}
      label={m['me.diary.switch']()}
      description={m['me.diary.consent']()}
      disabled={!settings || saving}
      onchange={switched}
    />
  </SettingsRow>
  <p class="honest">{m['me.diary.honest']()}</p>
</SettingsSection>

{#if diarySettings.enabled}
  <SettingsSection title={m['me.diary.targets']()} description={m['me.diary.targets.body']()}>
    <SettingsRow label={m['me.diary.kcalTarget']()} description={m['me.diary.kcalTarget.hint']()}>
      <!-- Listens for its field's blur and Enter; the field is the control. -->
      <!-- svelte-ignore a11y_no_static_element_interactions -->
      <div class="commit" onfocusout={commit} onkeydown={enterCommits}>
        <TextInput
          id="{fieldId}-kcal"
          label={m['me.diary.kcalTarget']()}
          value={kcal}
          inputmode="numeric"
          oninput={(text) => (kcal = text)}
        />
      </div>
    </SettingsRow>
    {#if lowTarget}
      <!-- A calm sentence and a way to help, never a refusal (docs/diary-design.md §5). -->
      <p class="floor">
        {m['me.diary.floor']()}
        <a href="https://www.bzga-essstoerungen.de" rel="noopener" target="_blank"
          >{m['me.diary.floor.link']()}</a
        >
      </p>
    {/if}
    <SettingsRow label={m['me.diary.proteinTarget']()}>
      <!-- Listens for its field's blur and Enter; the field is the control. -->
      <!-- svelte-ignore a11y_no_static_element_interactions -->
      <div class="commit" onfocusout={commit} onkeydown={enterCommits}>
        <TextInput
          id="{fieldId}-protein"
          label={m['me.diary.proteinTarget']()}
          value={protein}
          inputmode="numeric"
          oninput={(text) => (protein = text)}
        />
      </div>
    </SettingsRow>
    <SettingsRow label={m['me.diary.headline']()} description={m['me.diary.headline.hint']()}>
      <SegmentedControl
        label={m['me.diary.headline']()}
        selected={settings?.headline ?? 'eaten'}
        segments={[
          { id: 'eaten', label: m['me.diary.headline.eaten']() },
          { id: 'remaining', label: m['me.diary.headline.remaining']() }
        ]}
        onselect={(id) => void save({ headline: id as 'eaten' | 'remaining' }, true)}
      />
    </SettingsRow>
    <div class="actions">
      {#if targets === 'saved'}
        <p class="saved" role="status">{m['me.diary.saved']()}</p>
      {:else if targets !== 'idle'}
        <div class="failed" role="alert">
          <p>{explain(targets)}</p>
          <Button size="sm" onclick={() => void save({}, true)}>{m['error.retry']()}</Button>
        </div>
      {/if}
      <Button variant="ghost" href={resolve('/(app)/diary')}>{m['me.diary.open']()}</Button>
    </div>
  </SettingsSection>
{/if}

<SettingsSection title={m['me.diary.erase']()} description={m['me.diary.erase.body']()}>
  <div class="actions">
    <Button onclick={() => (erasing = true)}>{m['me.diary.erase']()}</Button>
  </div>
</SettingsSection>

<Modal
  open={askingOff}
  title={m['me.diary.off.title']()}
  closeLabel={m['app.dismiss']()}
  onclose={() => (askingOff = false)}
>
  <p>{m['me.diary.off.body']()}</p>
  {#snippet footer()}
    <Button onclick={() => void off(false)}>{m['me.diary.off.keep']()}</Button>
    <Button variant="primary" onclick={() => void off(true)}>{m['me.diary.off.delete']()}</Button>
  {/snippet}
</Modal>

<Modal
  open={erasing}
  title={m['me.diary.erase']()}
  closeLabel={m['app.dismiss']()}
  onclose={() => (erasing = false)}
>
  <Field label={m['me.diary.erase.type']({ word: m['me.diary.erase.word']() })}>
    {#snippet children({ id, describedBy })}
      <TextInput {id} {describedBy} value={confirmWord} oninput={(text) => (confirmWord = text)} />
    {/snippet}
  </Field>
  {#snippet footer()}
    <Button onclick={() => (erasing = false)}>{m['app.dismiss']()}</Button>
    <Button
      variant="danger"
      disabled={confirmWord.trim().toLowerCase() !== m['me.diary.erase.word']().toLowerCase()}
      onclick={() => void eraseAll()}>{m['me.diary.erase.confirm']()}</Button
    >
  {/snippet}
</Modal>

<style>
  /* Notes inside a card take the rows' inset from SettingsSection. */
  .honest,
  .floor {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2);
    padding: var(--space-4);
  }

  .saved {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .failed {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2);
    color: var(--text-danger);
    font-size: var(--text-sm);
  }

  /* The control column sizes to its field. */
  .commit {
    display: contents;
  }
</style>
