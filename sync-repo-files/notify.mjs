// QuestWorld push notifier. Runs on every sync commit: diffs data.json
// against the previous commit, works out family events, and web-pushes
// each one to the devices subscribed for the relevant profile.
//
// Dry run (no sends, prints events):  node notify.mjs --dry old.json new.json
import { execSync } from "node:child_process";
import { readFileSync, existsSync } from "node:fs";

const dry = process.argv[2] === "--dry";

const load = (txt) => { try { return JSON.parse(txt); } catch { return null; } };

let oldD, newD;
if (dry) {
  oldD = load(readFileSync(process.argv[3], "utf8"));
  newD = load(readFileSync(process.argv[4], "utf8"));
} else {
  try { oldD = load(execSync("git show HEAD~1:data.json", { encoding: "utf8" })); }
  catch { oldD = null; }
  newD = load(readFileSync("data.json", "utf8"));
}

if (!newD) { console.log("no parseable new data — nothing to do"); process.exit(0); }
if (!oldD) { console.log("no previous data (first sync) — skipping to avoid spam"); process.exit(0); }

const PARENT = "u-parent";
const nameOf = (id) => newD.users?.find((u) => u.id === id)?.displayName ?? "Someone";
const questTitle = (id) =>
  newD.quests?.find((q) => q.id === id)?.title ??
  oldD.quests?.find((q) => q.id === id)?.title ?? "a quest";
const prizeTitle = (id) =>
  (newD.prizes ?? []).find((p) => p.id === id)?.title ??
  (oldD.prizes ?? []).find((p) => p.id === id)?.title ?? "a prize";

// ------------------------------------------------------------- events
const events = []; // { to: userId, title, body }
const key = (c) => `${c.questId}|${c.userId}|${c.periodKey}`;
const oldComps = new Map((oldD.completions ?? []).map((c) => [key(c), c.status]));

for (const c of newD.completions ?? []) {
  const prev = oldComps.get(key(c));
  if (c.status === "PendingApproval" && prev !== "PendingApproval" && prev !== "Completed") {
    events.push({
      to: PARENT,
      title: "Ready to check ✋",
      body: `${nameOf(c.userId)} finished “${questTitle(c.questId)}” — tap Approve when you've seen it.`,
    });
  }
  if (c.status === "Completed" && prev === "PendingApproval") {
    events.push({
      to: c.userId,
      title: "Quest approved! 🎉",
      body: `“${questTitle(c.questId)}” was approved — your rewards have landed!`,
    });
  }
  if (c.status === "Rejected" && prev === "PendingApproval") {
    events.push({
      to: c.userId,
      title: "Almost! 💪",
      body: `“${questTitle(c.questId)}” needs another go — check with the Quest Master.`,
    });
  }
}

const oldReds = new Map((oldD.redemptions ?? []).map((r) => [r.id, r]));
for (const r of newD.redemptions ?? []) {
  const prev = oldReds.get(r.id);
  const cancelled = r.cancelled === true;
  if (!prev && !r.fulfilled && !cancelled) {
    events.push({
      to: PARENT,
      title: "Prize claimed 🎁",
      body: `${nameOf(r.userId)} spent their coins on “${prizeTitle(r.prizeId)}”!`,
    });
  }
  if (prev && !prev.fulfilled && r.fulfilled) {
    events.push({
      to: r.userId,
      title: "Prize time! 🎁",
      body: `“${prizeTitle(r.prizeId)}” is yours — go collect it!`,
    });
  }
}

const oldQuestIds = new Set((oldD.quests ?? []).map((q) => q.id));
for (const q of newD.quests ?? []) {
  if (!oldQuestIds.has(q.id) && q.active) {
    for (const kid of q.assignedTo ?? []) {
      events.push({
        to: kid,
        title: "New quest! 🗺️",
        body: `“${q.title}” just appeared — ${q.reward.xp} XP up for grabs!`,
      });
    }
  }
}

console.log(`events: ${events.length}`);
for (const e of events) console.log(`  → [${e.to}] ${e.title} | ${e.body}`);
if (dry || events.length === 0) process.exit(0);

// -------------------------------------------------------------- send
const subsFile = "subs.json";
const subs = existsSync(subsFile) ? load(readFileSync(subsFile, "utf8")) ?? [] : [];
if (subs.length === 0) { console.log("no subscribed devices"); process.exit(0); }

const { default: webpush } = await import("web-push");
webpush.setVapidDetails(
  "mailto:jaimi.kyte@gmail.com",
  process.env.VAPID_PUBLIC_KEY,
  process.env.VAPID_PRIVATE_KEY
);

let sent = 0, failed = 0;
for (const event of events) {
  for (const entry of subs.filter((s) => s.userId === event.to)) {
    try {
      await webpush.sendNotification(
        entry.subscription,
        JSON.stringify({ title: event.title, body: event.body }),
        { TTL: 6 * 3600 }
      );
      sent++;
    } catch (err) {
      failed++;
      console.log(`  send failed (${err.statusCode ?? err.message}) for ${event.to} — device may have unsubscribed`);
    }
  }
}
console.log(`sent: ${sent}, failed: ${failed}`);
