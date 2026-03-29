import { computed, readonly, ref } from 'vue';

export type AppThemeMode = 'auto' | 'light' | 'dark';

const THEME_MODE_STORAGE_KEY = 'ltfs-capybara.theme-mode';
const DEFAULT_THEME_MODE: AppThemeMode = 'auto';
const DARK_MEDIA_QUERY = '(prefers-color-scheme: dark)';

function isAppThemeMode(mode: string): mode is AppThemeMode {
    return mode === 'auto' || mode === 'light' || mode === 'dark';
}

function detectInitialThemeMode(): AppThemeMode {
    const saved = localStorage.getItem(THEME_MODE_STORAGE_KEY);
    if (saved && isAppThemeMode(saved)) {
        return saved;
    }

    return DEFAULT_THEME_MODE;
}

const appThemeMode = ref<AppThemeMode>(detectInitialThemeMode());
const systemPrefersDark = ref<boolean>(
    typeof window !== 'undefined' && typeof window.matchMedia === 'function'
        ? window.matchMedia(DARK_MEDIA_QUERY).matches
        : false,
);

if (typeof window !== 'undefined' && typeof window.matchMedia === 'function') {
    const mediaQuery = window.matchMedia(DARK_MEDIA_QUERY);
    const handleChange = (event: MediaQueryListEvent): void => {
        systemPrefersDark.value = event.matches;
    };

    mediaQuery.addEventListener('change', handleChange);
}

const isDarkTheme = computed<boolean>(() => {
    if (appThemeMode.value === 'dark') {
        return true;
    }

    if (appThemeMode.value === 'light') {
        return false;
    }

    return systemPrefersDark.value;
});

export function setAppThemeMode(mode: AppThemeMode): void {
    appThemeMode.value = mode;
    localStorage.setItem(THEME_MODE_STORAGE_KEY, mode);
}

export function useAppTheme() {
    return {
        appThemeMode: readonly(appThemeMode),
        isDarkTheme,
    };
}
