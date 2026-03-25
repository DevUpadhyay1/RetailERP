const CACHE_VERSION = 'retailerp-v1';
const APP_SHELL = [
    '/',
    '/css/site.css',
    '/css/pos.css',
    '/css/dashboard.css',
    '/js/site.js',
    '/js/dashboard.js',
    '/js/pwa.js',
    '/manifest.json',
    '/favicon.ico',
    '/icons/icon-192.png',
    '/icons/icon-512.png',
    'https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap.min.css',
    'https://code.jquery.com/jquery-3.7.1.min.js',
    'https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/js/bootstrap.bundle.min.js',
    '/offline.html'
];

self.addEventListener('install', event => {
    event.waitUntil(
        caches.open(CACHE_VERSION)
            .then(cache => cache.addAll(APP_SHELL))
            .then(() => self.skipWaiting())
    );
});

self.addEventListener('activate', event => {
    event.waitUntil(
        caches.keys().then(keys =>
            Promise.all(
                keys.filter(k => k !== CACHE_VERSION).map(k => caches.delete(k))
            )
        ).then(() => self.clients.claim())
    );
});

self.addEventListener('fetch', event => {
    const { request } = event;

    if (request.method !== 'GET') return;

    // Ignore non-http(s) schemes (chrome-extension://, etc.)
    if (!request.url.startsWith('http')) return;

    // API calls: network-first, no cache fallback
    if (request.url.includes('/api/')) {
        event.respondWith(
            fetch(request).catch(() => new Response(
                JSON.stringify({ success: false, offline: true, errors: ['You are offline.'] }),
                { headers: { 'Content-Type': 'application/json' } }
            ))
        );
        return;
    }

    // Navigation requests: network-first, fallback to offline page
    if (request.mode === 'navigate') {
        event.respondWith(
            fetch(request)
                .then(response => {
                    const clone = response.clone();
                    caches.open(CACHE_VERSION).then(cache => cache.put(request, clone));
                    return response;
                })
                .catch(() => caches.match(request).then(r => r || caches.match('/offline.html')))
        );
        return;
    }

    // Static assets: cache-first
    event.respondWith(
        caches.match(request).then(cached => {
            if (cached) return cached;
            return fetch(request).then(response => {
                if (response.ok) {
                    const clone = response.clone();
                    caches.open(CACHE_VERSION).then(cache => cache.put(request, clone));
                }
                return response;
            }).catch(() => new Response('', { status: 503 }));
        })
    );
});

// Listen for sync events (Background Sync API)
self.addEventListener('sync', event => {
    if (event.tag === 'sync-offline-bills') {
        event.respondWith(self.clients.matchAll().then(clients => {
            clients.forEach(client => client.postMessage({ type: 'TRIGGER_SYNC' }));
        }));
    }
});

self.addEventListener('message', event => {
    if (event.data === 'SKIP_WAITING') {
        self.skipWaiting();
    }
});
