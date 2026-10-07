<script lang="ts">
  import { createSectionWatch } from '../sectionWatch.svelte';

  const sections = [{ id: 'basics' }, { id: 'ingredients' }, { id: 'steps' }];
  const watch = createSectionWatch(() => sections);
</script>

<p data-testid="current">{watch.current}</p>
<p data-testid="away">{watch.away}</p>
{#each sections as section (section.id)}
  <h2 id={section.id} tabindex="-1">{section.id}</h2>
  <button type="button" onclick={(event) => watch.jump(event, section.id)}>Jump {section.id}</button
  >
{/each}
