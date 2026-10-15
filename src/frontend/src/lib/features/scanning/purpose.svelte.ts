/**
 * What a scan is for, decided by the page that is open, not by the scanner sniffing the route: a page registers a
 * purpose while it is mounted, the scan button reads it, and the sheet's buttons follow it
 * (`docs/barcode-scanning-design.md` §4). No page registers one yet; the shopping and recipe contexts do.
 */
export type ScanPurpose =
  /** The list, the front page, the week: a scan puts the product on the shopping list. */
  | { readonly kind: 'shopping'; readonly householdId: string }
  /** A recipe, or cooking it: a scan is matched against the recipe's lines. */
  | { readonly kind: 'recipe'; readonly recipeId: string; readonly householdId: string };

class ScanPurposeStore {
  #current = $state<ScanPurpose | null>(null);

  get current(): ScanPurpose | null {
    return this.#current;
  }

  /** Called when a page mounts; returns what unregisters it, for the page's cleanup. */
  register(purpose: ScanPurpose): () => void {
    this.#current = purpose;

    return () => {
      if (this.#current === purpose) {
        this.#current = null;
      }
    };
  }

  reset(): void {
    this.#current = null;
  }
}

export const scanPurpose = new ScanPurposeStore();
