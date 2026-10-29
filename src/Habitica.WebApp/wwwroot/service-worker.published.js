// Caution! Be sure you understand the caveats before publishing an application with
// offline support. See https://aka.ms/blazor-offline-considerations

self.importScripts('./service-worker-assets.js');
self.importScripts('./js/pushNotifications.js');
self.addEventListener('push', event => event.waitUntil(self.companionPush.push(event)));
self.addEventListener('notificationclick', event => event.waitUntil(self.companionPush.click(event)));
self.addEventListener('install', event => event.waitUntil(onInstall(event)));
self.addEventListener('activate', event => event.waitUntil(onActivate(event)));
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));

const cacheNamePrefix = 'offline-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlineAssetsInclude = [ /\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/, /\.json$/, /\.webmanifest$/, /\.css$/, /\.woff$/, /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/, /\.blat$/, /\.dat$/ ];
const offlineAssetsExclude = [ /^service-worker\.js$/ ];

// Replace with your base path if you are hosting on a subfolder. Ensure there is a trailing '/'.
const base = "/";
const baseUrl = new URL(base, self.origin);
const manifestUrlList = self.assetsManifest.assets.map(asset => new URL(asset.url, baseUrl).href);
// Only application routes may receive the offline shell. Auth, privacy and APIs stay network-only.
const appRoutes = /^\/(?:|sign-in|signin|dashboard|tasks|inventory|pets-mounts|party|quests|spells|settings|diagnostics|live-tests|notes|todo\/(?:tasks|dailies|habits|today|inbox|plan|projects|lists|history|statistics|sync|reminders(?:\/open)?|convert(?:\/[0-9a-fA-F-]{36})?|retire\/[0-9a-fA-F-]{36}))\/?$/;
const sensitivePath = pathname => /^\/(?:api|auth|oauth|\.well-known|privacy|terms|notes-api|task-api|organization-api|retirement-api)(?:\/|$)/.test(pathname);

async function onInstall(event) {
    console.info('Service worker: Install');

    // Fetch and cache all matching items from the assets manifest
    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => {
            const url = new URL(asset.url, baseUrl);
            return url.origin === baseUrl.origin && !sensitivePath(url.pathname);
        })
        .map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));
    await caches.open(cacheName).then(cache => cache.addAll(assetsRequests));
}

async function onActivate(event) {
    console.info('Service worker: Activate');

    // Delete unused caches
    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
        .map(key => caches.delete(key)));
}

async function onFetch(event) {
    let cachedResponse = null;
    const url = new URL(event.request.url);
    const sameOrigin = url.origin === baseUrl.origin;
    const sensitive = sensitivePath(url.pathname);
    if (event.request.method === 'GET' && sameOrigin && !sensitive) {
        // For all navigation requests, try to serve index.html from cache,
        // unless that request is for an offline resource.
        // If you need some URLs to be server-rendered, edit the following check to exclude those URLs
        const shouldServeIndexHtml = event.request.mode === 'navigate'
            && appRoutes.test(url.pathname);

        if (shouldServeIndexHtml || manifestUrlList.includes(event.request.url)) {
            const request = shouldServeIndexHtml ? 'index.html' : event.request;
            const cache = await caches.open(cacheName);
            cachedResponse = await cache.match(request);
        }
    }

    return cachedResponse || fetch(event.request);
}
