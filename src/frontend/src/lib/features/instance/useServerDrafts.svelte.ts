import { server, type SaveOutcome } from './stores/instance.svelte';
import {
  unreadableDatabaseNumbers,
  unreadableNumbers,
  type DatabaseDraft,
  type ServerDraft
} from './types';

export type ServerPart = 'server' | 'database';

/**
 * The editable copies of the server and database settings, and saving them: shared by first-run setup and Settings > Server,
 * which keep only what happens after a save. A copy is taken on load, so typing never edits the store.
 */
export function useServerDrafts() {
  let serverDraft = $state<ServerDraft | null>(null);
  let databaseDraft = $state<DatabaseDraft | null>(null);
  let serverOutcome = $state<SaveOutcome | null>(null);
  let databaseOutcome = $state<SaveOutcome | null>(null);
  let saving = $state<ServerPart | null>(null);

  function copyServer() {
    serverDraft = server.server ? structuredClone($state.snapshot(server.server)) : null;
  }

  function copyDatabase() {
    databaseDraft = server.database ? structuredClone($state.snapshot(server.database)) : null;
  }

  /** Runs one save; null when there is nothing valid to send. */
  async function save(
    part: ServerPart,
    send: () => Promise<SaveOutcome> | null
  ): Promise<SaveOutcome | null> {
    const pending = send();

    if (!pending) return null;

    saving = part;
    const outcome = await pending;
    saving = null;

    setOutcome(part, outcome);

    return outcome;
  }

  function setOutcome(part: ServerPart, outcome: SaveOutcome | null) {
    if (part === 'server') {
      serverOutcome = outcome;
    } else {
      databaseOutcome = outcome;
    }
  }

  return {
    get serverDraft() {
      return serverDraft;
    },
    set serverDraft(draft: ServerDraft | null) {
      serverDraft = draft;
    },
    get databaseDraft() {
      return databaseDraft;
    },
    set databaseDraft(draft: DatabaseDraft | null) {
      databaseDraft = draft;
    },
    get serverOutcome() {
      return serverOutcome;
    },
    get databaseOutcome() {
      return databaseOutcome;
    },
    /** Which part is being sent; null while idle (a restart wait after a retry is `server.phase`). */
    get saving() {
      return saving;
    },
    get busy() {
      return saving !== null || server.phase !== 'idle';
    },

    async loadAll() {
      await server.load();
      copyServer();
      copyDatabase();
    },
    async loadServer() {
      await server.loadServer();
      copyServer();
    },
    async loadDatabase() {
      await server.loadDatabase();
      copyDatabase();
    },

    saveServer: () =>
      save('server', () =>
        serverDraft && unreadableNumbers(serverDraft).length === 0
          ? server.saveServer($state.snapshot(serverDraft) as ServerDraft)
          : null
      ),
    saveDatabase: () =>
      save('database', () =>
        databaseDraft && unreadableDatabaseNumbers(databaseDraft).length === 0
          ? server.saveDatabase($state.snapshot(databaseDraft) as DatabaseDraft)
          : null
      ),

    /** Waits again for a restart that took too long. */
    async retry(part: ServerPart): Promise<SaveOutcome> {
      const outcome = await server.awaitRestart();

      setOutcome(part, outcome);

      return outcome;
    },
    dismissOutcome(part: ServerPart) {
      setOutcome(part, null);
    }
  };
}
