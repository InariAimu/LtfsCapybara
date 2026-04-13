---
description: "Use when editing ltfs-capybara-gui Vue, TypeScript, Tauri, Naive UI, i18n, or frontend API integration. Covers build validation, shared path helpers, locale wiring, and format parameter alignment."
name: "Ltfs Capybara GUI Rules"
applyTo: "ltfs-capybara-gui/src/**/*.ts, ltfs-capybara-gui/src/**/*.vue, ltfs-capybara-gui/src/**/*.tsx, ltfs-capybara-gui/src-tauri/**, ltfs-capybara-gui/package.json, ltfs-capybara-gui/vite.config.ts"
---

# Ltfs Capybara GUI Rules

- Validate GUI changes with `pnpm -C .\\ltfs-capybara-gui build`; this runs `vue-tsc` and `vite build`.
- Use `pnpm -C .\\ltfs-capybara-gui format` to use prettier for code formatting.
- Keep imports and locals clean; the GUI TypeScript configuration fails builds on unused imports or variables.
- Reuse shared path helpers from `ltfs-capybara-gui/src/utils/path.ts` instead of redefining normalize, parent, basename, or path-key helpers in views.
- Preserve the i18n bridge in `src/i18n/index.ts` and `App.vue`: locale is stored in `localStorage` key `ltfs-capybara.locale` and must stay aligned with Naive UI locale/date-locale wiring.
- Keep frontend format task payloads aligned with `Ltfs.FormatParam`, including `mediaPool` and `encryptionKey`; C# `byte[]` values serialize to Base64 strings, not number arrays.