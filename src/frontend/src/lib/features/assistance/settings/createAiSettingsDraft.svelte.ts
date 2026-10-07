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
 * The assistant settings being edited; there is no Save button: a choice saves at once, a typed field on blur.
 * The page calls `load` on open and `dispose` on close.
 */
export function createAiSettingsDraft() {
  let draft = $state<Assistance | null>(null);
  /** Open key fields. Opening is not an edit: an empty key means "remove the stored one", so nothing is owed until one is typed. */
  let keyOpen = $state<Provider[]>([]);
  /** Edits made vs. edits the server has; plain, nothing renders them. */
  let edits = 0;
  let savedEdits = 0;
  /** The last "Saved", replaced rather than stacked. */
  let announced: string | null = null;

  const autosave = createAutosave(save);

  const copyOfSaved = (): Assistance | null =>
    assistance.settings ? structuredClone($state.snapshot(assistance.settings)) : null;

  /** Saves what changed, on leaving a field or making a choice. */
  function commit(): void {
    if (edits !== savedEdits) {
      // Sent at once, never debounced: a half-typed key is not a key.
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

      // Adopt the server answer only if nothing was typed meanwhile; it would wipe newer input, and the owed save resends the whole form.
      if (edits === at && assistance.settings) {
        draft = copyOfSaved();
        keyOpen = keyOpen.filter((provider) => !keysSent.includes(provider));
      }

      // The app reads the assistant switches once at boot; refresh so a newly configured job shows its button without a reload.
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

    get anythingHosted() {
      return (draft?.uses ?? []).some(
        (use) => use.provider !== '' && providerFacts[use.provider].needsApiKey
      );
    },

    async load() {
      await assistance.load();

      draft = copyOfSaved();
    },

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

    /** Separate action: an emptied field is a key nobody has typed yet. */
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
