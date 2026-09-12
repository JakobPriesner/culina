<script lang="ts">
  import { Button } from '$ds';

  import { m } from '$shell/i18n';
  import type { Submission } from './submission.svelte';

  /**
   * The button at the bottom of an auth form.
   *
   * Disabled while a request is in flight, and while a rate limit is counting
   * down — and in that case it says *when*, because "try again later" is advice
   * nobody can act on. It is never disabled because a field looks wrong:
   * guessing at validity while someone is still typing hides the button exactly
   * when they reach for it.
   */
  interface Props {
    label: string;
    submission: Submission;
  }

  let { label, submission }: Props = $props();

  const secondsLeft = $derived(submission.retryIn);
</script>

<Button
  type="submit"
  variant="primary"
  size="lg"
  full
  disabled={secondsLeft !== null}
  loading={submission.showingProgress}
>
  {secondsLeft === null ? label : m['auth.retryIn']({ seconds: secondsLeft })}
</Button>
