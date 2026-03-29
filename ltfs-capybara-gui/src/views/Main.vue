<script setup lang="ts">
import { computed, ref } from 'vue';
import { NButton, NButtonGroup, NLayout, NLayoutHeader, NLayoutFooter, useMessage } from 'naive-ui';
import { useI18n } from 'vue-i18n';

import { invoke } from '@tauri-apps/api/core';

import LeftPanel from './LeftPanel.vue';
import Overview from './Overview.vue';
import Settings from './Settings.vue';
import LocalIndex from './LocalIndex.vue';
import TapeMachine from './TapeMachine.vue';
import TapeLibrary from './TapeLibrary.vue';
import Ltfs from './Ltfs.vue';
import Task from './Task.vue';

const greetMsg = ref('');
const { t } = useI18n();
const selectedMenuKey = ref<string>('overview');

const message = useMessage();

function handleMenuSelect(key: string) {
    selectedMenuKey.value = key;
}

const currentPageComponent = computed(() => {
    switch (selectedMenuKey.value) {
        case 'overview':
            return Overview;
        case 'settings':
            return Settings;
        case 'localindex':
            return LocalIndex;
        case 'task':
            return Task;
        case 'tape-machine-operations':
        case 'tape0':
            return TapeMachine;
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

async function greet() {
    // Learn more about Tauri commands at https://tauri.app/develop/calling-rust/
    greetMsg.value = await invoke('greet', { name: 'Meow!' });
    message.info(greetMsg.value);
}
</script>

<template>
    <n-layout has-sider position="absolute">
        <left-panel :selected-key="selectedMenuKey" @select="handleMenuSelect" />
        <n-layout>
            <n-layout position="absolute" style="top: 0; bottom: 32px">
                <keep-alive include="LocalIndex">
                    <component :is="currentPageComponent" />
                </keep-alive>
            </n-layout>
            <n-layout-footer bordered position="absolute" style="height: 32px; padding: 5px">
                {{ t('app.footer') }}
            </n-layout-footer>
        </n-layout>
    </n-layout>
</template>

<style scoped></style>
