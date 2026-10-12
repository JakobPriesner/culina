import { finishNativeShell } from './nativeShell.ts';

/** The last step of `pnpm build:native`. */
const buildDir = process.argv[2] ?? 'build-native';

await finishNativeShell(buildDir);

console.log(`native shell: ${buildDir}/index.html carries its own policy`);
