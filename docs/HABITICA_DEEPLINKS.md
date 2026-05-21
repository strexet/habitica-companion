# Habitica Mobile Deep Links

Last researched: 2026-05-21

This document records whether Habitica browser links can open the official mobile apps directly to party or quest views.

## Summary

Habitica mobile app deep-linking to the party or quest view is not currently a supported integration target for this project.

Use `https://habitica.com/party` as a plain web fallback. Do not emit `habitica://` URLs, Android `intent://` URLs, or mobile-app-specific party/quest links unless the official apps and `habitica.com` association files change.

## Evidence

### Public store listings

- iOS App Store: [Habitica: Gamified Taskmanager](https://apps.apple.com/us/app/habitica-gamified-taskmanager/id994882113)
- Google Play: [Habitica: Gamify Your Tasks](https://play.google.com/store/apps/details?id=com.habitrpg.android.habitica)

The public store listings identify the official apps, but neither listing documents a party or quest deep-link URL.

### `habitica.com` association files

Checked:

- `https://habitica.com/.well-known/apple-app-site-association`
- `https://habitica.com/apple-app-site-association`
- `https://habitica.com/.well-known/assetlinks.json`

All three URLs returned the Habitica web app HTML shell with `content-type: text/html; charset=UTF-8`, not the JSON association documents required for iOS Universal Links or Android App Links. That means `https://habitica.com/party` must be treated as a web URL, not as a verified app-opening link.

### Android app source

Official source: [HabitRPG/habitica-android](https://github.com/HabitRPG/habitica-android)

The Android manifest on `main` declares verified HTTPS handlers for:

- `https://habitica.com/`
- `https://habitica.com/settings/...`
- `https://habitica.com/profile/...`

Source: [`Habitica/AndroidManifest.xml`](https://raw.githubusercontent.com/HabitRPG/habitica-android/main/Habitica/AndroidManifest.xml)

The manifest does not declare a `/party` or quest-view handler, and it does not declare a `habitica://` scheme.

### iOS app source

Official source: [HabitRPG/habitica-ios](https://github.com/HabitRPG/habitica-ios)

The iOS entitlements file on `develop` does not declare `com.apple.developer.associated-domains`, so the app does not advertise Universal Link domains in source.

Source: [`Habitica.entitlements`](https://raw.githubusercontent.com/HabitRPG/habitica-ios/develop/Habitica.entitlements)

The app Info.plist registers Facebook and Google callback URL schemes, but no `habitica` URL scheme.

Source: [`HabitRPG/Habitica-Info.plist`](https://raw.githubusercontent.com/HabitRPG/habitica-ios/develop/HabitRPG/Habitica-Info.plist)

## Project rule

Until the official mobile apps and `habitica.com` association files document a supported party/quest deep-link contract:

- keep "Open in Habitica" controls pointed at stable web URLs;
- do not add platform-specific app-opening probes;
- do not add custom-scheme fallbacks;
- do not add Android `intent://` URLs.

