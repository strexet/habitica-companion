// Imported on demand. Reading capability never asks for notification permission.
export function createPushBrowser(host = globalThis) {
    const installed = () => host.navigator?.standalone === true || host.matchMedia?.('(display-mode: standalone)')?.matches === true;
    function capability() {
        const supported = host.isSecureContext === true && !!host.navigator?.serviceWorker && !!host.PushManager && !!host.Notification;
        return { state: !host.isSecureContext ? 'insecure' : !installed() ? 'installation-required' : !supported ? 'unsupported' : 'available', permission: host.Notification?.permission ?? 'unsupported' };
    }
    function bytes(value) {
        if (typeof value !== 'string' || !/^[A-Za-z0-9_-]{87}$/.test(value)) throw new Error('invalid-application-key');
        const raw = host.atob(value.replace(/-/g, '+').replace(/_/g, '/') + '=');
        const result = Uint8Array.from(raw, c => c.charCodeAt(0));
        if (result.length !== 65 || result[0] !== 4) throw new Error('invalid-application-key');
        return result;
    }
    const base64 = data => host.btoa(String.fromCharCode(...new Uint8Array(data))).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
    const fingerprint = async subscription => base64(await host.crypto.subtle.digest('SHA-256', new TextEncoder().encode(subscription.endpoint)));
    async function registration() {
        // Do not leave an indefinite serviceWorker.ready wait after a failed install.
        const worker = await host.navigator.serviceWorker.getRegistration();
        if (!worker?.active || !worker.pushManager || new URL(worker.scope).origin !== host.location.origin) throw new Error('worker-unavailable');
        return worker;
    }
    async function current() {
        const c = capability();
        if (c.state !== 'available') return { ...c, exists: false, fingerprint: null };
        try { const subscription = await (await registration()).pushManager.getSubscription(); return { ...c, exists: !!subscription, fingerprint: subscription ? await fingerprint(subscription) : null }; }
        catch { return { ...c, state: 'worker-unavailable', exists: false, fingerprint: null }; }
    }
    async function subscribe(applicationServerKey, reminderConsent) {
        const c = capability();
        if (c.state !== 'available') return { state: c.state };
        if (reminderConsent !== true) return { state: 'consent-required' };
        if (!host.navigator.userActivation?.isActive) return { state: 'gesture-required' };
        let key; try { key = bytes(applicationServerKey); } catch { return { state: 'invalid-application-key' }; }
        try {
            // Call before any await: iOS requires the direct user gesture here.
            const permission = await host.Notification.requestPermission();
            if (permission !== 'granted') return { state: permission === 'denied' ? 'denied' : 'dismissed' };
            const worker = await registration();
            let subscription = await worker.pushManager.getSubscription();
            if (subscription) {
                if (subscription.options?.userVisibleOnly !== true) return { state: 'user-visible-required' };
                const old = subscription.options?.applicationServerKey;
                if (!old || base64(old) !== applicationServerKey) return { state: 'key-changed' };
            } else subscription = await worker.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: key });
            const json = subscription.toJSON();
            if (!json?.endpoint || !json.keys?.p256dh || !json.keys?.auth) return { state: 'subscription-unavailable' };
            // Transient upload payload only; callers must never persist or log these keys.
            return { state: 'subscribed', fingerprint: await fingerprint(subscription), subscription: { endpoint: json.endpoint, keys: { p256dh: json.keys.p256dh, auth: json.keys.auth }, expirationTime: json.expirationTime ?? null } };
        } catch { return { state: 'subscription-unavailable' }; }
    }
    async function unsubscribe(expectedFingerprint) {
        const c = capability();
        if (c.state !== 'available') return { state: c.state };
        if (!host.navigator.userActivation?.isActive) return { state: 'gesture-required' };
        if (typeof expectedFingerprint !== 'string' || !/^[A-Za-z0-9_-]{43}$/.test(expectedFingerprint)) return { state: 'subscription-changed' };
        try {
            const subscription = await (await registration()).pushManager.getSubscription();
            if (!subscription) return { state: 'already-absent' };
            if (await fingerprint(subscription) !== expectedFingerprint) return { state: 'subscription-changed' };
            return { state: await subscription.unsubscribe() ? 'unsubscribed' : 'unsubscribe-unconfirmed' };
        } catch { return { state: 'unsubscribe-unconfirmed' }; }
    }
    return { capability, current, subscribe, unsubscribe };
}
const browser = createPushBrowser();
export const capability = () => browser.capability();
export const current = () => browser.current();
export const subscribe = (key, consent) => browser.subscribe(key, consent);
export const unsubscribe = fingerprint => browser.unsubscribe(fingerprint);
