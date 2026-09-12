<script lang="ts">
  import { onMount } from 'svelte';

  import type { AppError } from '$api';

  import FormField from '../FormField.svelte';
  import { createSubmission } from '../submission.svelte';

  /** One field wired to a submission that can be made to fail on demand. */
  interface Props {
    failure?: AppError | null;
  }

  let { failure = null }: Props = $props();

  let value = $state('');

  const submission = createSubmission();

  // Once, on mount. An $effect would read the submission's own state through
  // `run` and re-run itself forever.
  onMount(() => {
    if (failure) {
      void submission.run(async () => failure);
    }
  });
</script>

<FormField name="email" label="Email address" type="email" bind:value {submission} />
