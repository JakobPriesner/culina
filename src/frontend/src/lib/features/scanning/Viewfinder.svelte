<script lang="ts">
  import { Button } from '$ds';
  import { m } from '$shell/i18n';

  import { nativeDetector, startScanner, type CameraFailure, type Scanner } from './scanner';

  /**
   * The rear camera with a frame to aim into, which reads barcodes until it is locked on one. It mounts while the
   * sheet is looking and goes away with the answer, so the camera is on only while it is wanted. Where the camera
   * cannot be used (denied, none, or a browser with no decoder) it says so in one line; typing the digits, which
   * the sheet always offers beside it, is the way on.
   */
  interface Props {
    ondetect: (code: string) => void;
  }

  let { ondetect }: Props = $props();

  type Status = 'starting' | 'live' | CameraFailure | 'unsupported';

  let video = $state<HTMLVideoElement>();
  let status = $state<Status>('starting');
  let scanner = $state<Scanner>();
  let lit = $state(false);

  $effect(() => {
    if (!video) {
      return;
    }

    const detect = nativeDetector();

    if (!detect) {
      status = 'unsupported';

      return;
    }

    let leaving = false;
    let locked = false;
    let started: Scanner | undefined;

    void startScanner(video, detect, (code) => {
      if (!locked) {
        locked = true;
        ondetect(code);
      }
    }).then((result) => {
      if (!result.ok) {
        status = result.reason;

        return;
      }

      if (leaving) {
        result.scanner.stop();

        return;
      }

      started = result.scanner;
      scanner = result.scanner;
      status = 'live';
    });

    return () => {
      leaving = true;
      started?.stop();
      scanner = undefined;
      lit = false;
    };
  });

  const words: Record<Exclude<Status, 'starting' | 'live'>, () => string> = {
    denied: () => m['scan.camera.denied'](),
    none: () => m['scan.camera.none'](),
    unsupported: () => m['scan.camera.unsupported']()
  };

  async function toggleLight() {
    if (!scanner) {
      return;
    }

    try {
      await scanner.setTorch(!lit);
      lit = !lit;
    } catch {
      // The light is a nicety; a camera that refuses it keeps scanning.
    }
  }
</script>

{#if status === 'denied' || status === 'none' || status === 'unsupported'}
  <p class="quiet" role="status">{words[status]()}</p>
{:else}
  <div class="finder">
    <video bind:this={video} playsinline muted aria-label={m['scan.camera.label']()}></video>
    <div class="frame" aria-hidden="true"></div>
    {#if status === 'starting'}
      <p class="starting" role="status">{m['scan.camera.starting']()}</p>
    {:else}
      <p class="aim">{m['scan.camera.aim']()}</p>
    {/if}
    {#if scanner?.hasTorch}
      <div class="light">
        <Button size="sm" variant="media" onclick={toggleLight}>
          {lit ? m['scan.light.off']() : m['scan.light.on']()}
        </Button>
      </div>
    {/if}
  </div>
{/if}

<style>
  .finder {
    position: relative;
    overflow: hidden;
    border-radius: var(--radius-lg);
    background: var(--surface-sunken);
    aspect-ratio: 16 / 9;
  }

  video {
    display: block;
    width: 100%;
    height: 100%;
    object-fit: cover;
  }

  .frame {
    position: absolute;
    inset: 22% 12%;
    border: 2px solid var(--border-on-media);
    border-radius: var(--radius-md);
  }

  .aim,
  .starting {
    position: absolute;
    inset: auto 0 var(--space-2);
    margin: 0;
    text-align: center;
    font-size: var(--text-sm);
    color: var(--text-on-media);
  }

  .light {
    position: absolute;
    top: var(--space-2);
    right: var(--space-2);
  }

  .quiet {
    margin: 0;
    color: var(--text-muted);
    font-size: var(--text-sm);
  }
</style>
