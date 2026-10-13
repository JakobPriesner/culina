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

let registered: () => void = () => {};

/**
 * Settles once the native shell has registered the Keychain/Keystore store. Reading the session token before that
 * found nothing, kept nothing for the whole launch, and the 401 that followed deleted the real token: every cold start
 * signed the person out. Telemetry (`hooks.client.ts`) asks the API before the first route registers the store.
 */
export const secretsRegistered = new Promise<void>((resolve) => {
  registered = resolve;
});

/** Called once by the native shell; anything that read the instance before waits for it. */
export function useSecretStore(next: SecretStore): void {
  store = next;
  registered();
}

export const secrets: SecretStore = {
  read: (key) => store.read(key).catch(() => null),
  write: (key, value) => store.write(key, value).catch(() => {}),
  remove: (key) => store.remove(key).catch(() => {})
};
