import { formatDate } from '$shell/i18n';

/** "Mealie · 2026-10-27", the name an import gives its shelf (ImportFromSourceCommand.ShelfName). */
const importDated = /^(.*) · (\d{4})-(\d{2})-(\d{2})$/;

/**
 * A cookbook's name as the reader sees it. The server stores an import's date as a numeric ISO day, the same for the
 * whole household in any language; on screen it reads in the reader's own ("27. Okt. 2026"), like every other date.
 * Editing still shows the stored name.
 */
export function shelfTitle(name: string): string {
  const dated = importDated.exec(name);

  if (!dated) {
    return name;
  }

  const [, label, year, month, day] = dated;
  const date = new Date(Date.UTC(Number(year), Number(month) - 1, Number(day)));

  if (Number.isNaN(date.getTime())) {
    return name;
  }

  return `${label} · ${formatDate(date, { day: 'numeric', month: 'short', year: 'numeric', timeZone: 'UTC' })}`;
}
