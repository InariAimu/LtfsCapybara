<script setup lang="ts">
import { NMessageProvider } from 'naive-ui';
import { NConfigProvider, GlobalThemeOverrides } from 'naive-ui';
import Main from '@/views/Main.vue';

const themeOverrides: GlobalThemeOverrides = {
    Collapse: {
        itemMargin: '3 0 0 0',
    },
    Tree: {
        lineHeight: '1.0',
        fontSize: '13px',
        nodeWrapperPadding: '2px 0',
        nodeHeight: '28px',
    },
    DataTable: {
        lineHeight: '1.0',
        fontSizeMedium: '13px',
        thPaddingMedium: '8px',
        tdPaddingMedium: '8px',
    },
};
</script>

<template>
    <n-config-provider :theme-overrides="themeOverrides">
        <n-message-provider> <Main /> </n-message-provider
    ></n-config-provider>
</template>

<style scoped></style>
