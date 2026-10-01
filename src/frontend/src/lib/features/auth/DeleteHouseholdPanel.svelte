<script lang="ts">
  import { goto } from '$app/navigation';
  import { resolve } from '$app/paths';
  import type { AppError } from '$api';
  import { Button } from '$ds';
  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';
  import { toaster } from '$shell/toaster.svelte';

  import DeleteHouseholdDialog from './DeleteHouseholdDialog.svelte';
  import { deleteHousehold, restoreHousehold } from './households.svelte';
  import { session } from './session.svelte';

  /**
   * Deleting the household being looked at. Owners only.
   *
   * Absent rather than disabled for a plain member, like everything else here
   * somebody cannot do. That is a courtesy, not the guard: the server refuses
   * a member whatever the page shows, and a refusal that arrives anyway — an
   * owner demoted on another device a minute ago — is said in the dialog and
   * the session read again, so the button goes too.
   *
   * Afterwards the app goes to the start, which lands in the next household
   * or, if this was the last one, on the screen that offers a way back.
   */
  interface Props {
    householdId: string;
    name: string;
  }

  let { householdId, name }: Props = $props();

  let confirming = $state(false);
  let deleting = $state(false);
  let failure = $state<AppError | null>(null);

  async function remove() {
    if (deleting) {
      return;
    }

    // Taken now, not read later: once the session is read again the page hands
    // this panel the next household, and an Undo that restored that one — or
    // a message naming it — would be about the wrong kitchen.
    const id = householdId;
    const deletedName = name;

    deleting = true;
    failure = await deleteHousehold(id);
    deleting = false;

    if (failure) {
      // What the page believed about this person's role may be out of date.
      await session.refresh();

      return;
    }

    confirming = false;
    await session.refresh();

    toaster.show({
      message: () => m['household.delete.done']({ name: deletedName }),
      action: { label: () => m['trash.undo'](), run: () => void undo(id) }
    });

    await goto(resolve('/(app)'), { replaceState: true });
  }

  async function undo(id: string) {
    const refused = await restoreHousehold(id);

    if (refused) {
      toaster.show({ message: () => explain(refused), tone: 'danger' });

      return;
    }

    await session.refresh();
    session.selectHousehold(id);
    await goto(resolve('/(app)/me/household'));
  }

  function close() {
    confirming = false;
    failure = null;
  }
</script>

<div>
  <Button variant="danger" onclick={() => (confirming = true)}>
    {m['household.delete.action']()}
  </Button>
</div>

<DeleteHouseholdDialog
  open={confirming}
  {name}
  {deleting}
  error={failure}
  onconfirm={remove}
  onclose={close}
/>
