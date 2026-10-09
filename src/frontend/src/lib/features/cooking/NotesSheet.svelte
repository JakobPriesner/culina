<script lang="ts">
  import { Sheet } from '$ds';
  import { m } from '$shell/i18n';
  import PersonalNotePanel from './PersonalNotePanel.svelte';

  /** Your own note on the recipe, within reach of the cooking screen. */
  interface Props {
    open: boolean;
    recipeId: string;
    onclose: () => void;
  }

  let { open, recipeId, onclose }: Props = $props();
</script>

<Sheet {open} title={m['notes.title']()} closeLabel={m['picker.close']()} {onclose}>
  <!-- Keyed so a pending autosave is flushed for the recipe it was typed on, not the next one. -->
  {#key recipeId}
    <PersonalNotePanel {recipeId} variant="cook" />
  {/key}
</Sheet>
