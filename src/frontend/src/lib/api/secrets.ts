/** Where the app keeps its session token. */
export interface SecretStore {
  read(key: string): Promise<string | null>;
  write(key: string, value: string): Promise<void>;
  remove(key: string): Promise<void>;
}

/** Until the shell registers the Keychain/Keystore one: memory, so a token never lands in localStorage, where any script could read it for good. */
function memoryStore(): SecretStore {
  const kept = new Map<string, string>();

  return {
    read: (key) => Promise.resolve(kept.get(key) ?? null),
    write: (key, value) => {
      kept.set(key, value);

      return Promise.resolve();
    },
    remove: (key) => {
      kept.delete(key);

      return Promise.resolve();
    }
  };
}

let store: SecretStore = memoryStore();

/** Called once by the native shell before anything reads the instance. */
export function useSecretStore(next: SecretStore): void {
  store = next;
}

export const secrets: SecretStore = {
  read: (key) => store.read(key).catch(() => null),
  write: (key, value) => store.write(key, value).catch(() => {}),
  remove: (key) => store.remove(key).catch(() => {})
};
