<script lang="ts">
  import {
    Button,
    Checkbox,
    Field,
    IconButton,
    RadioGroup,
    SearchField,
    Select,
    Stepper,
    Switch,
    TextArea,
    TextInput
  } from '$ds';

  /*
   * The design-system gallery. Development only: `import.meta.env.DEV` is
   * statically false in a production build, so the whole tree below is removed
   * by the bundler and never ships.
   *
   * This is the one file exempt from "no hard-coded user-visible text" — the
   * strings here are specimens, not product copy, and translating them would
   * make the gallery harder to read for the person changing a component.
   */
  let title = $state('Tomato soup');
  let notes = $state('');
  let servings = $state(4);
  let search = $state('');
  let unit = $state('metric');
  let course = $state('main');
  let vegetarian = $state(true);
  let openRegistration = $state(false);
</script>

<svelte:head><title>Design system</title></svelte:head>

{#if import.meta.env.DEV}
  <main class="gallery">
    <h1>Design system</h1>

    <section>
      <h2>Buttons</h2>
      <div class="row">
        <Button variant="primary">Primary</Button>
        <Button variant="secondary">Secondary</Button>
        <Button variant="ghost">Ghost</Button>
        <Button variant="danger">Danger</Button>
      </div>
      <div class="row">
        <Button size="sm">Small</Button>
        <Button size="md">Medium</Button>
        <Button size="lg">Large</Button>
      </div>
      <div class="row">
        <Button variant="primary" loading>Saving</Button>
        <Button disabled>Disabled</Button>
        <Button href="/">A link</Button>
        <IconButton label="Add">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M12 5v14M5 12h14" stroke-linecap="round" />
          </svg>
        </IconButton>
        <IconButton label="Favourite" bordered pressed>
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8">
            <path d="M12 20s-7-4.4-7-9.2A4 4 0 0 1 12 8a4 4 0 0 1 7 2.8C19 15.6 12 20 12 20Z" />
          </svg>
        </IconButton>
      </div>
    </section>

    <section>
      <h2>Inputs</h2>
      <div class="stack">
        <Field label="Title" hint="What you would call it out loud.">
          {#snippet children({ id, describedBy, invalid })}
            <TextInput {id} {describedBy} {invalid} bind:value={title} />
          {/snippet}
        </Field>

        <Field label="Title" error="Give it a name.">
          {#snippet children({ id, describedBy, invalid })}
            <TextInput {id} {describedBy} {invalid} value="" />
          {/snippet}
        </Field>

        <Field label="Notes" optionalText="optional">
          {#snippet children({ id, describedBy, invalid })}
            <TextArea
              {id}
              {describedBy}
              {invalid}
              bind:value={notes}
              placeholder="Anything worth remembering next time."
            />
          {/snippet}
        </Field>

        <Field label="Units">
          {#snippet children({ id, describedBy, invalid })}
            <Select
              {id}
              {describedBy}
              {invalid}
              bind:value={unit}
              options={[
                { value: 'metric', label: 'Metric' },
                { value: 'imperial', label: 'Imperial' }
              ]}
            />
          {/snippet}
        </Field>

        <Field label="Servings">
          {#snippet children({ id, describedBy })}
            <Stepper
              {id}
              {describedBy}
              label="Servings"
              decreaseLabel="One fewer serving"
              increaseLabel="One more serving"
              bind:value={servings}
            />
          {/snippet}
        </Field>

        <Field label="Course" group>
          {#snippet children({ describedBy })}
            <RadioGroup
              name="course"
              {describedBy}
              bind:value={course}
              options={[
                { value: 'starter', label: 'Starter' },
                { value: 'main', label: 'Main', description: 'The default for a new recipe.' },
                { value: 'dessert', label: 'Dessert' }
              ]}
            />
          {/snippet}
        </Field>

        <Checkbox bind:checked={vegetarian} label="Vegetarian" />

        <Switch
          bind:checked={openRegistration}
          label="Open registration"
          description="Anyone with the address can create an account."
        />

        <SearchField
          id="gallery-search"
          label="Search recipes"
          clearLabel="Clear search"
          placeholder="Search recipes"
          bind:value={search}
        />
      </div>
    </section>
  </main>
{/if}

<style>
  .gallery {
    max-width: var(--measure);
    margin: 0 auto;
    padding: var(--space-8) var(--space-4) var(--space-24);
    display: flex;
    flex-direction: column;
    gap: var(--space-12);
  }

  section {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
  }

  .row {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-3);
  }

  .stack {
    display: flex;
    flex-direction: column;
    gap: var(--space-6);
    max-width: 26rem;
  }
</style>
