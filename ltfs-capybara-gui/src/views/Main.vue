<script setup lang="ts">
import { computed, ref } from 'vue';
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

const { t } = useI18n();
const selectedMenuKey = ref<string>('overview');

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
        case 'tape-library':
            return TapeLibrary;
        case 'ltfs':
        case 'ltfs01l6':
        case 'ltfs01l7':
            return Ltfs;
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
</script>

<template>
    <n-layout has-sider position="absolute">
        <left-panel :selected-key="selectedMenuKey" @select="handleMenuSelect" />
        <n-layout>
            <n-layout position="absolute" style="top: 0; bottom: 32px">
                <keep-alive include="LocalIndex">
                    <component
                        v-if="currentPageComponent === TapeMachine"
                        :is="currentPageComponent"
                        :tape-drive-id="selectedTapeDriveId"
                    />
                    <component v-else :is="currentPageComponent" />
                </keep-alive>
            </n-layout>
            <n-layout-footer bordered position="absolute" style="height: 32px; padding: 5px">
                {{ t('app.footer') }}
            </n-layout-footer>
        </n-layout>
    </n-layout>
</template>

<style scoped></style>
