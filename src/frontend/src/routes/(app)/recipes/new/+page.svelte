<script lang="ts">
  import { onMount } from 'svelte';

  import { goto } from '$app/navigation';
  import { resolve } from '$app/paths';
  import { page } from '$app/state';
  import FormFailure from '$features/auth/FormFailure.svelte';
  import { session } from '$features/auth/session.svelte';
  import IdeaDraft from '$features/assistance/IdeaDraft.svelte';
  import { drafts } from '$features/assistance/stores/drafts.svelte';
  import { recallSharedRecipe, sharedAddress } from '$features/import/sharedRecipe';
  import { readIncomingShare } from '$features/recipes/create/incomingShare';
  import OtherWaysIn from '$features/recipes/create/OtherWaysIn.svelte';
  import ResumeDraftLink from '$features/recipes/create/ResumeDraftLink.svelte';
  import TitleStartForm from '$features/recipes/create/TitleStartForm.svelte';
  import { findUnfinishedDraft } from '$features/recipes/create/unfinishedDraft';
  import { useNewRecipe } from '$features/recipes/create/useNewRecipe.svelte';
  import type { LastDraft } from '$features/recipes/editor/lastDraft';
  import PasteImport from '$features/recipes/editor/PasteImport.svelte';
  import { m } from '$shell/i18n';
  import Page from '$shell/Page.svelte';
  import PageHeader from '$shell/PageHeader.svelte';
  import { preferences } from '$shell/preferences.svelte';

  /**
   * Starting a recipe asks for one thing.
   *
   * A recipe with just a title is valid and saveable — it is the placeholder
   * for "I want to write this down later". The forty-field form is the reason
   * most recipes never get written down at all.
   *
   * So the page has one rank and then another, and never three things of equal
   * weight. The title field is the page: set in the editorial face at the size
   * it will be read at, in the one panel on the screen, with the only accented
   * button under it. Pasting a block of text and bringing a whole library over
   * are the other two ways in, and they sit below as two quiet doors — a door
   * being the honest shape for them, since both lead somewhere rather than
   * happening here.
   */
  let title = $state('');

  /** Whether the pasting box has been opened, which takes over the page. */
  let pasting = $state(false);
  let describing = $state(false);

  let incomingUrl = $state('');
  let incomingText = $state('');
  let incomingPhotos = $state<File[]>([]);
  let sharedFailure = $state(false);

  const shareId = page.url.searchParams.get('share');
  let loadingShare = $state(!!shareId);

  /**
   * A recipe already started here, still nothing but its title.
   *
   * Set once, on mount: this page is about starting something, not about
   * watching a draft change underneath the form while it is open.
   */
  let continuing = $state<LastDraft | null>(null);

  const creating = useNewRecipe({ title: () => title, shareId });
  const { submission } = creating;

  /** Something shared from another app and kept until this page could take it. */
  async function receiveShare(id: string) {
    try {
      const shared = await recallSharedRecipe(id);

      if (!shared) {
        sharedFailure = true;

        return;
      }

      title = shared.title;
      incomingUrl = sharedAddress(shared.url, shared.text);
      incomingText = shared.text;
      incomingPhotos = shared.photos;
      pasting = true;
    } catch {
      sharedFailure = true;
    } finally {
      loadingShare = false;
    }
  }

  async function offerUnfinishedDraft() {
    const userId = session.user?.userId;
    const householdId = session.activeHouseholdId;

    if (userId && householdId) {
      continuing = await findUnfinishedDraft(userId, householdId);
    }
  }

  onMount(() => {
    if (shareId) {
      void receiveShare(shareId);

      return;
    }

    if (page.url.searchParams.has('shareError')) sharedFailure = true;
    void offerUnfinishedDraft();

    const shared = readIncomingShare(page.url.searchParams);

    if (shared.title && !title) {
      title = shared.title;
    }

    incomingText = shared.text;

    if (shared.url) {
      incomingUrl = shared.url;
    }

    pasting = shared.url !== '' || shared.text !== '';
  });

  function cancelPasting() {
    void creating.forgetShare();
    incomingPhotos = [];
    incomingText = '';
    incomingUrl = '';
    void goto(resolve('/(app)/recipes/new'), { replaceState: true });
  }

  function cancelDescribing() {
    describing = false;
    drafts.dismiss();
  }
</script>

<svelte:head><title>{m['editor.new']()}</title></svelte:head>

<Page width="reading">
  <PageHeader
    title={pasting
      ? m['import.intake.title']()
      : continuing
        ? m['editor.newAnother']()
        : m['editor.new']()}
    subtitle={pasting ? undefined : m['editor.titleHint']()}
  />

  <div class="stack">
    {#if sharedFailure}<p role="alert">{m['import.share.failed']()}</p>{/if}
    {#if loadingShare}<p role="status">{m['import.share.loading']()}</p>{/if}
    {#if continuing && !pasting}
      <ResumeDraftLink draft={continuing} />
    {/if}

    {#if !pasting && !loadingShare}
      <TitleStartForm
        bind:title
        failure={submission.failure}
        loading={submission.showingProgress}
        onsubmit={() => creating.start(title.trim(), null)}
      />
    {:else}
      <FormFailure failure={submission.failure} />
    {/if}

    {#if session.activeHouseholdId && !loadingShare}
      {#if pasting}
        <PasteImport
          bind:open={pasting}
          householdId={session.activeHouseholdId}
          busy={submission.inFlight}
          saveError={submission.failure}
          initialUrl={incomingUrl}
          initialText={incomingText}
          initialPhotos={incomingPhotos}
          onimport={(parsed) => creating.start(parsed.title || title.trim(), parsed)}
          ondraft={(written, sourceUrl) => creating.startFromDraft(written, sourceUrl)}
          onqueued={creating.forgetShare}
          oncancel={cancelPasting}
        />
      {:else if describing}
        <IdeaDraft
          householdId={session.activeHouseholdId}
          language={preferences.locale}
          onwritten={(written) => void creating.startFromDraft(written)}
          oncancel={cancelDescribing}
        />
      {:else}
        <OtherWaysIn onpaste={() => (pasting = true)} ondescribe={() => (describing = true)} />
      {/if}
    {/if}
  </div>
</Page>

<style>
  .stack {
    display: flex;
    flex-direction: column;
    gap: var(--layout-section-gap);
    min-width: 0;
  }
</style>
