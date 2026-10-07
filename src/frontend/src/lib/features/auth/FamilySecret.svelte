<script lang="ts">
  import { m } from '$shell/i18n';

  /** A recipe card under a rubber stamp for link visitors; the card is made up since nothing behind the link (title, photo) may show to a signed-out visitor. */
</script>

<div class="card" aria-hidden="true">
  <div class="blurred">
    <span class="photo"></span>
    <span class="line title"></span>
    <span class="line"></span>
    <span class="line long"></span>
    <span class="line short"></span>
  </div>
  <p class="stamp">
    {m['auth.secret.stamp']()}
    <small>{m['auth.secret.stampNote']()}</small>
  </p>
</div>

<style>
  .card {
    position: relative;
    padding: var(--space-4);
    border: 1px solid var(--border);
    border-radius: var(--radius-md);
    background: var(--surface-raised);
    box-shadow: var(--shadow-card);
    transform: rotate(1.5deg);
  }

  .blurred {
    display: grid;
    gap: var(--space-2);
    filter: blur(3.5px);
    opacity: 0.75;
  }

  .photo {
    height: 4.5rem;
    border-radius: var(--radius-md);
    background: linear-gradient(135deg, var(--brand-accent), var(--accent));
  }

  .line {
    height: 0.6rem;
    border-radius: var(--radius-sm);
    background: var(--border-strong);
    opacity: 0.45;
  }

  .line.long {
    width: 80%;
  }

  .line.short {
    width: 60%;
  }

  .line.title {
    width: 70%;
    height: 1.1rem;
    opacity: 0.7;
  }

  .stamp {
    position: absolute;
    top: 48%;
    left: 50%;
    padding: var(--space-1) var(--space-3);
    border: 3px solid var(--text-danger);
    border-radius: var(--radius-sm);
    color: var(--text-danger);
    font-size: var(--text-lg);
    font-weight: var(--weight-semibold);
    letter-spacing: 0.12em;
    line-height: var(--leading-tight);
    text-align: center;
    text-transform: uppercase;
    white-space: nowrap;
    transform: translate(-50%, -50%) rotate(-12deg);
  }

  .stamp small {
    display: block;
    font-size: 0.55rem;
    letter-spacing: 0.2em;
  }

  @media (prefers-reduced-motion: no-preference) {
    .card {
      animation: thud 320ms ease-out 250ms;
    }

    .stamp {
      animation: slam 380ms cubic-bezier(0.2, 0.9, 0.3, 1.2) both;
    }
  }

  @keyframes slam {
    from {
      opacity: 0;
      transform: translate(-50%, -50%) rotate(-24deg) scale(2.6);
    }
    70% {
      opacity: 1;
    }
  }

  @keyframes thud {
    30% {
      transform: rotate(1.5deg) translateY(3px) scale(0.985);
    }
    60% {
      transform: rotate(1deg) translateY(-1px);
    }
  }
</style>
