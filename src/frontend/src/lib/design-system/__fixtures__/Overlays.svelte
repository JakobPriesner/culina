<script lang="ts">
  import Button from '../actions/Button.svelte';
  import Card from '../containment/Card.svelte';
  import Disclosure from '../containment/Disclosure.svelte';
  import Divider from '../containment/Divider.svelte';
  import Tabs from '../containment/Tabs.svelte';
  import Avatar from '../display/Avatar.svelte';
  import Badge from '../display/Badge.svelte';
  import ProgressBar from '../feedback/ProgressBar.svelte';
  import Toaster from '../feedback/Toaster.svelte';
  import Modal from '../overlay/Modal.svelte';
  import Popover from '../overlay/Popover.svelte';
  import Sheet from '../overlay/Sheet.svelte';
  import TextInput from '../inputs/TextInput.svelte';
  import { toaster } from '$shell/toaster.svelte';

  /** Every overlay and container, wired up enough to be driven by a test. */
  let modalOpen = $state(false);
  let sheetOpen = $state(false);
  let tab = $state('ingredients');
  let renamed = $state('Tomato soup');
  let undone = $state(false);
</script>

<div class="row">
  <Button onclick={() => (modalOpen = true)}>Open modal</Button>
  <Button onclick={() => (sheetOpen = true)}>Open sheet</Button>
  <Button onclick={() => (undone = false)}>Reset</Button>
  <Button
    onclick={() =>
      toaster.show({
        message: 'Recipe deleted',
        action: { label: 'Undo', run: () => (undone = true) }
      })}
  >
    Delete with undo
  </Button>

  <Popover>
    {#snippet trigger({ popovertarget })}
      <Button {popovertarget}>More</Button>
    {/snippet}
    <Button variant="ghost">Duplicate</Button>
  </Popover>
</div>

<p data-testid="undo-state">{undone ? 'restored' : 'deleted'}</p>
<p data-testid="title">{renamed}</p>

<div class="row">
  <Badge>Vegetarian</Badge>
  <Badge tone="accent">30 min</Badge>
  <Badge tone="danger">Missing 3</Badge>
  <Avatar name="Jakob Priesner" />
</div>

<Divider />

<Card>
  <h3>A distinct entity</h3>
  <p>Not the default wrapper for a block of content.</p>
</Card>

<ProgressBar value={3} max={8} label="Cooking progress" valueText="Step 3 of 8" />

<Disclosure summary="Nutrition">
  <p>Roughly 320 kcal a serving.</p>
</Disclosure>

<Tabs
  bind:selected={tab}
  label="Recipe sections"
  tabs={[
    { id: 'ingredients', label: 'Ingredients' },
    { id: 'steps', label: 'Steps' },
    { id: 'notes', label: 'Notes' }
  ]}
>
  {#snippet children(selected)}
    <p data-testid="tab-panel">{selected}</p>
  {/snippet}
</Tabs>

<Modal bind:open={modalOpen} title="Rename recipe" closeLabel="Close">
  <TextInput id="rename" bind:value={renamed} />

  {#snippet footer()}
    <Button variant="primary" onclick={() => (modalOpen = false)}>Save</Button>
  {/snippet}
</Modal>

<Sheet bind:open={sheetOpen} title="Scale to what you have" closeLabel="Close">
  <p>A sheet on a phone, a dialog on a desktop.</p>
</Sheet>

<Toaster label="Notifications" dismissLabel="Dismiss" />

<style>
  .row {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-3);
    margin-block: var(--space-4);
  }
</style>
