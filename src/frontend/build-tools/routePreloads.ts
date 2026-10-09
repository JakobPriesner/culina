import { readFile, writeFile } from 'node:fs/promises';

/**
 * Tells the shell which modules a cold visit to each route will need, so the browser fetches them beside the entry files
 * instead of after them: SvelteKit finds a route's modules only once its own entry has run, which costs a whole download round.
 * The shell is one file for every address, so it carries a small table and a script that picks the row for `location.pathname`.
 */

/** What the client manifest says about one file: its URL and the chunks it imports statically. */
export interface ManifestEntry {
  readonly file: string;
  readonly imports?: readonly string[];
  readonly css?: readonly string[];
}

export type Manifest = Readonly<Record<string, ManifestEntry>>;

/** SvelteKit's route table: route id to the leaf node and its layout nodes. The root layout (node 0) is implied. */
export type Dictionary = Readonly<Record<string, readonly [number, (readonly number[])?]>>;

export interface Plan {
  /** Files to fetch, relative to `/_app/immutable/`. */
  readonly files: readonly string[];
  /** Per node id, the files it adds beyond its layouts and the entry files, as indices into `files`. */
  readonly nodes: Readonly<Record<number, readonly number[]>>;
  readonly routes: readonly Route[];
}

export interface Route {
  /** The address, `*` standing for one parameter segment. */
  readonly pattern: string;
  /** Node ids, outermost first. */
  readonly chain: readonly number[];
  /** Index into `earlyRequests`, or null when the route needs no data before its code runs. */
  readonly early: number | null;
}

/** What the app asks for first. Preloaded only when the request is certain to match (see `script`); the strings are the paths the client calls. */
export const earlyRequests: readonly (readonly string[])[] = [
  ['/api/v1/users/me', '/api/v1/users/me/settings'],
  ['/api/v1/setup']
];

const immutable = '_app/immutable/';
const nodeKey = (id: number) => `.svelte-kit/generated/client-optimized/nodes/${id}.js`;

/** Reads the route table out of the file SvelteKit generates; one line per route, which a build would have to change shape to break. */
export function parseDictionary(source: string): Dictionary {
  const table: Record<string, [number, number[]?]> = {};

  for (const [, id, leaf, layouts] of source.matchAll(
    /"(\/[^"]*)":\s*\[(\d+)(?:,\[([\d,]*)\])?\]/g
  )) {
    table[id!] = [Number(leaf), layouts ? layouts.split(',').map(Number) : []];
  }

  return table;
}

/** The address of a route id with each parameter written as a `*`; anything but plain parameters is refused rather than matched wrongly. */
export function patternOf(routeId: string): string {
  const path = routeId.replace(/\/\([^/]+\)/g, '');

  if (/\[\[|\[\.\.\./.test(path)) {
    throw new Error(
      `Route ${routeId}: optional and rest parameters are not supported by the preload table.`
    );
  }

  return path.replace(/\[[^\]/]+\]/g, '*') || '/';
}

/** Which data the route's code asks for first: the session behind sign-in, the setup stage on the sign-in pages. */
function earlyFor(routeId: string): number | null {
  if (routeId.startsWith('/(app)')) {
    return 0;
  }

  return routeId.startsWith('/(auth)') ? 1 : null;
}

/** The manifest keys a key statically imports, itself included. */
const closure = (manifest: Manifest, key: string, seen = new Set<string>()): Set<string> => {
  const entry = manifest[key];

  if (entry && !seen.has(key)) {
    seen.add(key);
    entry.imports?.forEach((child) => closure(manifest, child, seen));
  }

  return seen;
};

/** Every file, scripts and stylesheets, a cold visit to each route loads through its nodes' static imports; the entry files are not included. */
export function routeFiles(manifest: Manifest, dictionary: Dictionary): Record<string, string[]> {
  return Object.fromEntries(
    Object.entries(dictionary).map(([routeId, [leaf, layouts = []]]) => {
      const keys = [0, ...layouts, leaf].flatMap((node) => [...closure(manifest, nodeKey(node))]);

      return [
        routeId,
        [...new Set(keys.flatMap((key) => [manifest[key]!.file, ...(manifest[key]!.css ?? [])]))]
      ];
    })
  );
}

export function plan(
  manifest: Manifest,
  dictionary: Dictionary,
  shellFiles: ReadonlySet<string>
): Plan {
  const closureOf = (key: string) => closure(manifest, key);

  const fileOf = (key: string) => manifest[key]!.file;
  const withoutShell = (keys: Iterable<string>) =>
    [...keys].filter((key) => !shellFiles.has(fileOf(key)));

  const files: string[] = [];
  const index = (file: string) => {
    const name = file.slice(immutable.length);
    const at = files.indexOf(name);

    return at >= 0 ? at : files.push(name) - 1;
  };

  const nodes: Record<number, number[]> = {};
  const routes: Route[] = [];

  for (const [routeId, [leaf, layouts = []]] of Object.entries(dictionary)) {
    const chain = [0, ...layouts, leaf];
    const covered = new Set<string>();

    // A node's layouts are fixed by where its file lives, so its row can leave out whatever they already bring.
    for (const node of chain) {
      const closure = closureOf(nodeKey(node));

      if (!(node in nodes)) {
        nodes[node] = withoutShell(closure)
          .filter((key) => !covered.has(key))
          .map((key) => index(fileOf(key)));
      }

      closure.forEach((key) => covered.add(key));
    }

    routes.push({ pattern: patternOf(routeId), chain, early: earlyFor(routeId) });
  }

  return { files, nodes, routes };
}

/** Two base-36 digits per file, so a row is one short string. */
const encode = (indices: readonly number[]) =>
  indices.map((at) => at.toString(36).padStart(2, '0')).join('');

/**
 * The inline script. It adds `modulepreload` links for the matching route, and, for a visit nothing has cached or controlled, `preload`
 * links for the first requests the app will make. Both skip an address with no row. The data preload needs a request that matches
 * exactly: the app sends its credentials, hence `use-credentials`; a service worker answers from its own cache, so a controlled page
 * is left alone (the preload would go unused and warn); and the client's ETag store is memory only, so a fresh page never adds `If-None-Match`.
 */
export function script(source: Plan): string {
  const table = {
    f: source.files,
    n: Array.from({ length: Math.max(...Object.keys(source.nodes).map(Number)) + 1 }, (_, id) =>
      encode(source.nodes[id] ?? [])
    ),
    r: source.routes.map((route) => [route.pattern, route.chain, route.early ?? undefined]),
    e: earlyRequests
  };

  return `(function(t){var h=document.head,p=location.pathname,r=t.r.find(function(r){return new RegExp('^'+r[0].replace(/\\*/g,'[^/]+')+'$').test(p)});if(!r)return;function a(rel,href){var l=document.createElement('link');l.rel=rel;l.href=href;if(rel=='preload'){l.as='fetch';l.crossOrigin='use-credentials'}h.appendChild(l)}r[1].forEach(function(n){(t.n[n].match(/../g)||[]).forEach(function(i){a('modulepreload','/${immutable}'+t.f[parseInt(i,36)])})});if(r[2]!=null&&!(navigator.serviceWorker&&navigator.serviceWorker.controller))t.e[r[2]].forEach(function(u){a('preload',u)})})(${JSON.stringify(table)})`;
}

/** The modules the shell already preloads, which a route's row never repeats. */
export function preloadedBy(html: string): Set<string> {
  return new Set(
    [...html.matchAll(/<link href="\/(_app\/immutable\/[^"]+\.js)" rel="modulepreload"/g)].map(
      (match) => match[1]!
    )
  );
}

/** What a build leaves for the client: the manifest and the route table. */
export async function readKit(
  kitDir = '.svelte-kit'
): Promise<{ manifest: Manifest; dictionary: Dictionary }> {
  return {
    manifest: JSON.parse(
      await readFile(`${kitDir}/output/client/.vite/manifest.json`, 'utf8')
    ) as Manifest,
    dictionary: parseDictionary(
      await readFile(`${kitDir}/generated/client-optimized/app.js`, 'utf8')
    )
  };
}

/** Adds the script to the finished shell, in place; the placeholder nonce is the one the host swaps per response. */
export async function addPreloads(
  buildDir: string,
  kitDir = '.svelte-kit'
): Promise<{ added: number }> {
  const shell = `${buildDir}/index.html`;
  const html = await readFile(shell, 'utf8');
  const { manifest, dictionary } = await readKit(kitDir);
  const tag = `<script nonce="__CULINA_NONCE__">${script(plan(manifest, dictionary, preloadedBy(html)))}</script>`;

  await writeFile(
    shell,
    html.replace('</head>', () => `${tag}\n  </head>`)
  );

  return { added: tag.length };
}
