// Generic-only Web Push display and same-origin, authenticated-on-open navigation.
(function (scope) {
    'use strict';
    const uuid = value => typeof value === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value) && value !== '00000000-0000-0000-0000-000000000000';
    const title = 'Companion review cue';
    const body = 'Open Companion to review a saved cue. Reviewing is optional.';
    function valid(value, now) {
        return value && value.version === 1 && value.type === 'task-review'
            && ['workspaceId','taskId','scheduleId','occurrenceId'].every(key => uuid(value[key]))
            && Number.isSafeInteger(value.scheduleRevision) && value.scheduleRevision > 0
            && Number.isSafeInteger(value.expiresAtMs) && value.expiresAtMs > now && value.expiresAtMs <= now + 3600000
            && value.url === '/todo/today' && value.title === title && value.body === body;
    }
    function destination(data) {
        if (!data || data.version !== 1 || !['workspaceId','taskId','occurrenceId'].every(key => uuid(data[key]))) return null;
        return '/todo/reminders/open?workspaceId=' + data.workspaceId + '&taskId=' + data.taskId + '&occurrenceId=' + data.occurrenceId;
    }
    async function push(event) {
        let payload;
        try { if (!event.data) return; const text = event.data.text(); if (text.length > 4096) return; payload = JSON.parse(text); } catch { return; }
        if (!valid(payload, Date.now())) return;
        const data = {version:1,workspaceId:payload.workspaceId,taskId:payload.taskId,occurrenceId:payload.occurrenceId};
        await scope.registration.showNotification(title, {body,tag:'companion-review-'+payload.occurrenceId,data,icon:'/icon-192.png'});
    }
    async function click(event) {
        event.notification.close();
        const path = destination(event.notification.data);
        if (!path) return;
        const url = new URL(path,scope.location.origin);
        if (url.origin !== scope.location.origin) return;
        const windows = await scope.clients.matchAll({type:'window',includeUncontrolled:false});
        // Preserve unrelated editors/drafts. Reuse only an exact guarded destination.
        const existing = windows.find(client => client.url === url.href);
        if (existing) await existing.focus(); else await scope.clients.openWindow(url.href);
    }
    scope.companionPush = {push,click};
})(self);
