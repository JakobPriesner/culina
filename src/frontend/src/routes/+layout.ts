// Culina is a static SPA: there is no Node server in production, so nothing is
// rendered or prerendered on the server. Everything the client can see is
// public by definition, and no secret can leak into the bundle.
export const ssr = false;
export const prerender = false;
export const trailingSlash = 'never';
