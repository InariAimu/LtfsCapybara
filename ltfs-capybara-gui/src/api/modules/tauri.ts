import { invoke, isTauri } from '@tauri-apps/api/core';

export type TaskbarProgressStatus = 'none' | 'normal' | 'indeterminate' | 'paused' | 'error';

export interface TaskbarProgressPayload {
    status?: TaskbarProgressStatus;
    progress?: number;
}

export interface NotificationPayload {
    title?: string;
    body?: string;
    icon?: string;
    sound?: string;
}

const runningInTauri = isTauri();
const originalDocumentTitle = typeof document !== 'undefined' ? document.title : '';

function setBrowserProgress(payload: TaskbarProgressPayload): void {
    if (typeof document === 'undefined') {
        return;
    }

    const progress = typeof payload.progress === 'number' ? Math.max(0, Math.min(100, payload.progress)) : null;
    const status = payload.status ?? 'normal';

    if (status === 'none' || progress === null) {
        document.title = originalDocumentTitle;
        return;
    }

    document.title = `[${Math.round(progress)}%] ${originalDocumentTitle}`;
}

function clearBrowserProgress(): void {
    if (typeof document !== 'undefined') {
        document.title = originalDocumentTitle;
    }
}

async function showBrowserNotification(payload: NotificationPayload): Promise<void> {
    if (typeof window === 'undefined' || typeof Notification === 'undefined') {
        return;
    }

    let permission = Notification.permission;
    if (permission === 'default') {
        permission = await Notification.requestPermission();
    }
    if (permission !== 'granted') {
        return;
    }

    const { title, body, icon } = payload;
    new Notification(title ?? (document.title || 'Notification'), {
        body,
        icon,
    });
}

export const tauriApi = {
    setTaskbarProgress(payload: TaskbarProgressPayload) {
        if (runningInTauri) {
            return invoke<void>('set_taskbar_progress', { payload });
        }

        setBrowserProgress(payload);
        return Promise.resolve();
    },

    clearTaskbarProgress() {
        if (runningInTauri) {
            return invoke<void>('clear_taskbar_progress');
        }

        clearBrowserProgress();
        return Promise.resolve();
    },

    showNotification(payload: NotificationPayload) {
        if (runningInTauri) {
            return invoke<void>('show_notification', { payload });
        }

        return showBrowserNotification(payload);
    },
};