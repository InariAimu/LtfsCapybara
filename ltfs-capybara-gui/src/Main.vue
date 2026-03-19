<script setup lang="ts">
import { ref } from 'vue';
import {
    NButton,
    NButtonGroup,
    NLayout,
    NLayoutHeader,
    NLayoutSider,
    NLayoutFooter,
    NCollapse,
    NCollapseItem,
    NText,
    useMessage,
} from 'naive-ui';

import { invoke } from '@tauri-apps/api/core';

const greetMsg = ref('');

const message = useMessage();

async function greet() {
    // Learn more about Tauri commands at https://tauri.app/develop/calling-rust/
    greetMsg.value = await invoke('greet', { name: 'Meow!' });
    message.info(greetMsg.value);
}
</script>

<template>
    <n-layout position="absolute">
        <n-layout-header style="height: 64px; padding: 24px" bordered>
            <n-button-group>
                <n-button @click="greet"> 猫 </n-button>
                <n-button @click="greet"> 狗 </n-button>
                <n-button @click="greet"> 熊 </n-button>
            </n-button-group>
        </n-layout-header>
        <n-layout has-sider position="absolute" style="top: 64px; bottom: 64px">
            <n-layout-sider bordered content-style="padding: 10px;">
                <n-collapse>
                    <n-collapse-item title="LTFS01L6" name="1">
                        <n-collapse>
                            <n-collapse-item title="LTFS01L6" name="1">
                                2.2TB / 2.2TB
                                <template #header-extra> 2.2TB / 2.2TB</template>
                            </n-collapse-item>
                        </n-collapse>
                        <template #header-extra> 2.2TB / 2.2TB</template>
                    </n-collapse-item>
                </n-collapse>
            </n-layout-sider>
            <n-layout content-style="padding: 24px;"> 猫 </n-layout>
        </n-layout>
        <n-layout-footer bordered position="absolute" style="height: 64px; padding: 24px">
            猫
        </n-layout-footer>
    </n-layout>
</template>

<style scoped></style>
