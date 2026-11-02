# PWA Installation and Relaunch

## Current implementation

The manifest names Habitica Companion, retains the existing stable manifest ID/start URL, declares standalone display and scope, and references verified 192-pixel and 512-pixel PNG icons. The document supplies an Apple web-app title and touch icons. PwaInstallPanel exposes expandable installation steps on Sign-in and Settings without notification prompts or credential writes.

The Safari flow follows [Apple installation guidance](https://support.apple.com/guide/iphone/open-as-web-app-iphea86e5236/ios): open Share, choose Add to Home Screen, retain Open as Web App when available and open the resulting Home Screen icon. Toolbar layout can change the Share entry point. [Safari 26 guidance](https://webkit.org/blog/17333/webkit-features-in-safari-26-0/) documents the Open as Web App choice. Installation cannot establish a shared session between Safari and the standalone app; missing credentials require explicit sign-in. Remember credentials remains optional and session-only remains the default.

## Offline boundaries

The secure-page load handler registers service-worker.js. Development retains the network-only worker; the Release publication supplies the cache worker. Registration refusal preserves online use. Successful installation caches same-origin public build assets and the manifest. Only known application navigation routes receive the cached index shell. Auth, OAuth, discovery, privacy, terms, API, unknown and foreign routes remain network-only; non-GET requests never use the shell cache.

Cached public screens establish no authenticated session or fresh Habitica result. Initialization restores credentials only from an explicitly persisted store. Missing credentials keep the app signed out. Recognized browser-storage or malformed-JSON failures retain a sanitized recovery message without authenticating or exposing storage details. Cancellation propagates. No credential is stored in the service-worker cache or app export.

## Physical iPhone release gate

Local synthetic evidence cannot complete the device portion of T-d76d2a9a-1318-4e63-9bb4-73f620b108cc. The gate requires an explicitly selected device, iOS version, production/staging URL and authorized account or signed-out lane. Installation and relaunch alone grant no gameplay, notification, server enrollment or credential-persistence permission.

| Check | Required observation | Current device evidence |
| --- | --- | --- |
| Safari installation | Correct app name/icon; Open as Web App choice; separate app window | Unrun |
| Missing standalone session | Explicit sign-in; no assumption of Safari storage transfer | Unrun |
| Session-only closure/relaunch | No silent credential persistence; reauthentication when the process session is lost | Unrun |
| Explicit remembered sign-in | Restore only the chosen credential store; no credential text in UI/export | Unrun |
| Offline startup after online preparation | Cached app shell and retained local data; fresh network actions stay unavailable | Unrun |
| Storage denied or evicted | Signed-out recovery without invented account state | Unrun |
| Layout and input | Safe-area edges, onscreen keyboard, focus return, readable light/dark text and enlarged text | Unrun |

Use disposable synthetic data for offline/storage controls. Device results must record observed state and limitations rather than infer browser policy from desktop fixtures. Installation supplies no Web Push qualification. [WebKit's push contract](https://webkit.org/blog/13878/web-push-for-web-apps-on-ios-and-ipados/) requires a supported Home Screen app and direct user interaction for permission; subscription and delivery remain separate tasks.
