import { startNativeImages } from './nativeImages';

let started = false;

/** What the installed app sets up once, before its first route: where photos come from. Never loaded by the website. */
export async function startNativeShell(): Promise<void> {
  if (started) {
    return;
  }

  started = true;
  startNativeImages();
}
