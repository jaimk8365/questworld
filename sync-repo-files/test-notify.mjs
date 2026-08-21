import { execFileSync, spawnSync } from "node:child_process";
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

const missingHealth = join(dir, "missing.json");
const missing = spawnSync(process.execPath, ["notify.mjs", "--check-config", "--health", missingHealth], { cwd: here });
const missingResult = JSON.parse(readFileSync(missingHealth, "utf8"));
if (missing.status === 0 || missingResult.status !== "missing-secrets") {
  throw new Error("missing notification keys did not fail safely");
}

const configHealth = join(dir, "config.json");
execFileSync(process.execPath, ["notify.mjs", "--check-config", "--health", configHealth], {
  cwd: here,
  env: { ...process.env, VAPID_PUBLIC_KEY: "public-key-long-enough-for-a-real-vapid-key-123456789", VAPID_PRIVATE_KEY: "private-key-long-enough-123456789" },
});
const configResult = JSON.parse(readFileSync(configHealth, "utf8"));
if (configResult.status !== "config-ok") throw new Error("valid notification keys were not accepted");
console.log("notification secret guard passed");
