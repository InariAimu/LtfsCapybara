<script setup lang="ts">
import { ref } from 'vue';
import {
    NButton,
    NButtonGroup,
    NLayout,
    NLayoutHeader,
    NLayoutSider,
    NLayoutFooter,
    useMessage,
} from 'naive-ui';

import { invoke } from '@tauri-apps/api/core';

import LeftPanel from './LeftPanel.vue';
import FileList from './FileList.vue';

const greetMsg = ref('');

const message = useMessage();

async function greet() {
    // Learn more about Tauri commands at https://tauri.app/develop/calling-rust/
    greetMsg.value = await invoke('greet', { name: 'Meow!' });
    message.info(greetMsg.value);
}
</script>

<template>
    <n-layout has-sider position="absolute">
        <left-panel />
        <n-layout>
            <n-layout-header style="padding: 0px" bordered>
                <n-button-group>
                    <n-button @click="greet"> Cat </n-button>
                    <n-button @click="greet"> Dog </n-button>
                    <n-button @click="greet"> Bear </n-button>
                </n-button-group>
            </n-layout-header>
            <n-layout has-sider position="static">
                <n-layout-sider bordered content-style="padding: 10px;">
                    <file-list />
                </n-layout-sider>
                <n-layout content-style="padding: 24px;"> Cat </n-layout>
            </n-layout>
            <n-layout-footer bordered position="absolute" style="height: 32px; padding: 5px">
                Cat
            </n-layout-footer>
        </n-layout>
    </n-layout>
</template>

<style scoped></style>
