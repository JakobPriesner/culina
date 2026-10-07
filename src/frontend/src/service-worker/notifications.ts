import { base } from '$service-worker';
import { applyTimerAction, type TimerNotice } from '../lib/features/cooking/timerState';
import { shellDocument, worker } from './scope';

/** A finished recipe import, as the server names it and as a notification keeps it. */
const intakePath = /^\/recipes\/imports\/[0-9a-f-]{36}$/;

type IntakeNotice = { type: 'culina:intake'; url: string };

/** Shows a finished import, and only ever one that points at an import. */
export function handlePush(event: PushEvent): void {
  if (!event.data) return;
  try {
    const notice = event.data.json() as {
      title?: string;
      body?: string;
      url?: string;
      tag?: string;
    };
    if (typeof notice.url !== 'string' || !intakePath.test(notice.url)) return;
    event.waitUntil(
      worker.registration.showNotification(notice.title ?? 'Culina', {
        body: notice.body,
        tag: notice.tag,
        icon: `${base}/icon-192.png`,
        data: { type: 'culina:intake', url: `${base}${notice.url}` } satisfies IntakeNotice
      })
    );
  } catch {
    /* Malformed pushes do not navigate or expose arbitrary content. */
  }
}

/** Brings the cooking app back to the front when a notification is clicked. */
export function handleNotificationClick(event: NotificationEvent): void {
  event.notification.close();
  const notice = event.notification.data as TimerNotice | IntakeNotice | undefined;
  event.waitUntil(
    openFrom(notice, event.action).catch(() => {
      /* Optional platform facilities must not reject the event. */
    })
  );
}

async function openFrom(notice: TimerNotice | IntakeNotice | undefined, action: string) {
  if (notice?.type === 'culina:timer' && action) {
    await actOnTimer(notice, action);
    return;
  }

  const href = safeHref(notice);
  const clients = await worker.clients.matchAll({ type: 'window', includeUncontrolled: true });
  const client = clients.find((page) => page.url === href) ?? clients[0];

  if (!client) {
    await worker.clients.openWindow(href);
    return;
  }

  if (client.url !== href) await client.navigate(href);
  await client.focus();
}

/** A timer button: changes the timer in the worker and tells the open pages, without foregrounding one. */
async function actOnTimer(notice: TimerNotice, action: string) {
  if (!(await applyTimerAction(notice, action))) return;

  const clients = await worker.clients.matchAll({ type: 'window', includeUncontrolled: true });

  for (const client of clients)
    client.postMessage({ type: 'culina:timers-changed', sessionId: notice.sessionId });
}

/** Only same-origin cook routes from our notification payload can navigate. */
function safeHref(notice: TimerNotice | IntakeNotice | undefined): string {
  const target = new URL(notice?.url ?? shellDocument, worker.location.origin);
  const safe =
    target.origin === worker.location.origin &&
    target.pathname.startsWith(`${base}/recipes/`) &&
    (target.pathname.endsWith('/cook') ||
      (notice?.type === 'culina:intake' && intakePath.test(target.pathname.slice(base.length))));

  return safe ? target.href : new URL(shellDocument, worker.location.origin).href;
}
