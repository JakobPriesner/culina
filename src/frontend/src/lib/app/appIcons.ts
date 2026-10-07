/**
 * The app icons a person can pick and where each one's files are; read by settings and by `build-tools/generateIcons.ts` (a new icon is one id here and one drawing there). Imports nothing: the build tool runs it under Node.
 * The default sits at the root of `static/` under the names browsers and existing installs ask for; the others have their own folder.
 */
export const appIcons = ['cocotte', 'basil', 'ink', 'paper', 'saffron'] as const;

export type AppIcon = (typeof appIcons)[number];

export const defaultAppIcon: AppIcon = 'cocotte';

/** Which icon this device shows; per device, as every install has its own. */
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
