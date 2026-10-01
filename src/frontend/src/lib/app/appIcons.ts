/**
 * The app icons a person can pick, and where each one's files are.
 *
 * Read by the settings screen and by `build-tools/generateIcons.ts`, which
 * draws every file named here, so a new icon is one id below and one drawing
 * there. Nothing here imports anything: the build tool runs this file straight
 * under Node.
 *
 * The default lives at the root of `static/` under the names browsers and
 * phones ask for without being told (`/favicon.ico`, `/apple-touch-icon.png`)
 * and the names an existing install already holds. The others sit in a folder
 * of their own.
 */
export const appIcons = ['cocotte', 'basil', 'ink', 'paper', 'saffron'] as const;

export type AppIcon = (typeof appIcons)[number];

export const defaultAppIcon: AppIcon = 'cocotte';

/** Which icon this device shows. Per device, because every install has its own. */
export const appIconStorageKey = 'culina.appIcon';

export function isAppIcon(value: unknown): value is AppIcon {
  return appIcons.includes(value as AppIcon);
}

/** A stored value is editable by hand and outlives releases that remove an icon. */
export function parseAppIcon(raw: string | null): AppIcon {
  return isAppIcon(raw) ? raw : defaultAppIcon;
}

/** The folder an icon's files are in, as a URL path with no trailing slash. */
export function appIconFolder(icon: AppIcon): string {
  return icon === defaultAppIcon ? '' : `/icons/${icon}`;
}

/** The files the document links to, which are the ones that change with the icon. */
export function appIconLinks(icon: AppIcon) {
  const folder = appIconFolder(icon);

  return {
    manifest: `${folder}/manifest.webmanifest`,
    svg: `${folder}/icon.svg`,
    ico: `${folder}/favicon.ico`,
    appleTouch: `${folder}/apple-touch-icon.png`
  };
}
