# GUI Repository Memory

- Build validation command: `pnpm -C .\\ltfs-capybara-gui build`.
- The GUI build treats unused imports and unused locals as errors; keep Vue SFC imports clean.
- i18n is wired through `ltfs-capybara-gui/src/i18n/index.ts`, with locale persisted in `localStorage` key `ltfs-capybara.locale` and bridged to Naive UI locale and date-locale in `App.vue`.
- Shared path helpers live in `ltfs-capybara-gui/src/utils/path.ts`; reuse them instead of redefining path normalization helpers in views or composables.
- Frontend `TapeFsFormatParam` should stay aligned with `Ltfs.FormatParam`, including `mediaPool` and `encryptionKey`.
- C# `byte[]` values are represented in JSON as Base64 strings, not numeric arrays.