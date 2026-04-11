<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { MenuOutline } from '@vicons/ionicons5';
import { NLayout, NLayoutFooter, NDrawer, NDrawerContent, NButton, NIcon } from 'naive-ui';
import { useI18n } from 'vue-i18n';

import LeftPanel from './LeftPanel.vue';
import Overview from './Overview.vue';
import Settings from './Settings.vue';
import LocalIndex from './LocalIndex.vue';
import TapeMachine from './TapeMachine.vue';
import TapeLibrary from './TapeLibrary.vue';
import Ltfs from './Ltfs.vue';
import Task from './Task.vue';
import AIChat from './AIChat.vue';
import Test from './Test.vue';
import { useFileStore } from '@/stores/fileStore';
import { useExecutionStore } from '@/stores/executionStore';

const { t } = useI18n();
const fileStore = useFileStore();
const executionStore = useExecutionStore();
const selectedMenuKey = ref<string>('overview');
const keepAliveInclude = ['LocalIndex', 'AIChat'];
const isMobilePortrait = ref(false);
const isMenuDrawerOpen = ref(false);

function handleMenuSelect(key: string) {
    selectedMenuKey.value = key;
    if (isMobilePortrait.value) {
        isMenuDrawerOpen.value = false;
    }
}

const currentPageComponent = computed(() => {
    if (
        selectedMenuKey.value === 'tape-machine-operations' ||
        selectedMenuKey.value.startsWith('tape-machine:')
    ) {
        return TapeMachine;
    }

    if (selectedMenuKey.value === 'ltfs' || selectedMenuKey.value.startsWith('ltfs:')) {
        return Ltfs;
    }

    switch (selectedMenuKey.value) {
        case 'overview':
            return Overview;
        case 'settings':
            return Settings;
        case 'localindex':
            return LocalIndex;
        case 'task':
            return Task;
        case 'ai-chat':
            return AIChat;
        case 'test':
            return Test;
        case 'tape-library':
            return TapeLibrary;
        default:
            return Overview;
    }
});

const selectedTapeDriveId = computed(() => {
    const key = selectedMenuKey.value;
    if (!key.startsWith('tape-machine:')) {
        return null;
    }

    return key.substring('tape-machine:'.length);
});

const selectedLtfsDriveId = computed(() => {
    const key = selectedMenuKey.value;
    if (!key.startsWith('ltfs:')) {
        return null;
    }

    return key.substring('ltfs:'.length);
});

const currentPageProps = computed(() => {
    if (currentPageComponent.value === TapeMachine) {
        return { tapeDriveId: selectedTapeDriveId.value };
    }

    if (currentPageComponent.value === Ltfs) {
        return { tapeDriveId: selectedLtfsDriveId.value };
    }

    return {};
});

const currentPageTitle = computed(() => {
    const key = selectedMenuKey.value;
    if (key === 'tape-machine-operations' || key.startsWith('tape-machine:')) {
        return t('menu.tapeMachine');
    }
    if (key === 'ltfs' || key.startsWith('ltfs:')) {
        return t('menu.ltfs');
    }

    switch (key) {
        case 'overview':
            return t('menu.overview');
        case 'settings':
            return t('menu.settings');
        case 'localindex':
            return t('menu.localIndex');
        case 'task':
            return t('menu.task');
        case 'ai-chat':
            return t('menu.aiChat');
        case 'test':
            return t('menu.test');
        case 'tape-library':
            return t('menu.tapeLibrary');
        default:
            return t('app.navigation');
    }
});

const contentLayoutStyle = computed(() => ({
    top: isMobilePortrait.value ? '48px' : '0',
    bottom: '32px',
}));

function updateResponsiveMode() {
    if (typeof window === 'undefined') {
        return;
    }

    const nextIsMobilePortrait = window.innerWidth <= 768 && window.innerHeight > window.innerWidth;
    isMobilePortrait.value = nextIsMobilePortrait;
    if (!nextIsMobilePortrait) {
        isMenuDrawerOpen.value = false;
    }
}

function openMenuDrawer() {
    isMenuDrawerOpen.value = true;
}

watch(
    selectedTapeDriveId,
    value => {
        fileStore.setCurrentTapeDriveId(value ?? '');
    },
    { immediate: true },
);

onMounted(() => {
    updateResponsiveMode();
    window.addEventListener('resize', updateResponsiveMode);
});

onBeforeUnmount(() => {
    window.removeEventListener('resize', updateResponsiveMode);
});

function formatRemainingSeconds(seconds: number | null | undefined): string {
    if (!Number.isFinite(seconds ?? NaN) || (seconds ?? 0) < 0) {
        return '-';
    }

    const totalSeconds = Math.round(seconds ?? 0);
    const minutes = Math.floor(totalSeconds / 60);
    const remainingSeconds = totalSeconds % 60;
    return `${minutes}:${String(remainingSeconds).padStart(2, '0')}`;
}

function formatFooterSpeed(speedMBPerSecond: number | null | undefined): string {
    if (!Number.isFinite(speedMBPerSecond ?? NaN) || (speedMBPerSecond ?? 0) < 0) {
        return '-';
    }

    return `${(speedMBPerSecond ?? 0).toFixed(speedMBPerSecond! >= 100 ? 0 : 1)} MB/s`;
}

function formatRemainingBytes(bytes: number | null | undefined): string {
    if (!Number.isFinite(bytes ?? NaN) || (bytes ?? 0) < 0) {
        return '-';
    }

    const value = bytes ?? 0;
    if (value >= 1024 * 1024 * 1024) {
        return `${(value / (1024 * 1024 * 1024)).toFixed(1)} GB`;
    }
    if (value >= 1024 * 1024) {
        return `${(value / (1024 * 1024)).toFixed(1)} MB`;
    }
    if (value >= 1024) {
        return `${(value / 1024).toFixed(1)} KB`;
    }

    return `${value.toFixed(0)} B`;
}

const footerText = computed(() => {
    const activeExecution = executionStore.activeExecution;
    if (!activeExecution?.progress) {
        return t('app.footer');
    }

    return t('app.footerRunning', {
        percent: activeExecution.progress.percentComplete.toFixed(1),
        speed: formatFooterSpeed(activeExecution.progress.instantSpeedMBPerSecond),
        eta: formatRemainingSeconds(activeExecution.progress.estimatedRemainingSeconds),
        error: activeExecution.progress.highestChannelErrorRate?.displayValue ?? '-',
        remaining: formatRemainingBytes(activeExecution.progress.remainingBytes),
    });
});
</script>

<template>
    <n-layout :has-sider="!isMobilePortrait" position="absolute" class="main-shell">
        <left-panel
            v-if="!isMobilePortrait"
            :selected-key="selectedMenuKey"
            @select="handleMenuSelect"
        />
        <n-layout class="content-shell">
            <div v-if="isMobilePortrait" class="mobile-topbar">
                <n-button quaternary circle class="mobile-menu-button" @click="openMenuDrawer">
                    <template #icon>
                        <n-icon>
                            <MenuOutline />
                        </n-icon>
                    </template>
                </n-button>
                <div class="mobile-topbar-title">{{ currentPageTitle }}</div>
            </div>
            <n-layout position="absolute" :style="contentLayoutStyle">
                <keep-alive :include="keepAliveInclude">
                    <component :is="currentPageComponent" v-bind="currentPageProps" />
                </keep-alive>
            </n-layout>
            <n-layout-footer bordered position="absolute" style="height: 32px; padding: 5px">
                {{ footerText }}
            </n-layout-footer>
        </n-layout>
    </n-layout>
    <n-drawer v-model:show="isMenuDrawerOpen" placement="left" :width="260">
        <n-drawer-content :title="t('app.navigation')" closable body-content-style="padding: 0;">
            <left-panel embedded :selected-key="selectedMenuKey" @select="handleMenuSelect" />
        </n-drawer-content>
    </n-drawer>
</template>

<style scoped>
.main-shell {
    inset: 0;
    width: 100%;
    height: 100vh;
    min-height: 100vh;
}

.content-shell {
    height: 100%;
    min-height: 100vh;
}

.mobile-topbar {
    position: absolute;
    top: 0;
    left: 0;
    right: 0;
    height: 48px;
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 0 12px;
    border-bottom: 1px solid rgba(148, 163, 184, 0.2);
    background: rgba(255, 255, 255, 0.92);
    backdrop-filter: blur(10px);
    z-index: 20;
}

.mobile-menu-button {
    flex: 0 0 auto;
}

.mobile-topbar-title {
    min-width: 0;
    font-size: 14px;
    font-weight: 600;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
}

:global(.n-config-provider[data-theme='dark']) .mobile-topbar {
    background: rgba(24, 24, 28, 0.92);
}

@supports (height: 100dvh) {
    .main-shell {
        height: 100dvh;
        min-height: 100dvh;
    }

    .content-shell {
        min-height: 100dvh;
    }
}
</style>
