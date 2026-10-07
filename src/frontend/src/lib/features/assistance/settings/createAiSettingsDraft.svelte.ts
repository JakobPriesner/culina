import type { AppError } from '$api';
import { assistance } from '$features/assistance/stores/assistance.svelte';
import { session } from '$features/auth/session.svelte';
import { createAutosave } from '$features/recipes/editor/autosave.svelte';
import { explain } from '$shell/explain';
import { m } from '$shell/i18n';
import { toaster } from '$shell/toaster.svelte';

import {
  providerFacts,
  type Assistance,
  type Capability,
  type Connection,
  type Provider,
  type Use
} from '../types';

/**
 * The assistant's settings as they are being edited, and how they are kept.
 *
 * There is no Save button. A choice — a switch, a provider, a model — is saved
 * the moment it is made, and a typed field when focus leaves it: the page is a
 * handful of independent settings, and a button at the bottom of it was a step
 * that only existed to be forgotten on the way out.
 *
 * It makes no lifecycle calls of its own: the page calls `load` when it opens
 * and `dispose` when it closes.
 */
export function createAiSettingsDraft() {
  let draft = $state<Assistance | null>(null);
  /**
   * The providers whose key field is open. Opening one is not an edit: an
   * empty key means "take the stored one away", so nothing is owed until a
   * key has actually been typed.
   */
  let keyOpen = $state<Provider[]>([]);
  /** Edits made, and how many of them the server has. Plain: nothing renders them. */
  let edits = 0;
  let savedEdits = 0;
  /** The last "Saved", replaced rather than stacked by the next one. */
  let announced: string | null = null;

  const autosave = createAutosave(save);

  const copyOfSaved = (): Assistance | null =>
    assistance.settings ? structuredClone($state.snapshot(assistance.settings)) : null;

  /** Sends what changed, if anything did — on leaving a field, or on a choice made. */
  function commit(): void {
    if (edits !== savedEdits) {
      // Owed and sent at once: this page saves when a field is left, never in
      // a pause, since a key typed halfway is not a key.
      autosave.touch();
      void autosave.flush();
    }
  }

  function editConnection(provider: Provider, patch: Partial<Connection>): void {
    if (!draft) return;

    draft.connections = draft.connections.map((one) =>
      one.provider === provider ? { ...one, ...patch } : one
    );
    edits += 1;
  }

  function editUse(capability: Capability, patch: Partial<Use>): void {
    if (!draft) return;

    draft.uses = draft.uses.map((one) =>
      one.capability === capability ? { ...one, ...patch } : one
    );
    edits += 1;
  }

  async function save(): Promise<AppError | null> {
    if (!draft || edits === savedEdits) return null;

    const at = edits;
    const keysSent = draft.connections
      .filter((one) => one.apiKey !== undefined)
      .map((one) => one.provider);

    const failure = await assistance.save($state.snapshot(draft) as Assistance);

    announce(failure);

    if (!failure) {
      savedEdits = at;

      // Taken back from the server only when nothing was typed while it was
      // away: adopting its answer then would wipe what was typed since, and
      // the save that is owed for it sends the whole form again anyway.
      if (edits === at && assistance.settings) {
        draft = copyOfSaved();
        keyOpen = keyOpen.filter((provider) => !keysSent.includes(provider));
      }

      // What is set here is what decides whether the rest of the app shows an
      // assistant's buttons at all: the four switches travel with the signed-in
      // account, and that is read once when the app boots. Without this, giving
      // "improve a recipe" a provider left the editor with no button to press
      // until somebody reloaded the page — and nothing on screen said so.
      await session.refresh();
    }

    return failure;
  }

  function announce(failure: AppError | null): void {
    if (announced) {
      toaster.dismiss(announced);
    }

    announced = toaster.show(
      failure
        ? { message: () => explain(failure), tone: 'danger' }
        : { message: () => m['ai.saved'](), tone: 'success' }
    );
  }

  return {
    get draft() {
      return draft;
    },

    get keyOpen() {
      return keyOpen;
    },

    /** Whether any job is given to a provider that sends data off the machine. */
    get anythingHosted() {
      return (draft?.uses ?? []).some(
        (use) => use.provider !== '' && providerFacts[use.provider].needsApiKey
      );
    },

    async load() {
      await assistance.load();

      draft = copyOfSaved();
    },

    /** Sends what is owed on the way out, and stops the timer. */
    dispose() {
      commit();
      autosave.dispose();
    },

    commit,
    editConnection,
    editUse,

    connectionFor(provider: Provider): Connection {
      return (
        draft?.connections.find((one) => one.provider === provider) ?? {
          provider,
          apiKeyConfigured: false,
          baseUrl: '',
          usable: false
        }
      );
    },

    useFor(capability: Capability): Use {
      return (
        draft?.uses.find((one) => one.capability === capability) ?? {
          capability,
          enabled: false,
          provider: '',
          model: '',
          defaultModel: ''
        }
      );
    },

    openKey(provider: Provider) {
      keyOpen = [...keyOpen, provider];
    },

    closeKey(provider: Provider) {
      keyOpen = keyOpen.filter((one) => one !== provider);
      editConnection(provider, { apiKey: undefined });
    },

    /** Its own action, because an emptied field is a key nobody has typed yet. */
    removeKey(provider: Provider) {
      editConnection(provider, { apiKey: '' });
      commit();
    },

    setEnabled(enabled: boolean) {
      if (!draft) return;

      draft.enabled = enabled;
      edits += 1;
      commit();
    },

    setBudget(which: 'monthlyBudget' | 'personalBudget', value: number | null) {
      if (!draft) return;

      draft[which] = value;
      edits += 1;
    }
  };
}

export type AiSettingsDraft = ReturnType<typeof createAiSettingsDraft>;
