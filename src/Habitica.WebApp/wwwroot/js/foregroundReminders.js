const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;
export function createForegroundReminders(host, store, publish = () => {}) {
    let context = null, cues = [], version = 0, generation = 0, timer = null, busyGeneration = null, disposed = false;
    let prefs = {inApp:false,browser:false}, storage = 'available', delivery = 'idle', lastWall = null, lastMono = null, clockChanged = false;
    const now = () => host.Date.now();
    const permission = () => ['granted','denied','default'].includes(host.Notification?.permission) ? host.Notification.permission : 'unsupported';
    const capability = () => !host.isSecureContext ? 'insecure' : !host.Notification || !host.navigator?.serviceWorker || !host.crypto?.subtle ? 'unsupported' : 'available';
    const snapshot = () => ({bindingVersion:version,contextKey:context,inApp:prefs.inApp,browser:prefs.browser,permission:permission(),capability:capability(),storage,delivery,nowMs:now(),clockChanged});
    const emit = () => { const state=snapshot();try { const result=publish(state);result?.catch?.(()=>{}); } catch {} return state; };
    const current = (key, serial) => !disposed && context===key && generation===serial;
    function cancelTimer() { if(timer!==null)host.clearTimeout(timer);timer=null; }
    function arm() {
        cancelTimer();if(!context||disposed)return;
        const next=cues.find(c=>c.atMs>now());
        timer=host.setTimeout(()=>{timer=null;void tick();},Math.max(50,Math.min(30000,next?next.atMs-now():30000)));
    }
    async function read(key,serial) {
        try { const value=await store.readReminderState(key);if(!current(key,serial))return false;prefs=value;storage='available';return true; }
        catch { if(current(key,serial)){storage='unavailable';prefs={inApp:false,browser:false};delivery='storage-unavailable';}return false; }
    }
    const digest = async text => Array.from(new Uint8Array(await host.crypto.subtle.digest('SHA-256',new TextEncoder().encode(text))),x=>x.toString(16).padStart(2,'0')).join('');
    async function bounded(action) {
        let handle;
        try { return await Promise.race([action(),new Promise((_,reject)=>{handle=host.setTimeout(()=>reject(new Error('unavailable')),3000);})]); }
        finally {if(handle!==undefined)host.clearTimeout(handle);}
    }
    async function tick() {
        if(busyGeneration===generation||!context||disposed){arm();return snapshot();}
        const key=context,serial=generation;busyGeneration=serial;
        try {
            const wall=now(),mono=host.performance.now();
            if(lastWall!==null&&Math.abs((wall-lastWall)-(mono-lastMono))>5000)clockChanged=true;
            lastWall=wall;lastMono=mono;
            if(!await read(key,serial))return snapshot();
            if(!prefs.inApp||!prefs.browser||permission()!=='granted'||capability()!=='available'||host.document.visibilityState!=='visible')return snapshot();
            const due=cues.filter(c=>c.atMs<=now());
            for(const cue of due) {
                const id=await digest(key+':'+cue.taskId+':'+cue.reviewAfterUtc+':'+cue.scheduleRevision);
                if(!current(key,serial))break;
                if(prefs.receipts?.some(r=>r.id===id))continue;
                if(prefs.receipts?.length>=2048){delivery='receipt-capacity';break;}
                // Inspect the existing registration only. Never register, subscribe or fetch upstream.
                const registration=await bounded(()=>host.navigator.serviceWorker.getRegistration(host.location.href));
                if(!current(key,serial))break;
                if(!registration?.active||typeof registration.showNotification!=='function'||!registration.scope?.startsWith(host.location.origin+'/')||!host.location.href.startsWith(registration.scope)){delivery='worker-unavailable';break;}
                if(host.document.visibilityState!=='visible'||permission()!=='granted')break;
                const claimed=await store.claimReminder(key,id,now());
                if(!current(key,serial))break;
                if(claimed==='duplicate')continue;
                if(claimed!=='claimed'){delivery=claimed;break;}
                // Claim remains consumed if permission/account/source changes or acknowledgement is lost.
                if(!await read(key,serial)||!current(key,serial)||!prefs.inApp||!prefs.browser||permission()!=='granted'||host.document.visibilityState!=='visible')break;
                try {
                    await bounded(()=>registration.showNotification('Companion review cue',{body:'Open Today to review a saved cue. Reviewing is optional.',tag:'companion-review-'+id,renotify:false}));
                    await store.finishReminder(key,id,'shown');if(current(key,serial))delivery='shown';
                } catch {try{await store.finishReminder(key,id,'unknown');}catch{}if(current(key,serial))delivery='unknown';}
                break; // At most one new attempt per local wake; no catch-up burst.
            }
        } catch {if(current(key,serial)){delivery='unavailable';storage='unavailable';}}
        finally {if(busyGeneration===serial)busyGeneration=null;if(current(key,serial)){emit();arm();}}
        return snapshot();
    }
    function bind(owner,rows,nextVersion) {
        if(!Number.isSafeInteger(nextVersion)||nextVersion<=version||disposed)return Promise.resolve(snapshot());
        cancelTimer();generation++;version=nextVersion;context=null;cues=[];prefs={inApp:false,browser:false};delivery='idle';lastWall=null;lastMono=null;clockChanged=false;
        if(owner!==null) {
            if(!uuid.test(owner?.accountId)||!uuid.test(owner?.workspaceId)||!['browser','cloud'].includes(owner?.source)||!Array.isArray(rows)||rows.length>10000)throw new Error('Invalid reminder binding.');
            const ids=new Set();
            cues=rows.map(c=>{
                const atMs=Date.parse(c?.reviewAfterUtc);
                if(!uuid.test(c?.taskId)||ids.has(c.taskId)||!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z$/.test(c?.reviewAfterUtc)||!Number.isFinite(atMs)||!Number.isSafeInteger(c?.scheduleRevision)||c.scheduleRevision<0)throw new Error('Invalid reminder cue.');
                ids.add(c.taskId);return {...c,atMs};
            }).sort((a,b)=>a.atMs-b.atMs||a.taskId.localeCompare(b.taskId));
            context=owner.accountId+':'+owner.source+':'+owner.workspaceId;
        }
        emit();return tick();
    }
    async function configure(kind,enabled,expectedVersion) {
        if(expectedVersion!==version||!context||disposed||!['inApp','browser'].includes(kind)||typeof enabled!=='boolean')return snapshot();
        if(kind==='browser'&&enabled)return snapshot(); // Only the direct permission action can enable this channel.
        const key=context,serial=generation;
        try {const value=await store.configureReminderState(key,kind,enabled);if(current(key,serial)){prefs=value;storage='available';delivery='idle';}}
        catch {if(current(key,serial)){storage='unavailable';delivery='storage-unavailable';}}
        emit();arm();return snapshot();
    }
    async function requestPermission(expectedVersion) {
        if(expectedVersion!==version||!context||disposed||!prefs.inApp||storage!=='available')return snapshot();
        if(capability()!=='available'){delivery='unsupported';return emit();}
        if(!host.navigator.userActivation?.isActive){delivery='gesture-required';return emit();}
        const key=context,serial=generation;
        // Invoke permission synchronously before the first await in this click path.
        let answer;
        try {answer=permission()==='default'?host.Notification.requestPermission():permission();answer=await answer;}
        catch {if(current(key,serial))delivery='permission-unavailable';return emit();}
        if(!current(key,serial))return snapshot();
        if(answer!=='granted'){delivery=answer==='denied'?'denied':'dismissed';return emit();}
        try {const value=await store.configureReminderState(key,'browser',true);if(current(key,serial)){prefs=value;delivery='enabled';}}
        catch {if(current(key,serial)){storage='unavailable';delivery='storage-unavailable';}}
        emit();arm();return snapshot();
    }
    const wake=()=>{void tick();};
    host.document.addEventListener('visibilitychange',wake);host.addEventListener('pageshow',wake);host.addEventListener('focus',wake);
    return {bind,configure,requestPermission,tick,snapshot,
        async clear(){generation++;context=null;cues=[];prefs={inApp:false,browser:false};cancelTimer();await store.clearReminderStates();return emit();},
        stop(){disposed=true;generation++;context=null;cues=[];cancelTimer();host.document.removeEventListener('visibilitychange',wake);host.removeEventListener('pageshow',wake);host.removeEventListener('focus',wake);}
    };
}
let runtime;
export async function prepare(callback) {
    if(runtime)return;
    const store=await import('./storage/indexedDbStorage.js');
    runtime=createForegroundReminders(globalThis,store,state=>callback.invokeMethodAsync('Publish',state));
}
export const bind=(...args)=>runtime.bind(...args);
export const configure=(...args)=>runtime.configure(...args);
export const requestPermission=(...args)=>runtime.requestPermission(...args);
export const clear=()=>runtime.clear();
export const stop=()=>{runtime?.stop();runtime=undefined;};
