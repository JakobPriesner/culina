import { mount, unmount } from 'svelte';
import type { RecipeReading } from '$features/recipes/types';
import { registerStore } from '$shell/stores';
import { cooking } from './stores/cooking.svelte';
import { mirrorPipDocument } from './pipDocument';

interface PipWindowApi {
  requestWindow(options: { width: number; height: number }): Promise<Window>;
}
type PipHost = Window & { documentPictureInPicture?: PipWindowApi };

/** A second view of the active session, owned by the kitchen rather than a route. */
export function createCookingPip() {
  let active = $state(false);
  let opening = $state(false);
  let sessionId = $state<string | null>(null);
  let recipe = $state.raw<RecipeReading | null>(null);
  let pipWindow: Window | null = null;
  let dispose: (() => void) | null = null;
  let generation = 0;

  function close() {
    generation++;
    const child = pipWindow;
    pipWindow = null;
    dispose?.();
    dispose = null;
    active = false;
    opening = false;
    sessionId = null;
    recipe = null;
    if (child && !child.closed) child.close();
  }

  return {
    get supported() {
      return (
        typeof window !== 'undefined' &&
        typeof (window as PipHost).documentPictureInPicture?.requestWindow === 'function'
      );
    },
    get active() {
      return active;
    },
    get opening() {
      return opening;
    },
    get sessionId() {
      return sessionId;
    },
    close,
    updateRecipe(value: RecipeReading) {
      if (recipe?.id === value.id) recipe = value;
    },
    async open(value: RecipeReading, returnToCooking: () => void): Promise<boolean> {
      const api =
        typeof window !== 'undefined' ? (window as PipHost).documentPictureInPicture : undefined;
      const current = cooking.session;
      if (!api || !current || current.recipeId !== value.id || opening) return false;
      if (active) return true;
      opening = true;
      sessionId = current.sessionId;
      const version = ++generation;
      let child: Window | undefined;
      try {
        // Request before any import/await so the click's user activation is preserved.
        child = await api.requestWindow({ width: 420, height: 560 });
        if (
          version !== generation ||
          cooking.session?.sessionId !== current.sessionId ||
          child.closed
        ) {
          child.close();
          if (version === generation) close();
          return false;
        }
        pipWindow = child;
        const onHide = () => close();
        child.addEventListener('pagehide', onHide, { once: true });
        dispose = () => child?.removeEventListener('pagehide', onHide);
        const { default: Surface } = await import('./CookingPipSurface.svelte');
        if (
          version !== generation ||
          cooking.session?.sessionId !== current.sessionId ||
          child.closed
        ) {
          child.close();
          if (version === generation) close();
          return false;
        }
        recipe = value;
        child.document.title = value.title;
        const target = child.document.createElement('div');
        child.document.body.append(target);
        const component = mount(Surface, {
          target,
          props: {
            get recipe() {
              return recipe!;
            },
            onreturn: () => {
              window.focus();
              close();
              returnToCooking();
            }
          }
        });
        let stopMirroring = () => {};
        dispose = () => {
          child?.removeEventListener('pagehide', onHide);
          stopMirroring();
          void unmount(component);
          target.remove();
        };
        stopMirroring = mirrorPipDocument(document, child.document);
        opening = false;
        active = true;
        return true;
      } catch {
        child?.close();
        if (version === generation) close();
        return false;
      }
    }
  };
}

export const cookingPip = createCookingPip();
registerStore(() => cookingPip.close());
