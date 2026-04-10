<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { NLayout, NLayoutFooter } from 'naive-ui';
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

const { t } = useI18n();
const fileStore = useFileStore();
const selectedMenuKey = ref<string>('overview');
const keepAliveInclude = ['LocalIndex', 'AIChat'];

function handleMenuSelect(key: string) {
    selectedMenuKey.value = key;
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

watch(
    selectedTapeDriveId,
    value => {
        fileStore.setCurrentTapeDriveId(value ?? '');
    },
    { immediate: true },
);
</script>

<template>
    <n-layout has-sider position="absolute">
        <left-panel :selected-key="selectedMenuKey" @select="handleMenuSelect" />
        <n-layout>
            <n-layout position="absolute" style="top: 0; bottom: 32px">
                <keep-alive :include="keepAliveInclude">
                    <component :is="currentPageComponent" v-bind="currentPageProps" />
                </keep-alive>
            </n-layout>
            <n-layout-footer bordered position="absolute" style="height: 32px; padding: 5px">
                {{ t('app.footer') }}
            </n-layout-footer>
        </n-layout>
    </n-layout>
</template>

<style scoped></style>
