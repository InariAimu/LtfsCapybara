<script setup lang="ts">
import type { MenuOption } from 'naive-ui';
import type { Component } from 'vue';
import {
    SettingsOutline,
    FileTrayFullSharp,
    FileTraySharp,
    FileTrayStackedSharp,
    DocumentTextOutline,
} from '@vicons/ionicons5';
import { NIcon, NMenu, NLayoutSider } from 'naive-ui';
import { h, ref } from 'vue';

function renderIcon(icon: Component) {
    return () => h(NIcon, null, { default: () => h(icon) });
}

const menuOptions: MenuOption[] = [
    {
        label: 'Tape Machine',
        key: 'tape-machine-operations',
        icon: renderIcon(FileTrayFullSharp),
        children: [
            {
                label: 'TAPE0',
                key: 'tape0',
            },
        ],
    },
    {
        label: 'Tape Library',
        key: 'tape-library',
        disabled: true,
        icon: renderIcon(FileTrayStackedSharp),
    },
    {
        label: 'LTFS',
        key: 'ltfs',
        icon: renderIcon(FileTraySharp),
        children: [
            {
                label: 'LTFS01L6',
                key: 'ltfs01l6',
            },
            {
                label: 'LTFS01L7',
                key: 'ltfs01l7',
            },
        ],
    },
    {
        label: 'Local Index',
        key: 'local-index',
        icon: renderIcon(DocumentTextOutline),
        children: [
            {
                label: 'LTFS01L6',
                key: 'ltfs01l6',
            },
            {
                label: 'LTFS01L7',
                key: 'ltfs01l7',
            },
        ],
    },
    {
        label: 'Settings',
        key: 'settings',
        icon: renderIcon(SettingsOutline),
    },
];

const inverted = ref(true);
</script>

<template>
    <n-layout-sider bordered :collapsed-width="48" :width="180" :inverted="inverted">
        <n-menu
            :indent="16"
            :inverted="inverted"
            :collapsed-width="64"
            :collapsed-icon-size="22"
            :options="menuOptions"
        />
    </n-layout-sider>
</template>

<style scoped></style>
