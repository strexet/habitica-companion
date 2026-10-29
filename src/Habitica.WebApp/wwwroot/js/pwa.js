// Installation and notification permission remain explicit browser actions.
(() => {
    if (!('serviceWorker' in navigator) || !window.isSecureContext) return;
    window.addEventListener('load', () => {
        navigator.serviceWorker.register('service-worker.js', { scope: './' })
            .catch(() => {
                // Online use remains available if offline setup is refused.
                document.documentElement.dataset.pwaOffline = 'unavailable';
            });
    }, { once: true });
})();
