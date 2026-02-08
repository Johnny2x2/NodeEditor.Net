// Service Worker for NodeEditorMax PWA
const CACHE_NAME = 'nodeeditormax-v1';
const OFFLINE_URL = '/';

// Assets to cache on install
const PRECACHE_ASSETS = [
    '/',
    '/app.css',
    '/_content/NodeEditor.Blazor/css/node-editor.css',
    '/_content/NodeEditor.Blazor/css/plugin-manager.css'
];

self.addEventListener('install', (event) => {
    event.waitUntil(
        caches.open(CACHE_NAME).then((cache) => {
            return cache.addAll(PRECACHE_ASSETS);
        })
    );
    self.skipWaiting();
});

self.addEventListener('activate', (event) => {
    event.waitUntil(
        caches.keys().then((cacheNames) => {
            return Promise.all(
                cacheNames
                    .filter((name) => name !== CACHE_NAME)
                    .map((name) => caches.delete(name))
            );
        })
    );
    self.clients.claim();
});

self.addEventListener('fetch', (event) => {
    // Skip non-GET requests and Blazor SignalR connections
    if (event.request.method !== 'GET' || 
        event.request.url.includes('_blazor') ||
        event.request.url.includes('negotiate')) {
        return;
    }

    event.respondWith(
        fetch(event.request)
            .then((response) => {
                // Cache successful responses
                if (response.ok) {
                    const responseClone = response.clone();
                    caches.open(CACHE_NAME).then((cache) => {
                        cache.put(event.request, responseClone);
                    });
                }
                return response;
            })
            .catch(() => {
                // Return cached version on network failure
                return caches.match(event.request);
            })
    );
});
