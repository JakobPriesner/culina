<script lang="ts">
  import FourStates from '$ds/__fixtures__/FourStates.svelte';
  import Overlays from '$ds/__fixtures__/Overlays.svelte';
  import OlliGallery from './OlliGallery.svelte';
  import DraftProgress from '$features/assistance/DraftProgress.svelte';
  import Olli from '$shell/olli/Olli.svelte';
  import { olliSetting } from '$shell/olli/setting.svelte';
  import {
    Button,
    type ButtonVariant,
    Checkbox,
    Field,
    GenerationAura,
    GenerationStatus,
    IconButton,
    ImageField,
    RadioGroup,
    SearchField,
    Select,
    Stepper,
    Switch,
    TextArea,
    TextInput
  } from '$ds';

  /*
   * The design-system gallery: dev only (the route imports it behind a condition folded to false,
   * see ./+page.svelte). Exempt from "no hard-coded text": the strings are specimens.
   */
  let title = $state('Tomato soup');
  let notes = $state('');
  let servings = $state(4);
  let search = $state('');
  let unit = $state('metric');
  let course = $state('main');
  let vegetarian = $state(true);
  let openRegistration = $state(false);

  /* Both picture-field states side by side; nothing is uploaded, the file stays in the page as an object URL. */
  let chosen = $state<string | undefined>();

  /** A drawn stand-in photograph: the gallery must render with nothing behind it. */
  const specimenPhoto =
    'data:image/svg+xml;utf8,' +
    encodeURIComponent(
      `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 300">
        <defs><linearGradient id="g" x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stop-color="%23c9a227"/><stop offset="1" stop-color="%235c7a3f"/>
        </linearGradient></defs>
        <rect width="400" height="300" fill="url(%23g)"/>
        <circle cx="200" cy="150" r="70" fill="rgba(255,255,255,0.35)"/>
      </svg>`.replace(/\s+/g, ' ')
    );

  let drawing = $state(true);
</script>

<svelte:head><title>Design system</title></svelte:head>

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

      <ImageField
        label="Photo"
        hint="One picture of the finished dish. It is what you will recognise it by."
        chooseLabel="Choose a photo"
        replaceLabel="Replace the photo"
        removeLabel="Remove the photo"
        dropLabel="Drop the photo here"
        src={chosen}
        onpick={(file) => (chosen = URL.createObjectURL(file))}
        onremove={() => (chosen = undefined)}
      />

      <!-- Filled state: the actions lie on the picture. -->
      <ImageField
        label="Photo, with one in it"
        hint="Unused here."
        chooseLabel="Choose a photo"
        replaceLabel="Replace"
        removeLabel="Remove"
        dropLabel="Drop the photo here"
        src={specimenPhoto}
        onpick={() => {}}
        onremove={() => {}}
        extraAction={drawOne}
      />

      {#snippet drawOne(variant: ButtonVariant)}
        <Button {variant} size="sm">Draw one</Button>
      {/snippet}

      <!-- A picture being made: no progress, since a provider reports none until finished. -->
      <ImageField
        label="Photo, being drawn"
        hint="One picture of the finished dish."
        chooseLabel="Choose a photo"
        replaceLabel="Replace"
        removeLabel="Remove"
        dropLabel="Drop the photo here"
        generating={drawing}
        generatingLabel="Drawing…"
        generatingArt={drawingOlli}
        animateGeneration={olliSetting.animated}
        onpick={() => {}}
        onremove={() => {}}
      />

      {#snippet drawingOlli()}
        <Olli pose="drawing" size="md" working={drawing} />
      {/snippet}

      <Checkbox bind:checked={drawing} label="Keep drawing" />

      <SearchField
        id="gallery-search"
        label="Search recipes"
        clearLabel="Clear search"
        placeholder="Search recipes"
        bind:value={search}
      />
    </div>
  </section>

  <section>
    <h2>Overlays and containers</h2>
    <Overlays />
  </section>

  <section>
    <h2>Olli</h2>
    <OlliGallery />
  </section>

  <section id="ai-motion">
    <h2>AI recipe work</h2>
    <div class="generated">
      <GenerationAura />
      <DraftProgress label="Refining your recipe…" arriving={true} />
      <p>A clear work scene, with readable status and optional motion.</p>
    </div>
  </section>

  <section>
    <h2>States</h2>
    <div class="stack">
      <FourStates state="loading" />
      <FourStates state="empty" />
      <FourStates state="filtered" />
      <FourStates state="error" />
      <FourStates state="loaded" refreshing />

      <!-- Something the assistant is writing: glow round where the work lands, status line inside. -->
      <div class="generated">
        <GenerationAura active={drawing} />
        <GenerationStatus label={drawing ? 'Writing…' : 'Written'} />
        <p>Aubergine and feta bake, with whatever herbs are left.</p>
      </div>
    </div>
  </section>
</main>

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

  .generated {
    position: relative;
    isolation: isolate;
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    padding: var(--space-4);
    border: 1px solid var(--border);
    border-radius: var(--radius-lg);
    background: var(--surface-sunken);
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
