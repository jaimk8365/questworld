/* QuestWorld service worker — offline support.
   __QW_BUILD__ is replaced with a timestamp by deploy.sh so every deploy
   gets a fresh cache and old versions are cleaned up. */
const CACHE = "questworld-__QW_BUILD__";

self.addEventListener("install", (event) => {
  event.waitUntil(
    caches
      .open(CACHE)
      .then((cache) => cache.addAll(["./", "./manifest.webmanifest", "./icon.svg", "./apple-touch-icon.png", "./icon-512.png"]))
      .then(() => self.skipWaiting())
  );
});

self.addEventListener("activate", (event) => {
  event.waitUntil(
    caches
      .keys()
      .then((keys) => Promise.all(keys.filter((k) => k !== CACHE).map((k) => caches.delete(k))))
      .then(() => self.clients.claim())
  );
});

// ---- push notifications ----
self.addEventListener("push", (event) => {
  let payload = {};
  try { payload = event.data ? event.data.json() : {}; } catch { /* ignore */ }
  event.waitUntil(
    self.registration.showNotification(payload.title || "QuestWorld", {
      body: payload.body || "",
      icon: "./icon-512.png",
      badge: "./apple-touch-icon.png",
      data: { url: "./" },
    })
  );
});

self.addEventListener("notificationclick", (event) => {
  event.notification.close();
  event.waitUntil(
    clients.matchAll({ type: "window", includeUncontrolled: true }).then((wins) =>
      wins.length ? wins[0].focus() : clients.openWindow("./")
    )
  );
});

self.addEventListener("fetch", (event) => {
  const req = event.request;
  if (req.method !== "GET") return;
  const url = new URL(req.url);
  if (url.origin !== location.origin) return;

  if (req.mode === "navigate") {
    // Network-first for the page itself: updates arrive whenever online,
    // cached copy keeps the game working offline.
    event.respondWith(
      fetch(req)
        .then((res) => {
          const copy = res.clone();
          caches.open(CACHE).then((cache) => cache.put("./", copy));
          return res;
        })
        .catch(() => caches.match("./"))
    );
  } else {
    // Cache-first for assets (they carry content hashes in their names).
    event.respondWith(
      caches.match(req).then(
        (hit) =>
          hit ||
          fetch(req).then((res) => {
            if (res.ok) {
              const copy = res.clone();
              caches.open(CACHE).then((cache) => cache.put(req, copy));
            }
            return res;
          })
      )
    );
  }
});
