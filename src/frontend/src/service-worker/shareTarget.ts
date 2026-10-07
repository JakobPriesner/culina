import { keepSharedRecipe } from '../lib/features/import/sharedRecipe';

const shareTargetPath = '/recipes/import';

/** The web share target: another app sharing a recipe to Culina posts it here. */
export const isShareTarget = (request: Request, url: URL, origin: string): boolean =>
  request.method === 'POST' && url.origin === origin && url.pathname === shareTargetPath;

/** Keeps what was shared and sends the person on to review it. */
export async function shareTargetResponse(request: Request, url: URL): Promise<Response> {
  try {
    const id = await keepSharedRecipe(await request.formData());

    return Response.redirect(new URL(`/recipes/new?share=${id}`, url.origin), 303);
  } catch {
    return Response.redirect(new URL('/recipes/new?shareError=1', url.origin), 303);
  }
}
