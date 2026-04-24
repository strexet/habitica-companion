import { copyFile, mkdir } from "node:fs/promises";
import { dirname, resolve } from "node:path";

const sourcePath = resolve(process.cwd(), "node_modules/dexie/dist/modern/dexie.mjs");
const destinationPath = resolve(process.cwd(), "wwwroot/vendor/dexie.mjs");

await mkdir(dirname(destinationPath), { recursive: true });
await copyFile(sourcePath, destinationPath);
