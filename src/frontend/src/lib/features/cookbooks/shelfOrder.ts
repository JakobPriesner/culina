import { http, request, type AppError } from '$api';

/** One line of a shelf being arranged: enough to say which recipe it is. */
export interface ShelfLine {
  readonly id: string;
  readonly title: string;
}

/** The server's page size cap: a shelf is read whole, a hundred at a time. */
const pageSize = 100;

/** Every recipe on a chosen shelf, in its order; arranging needs all of it, not the first page the grid shows. */
export async function readShelfOrder(
  householdId: string,
  cookbookId: string
): Promise<{ ok: true; lines: ShelfLine[] } | { ok: false; error: AppError }> {
  const lines: ShelfLine[] = [];
  let cursor: string | undefined;

  do {
    const result = await request(() =>
      http.GET('/api/v1/recipes', {
        params: {
          query: { householdId, cookbookId, sort: 'cookbookOrder', limit: pageSize, cursor }
        }
      })
    );

    if (!result.ok) {
      return { ok: false, error: result.error };
    }

    lines.push(...result.value.items.map((item) => ({ id: item.recipeId, title: item.title })));
    cursor = result.value.nextCursor ?? undefined;
  } while (cursor);

  return { ok: true, lines };
}

/** The list with one line moved by one place, or the same list when it is already at that end. */
export function moveLine<T>(lines: readonly T[], index: number, by: -1 | 1): readonly T[] {
  const to = index + by;

  if (index < 0 || index >= lines.length || to < 0 || to >= lines.length) {
    return lines;
  }

  const moved = [...lines];

  [moved[index], moved[to]] = [moved[to]!, moved[index]!];

  return moved;
}
