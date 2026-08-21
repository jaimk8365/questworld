import { execFileSync } from "node:child_process";
import { mkdtempSync, readFileSync } from "node:fs";
import { join } from "node:path";
import { tmpdir } from "node:os";
import { fileURLToPath } from "node:url";

const here = fileURLToPath(new URL(".", import.meta.url));
const dir = mkdtempSync(join(tmpdir(), "questworld-notify-"));
const health = join(dir, "health.json");
execFileSync(process.execPath, ["notify.mjs", "--dry", "test-old.json", "test-new.json", "--health", health], { cwd: here, stdio: "inherit" });
const result = JSON.parse(readFileSync(health, "utf8"));
if (result.status !== "dry-run" || result.events !== 1 || result.sent !== 0 || result.failed !== 0) {
  throw new Error(`unexpected health result: ${JSON.stringify(result)}`);
}
console.log("notification health record passed");
