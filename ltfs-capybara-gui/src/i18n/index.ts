import { createI18n } from 'vue-i18n';
import enUS from './messages/en-US';
import zhCN from './messages/zh-CN';

export type AppLocale = 'en-US' | 'zh-CN';

const LOCALE_STORAGE_KEY = 'ltfs-capybara.locale';
const DEFAULT_LOCALE: AppLocale = 'en-US';

const messages = {
    'en-US': enUS,
    'zh-CN': zhCN,
};

function isAppLocale(locale: string): locale is AppLocale {
    return locale === 'en-US' || locale === 'zh-CN';
}

function detectInitialLocale(): AppLocale {
    const saved = localStorage.getItem(LOCALE_STORAGE_KEY);
    if (saved && isAppLocale(saved)) {
        return saved;
    }

    const browserLocale = navigator.language;
    if (isAppLocale(browserLocale)) {
        return browserLocale;
    }

    if (browserLocale.toLowerCase().startsWith('zh')) {
        return 'zh-CN';
    }

    return DEFAULT_LOCALE;
}

export const i18n = createI18n({
    legacy: false,
    locale: detectInitialLocale(),
    fallbackLocale: DEFAULT_LOCALE,
    messages,
});

export function setAppLocale(locale: AppLocale): void {
    i18n.global.locale.value = locale;
    localStorage.setItem(LOCALE_STORAGE_KEY, locale);
}
