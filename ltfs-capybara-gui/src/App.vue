<script setup lang="ts">
import { NMessageProvider } from 'naive-ui';
import { NConfigProvider, GlobalThemeOverrides } from 'naive-ui';
import Main from '@/views/Main.vue';

const themeOverrides: GlobalThemeOverrides = {
    Collapse: {
        itemMargin: '3 0 0 0',
    },
};
</script>

<template>
    <n-config-provider :theme-overrides="themeOverrides">
        <n-message-provider> <Main /> </n-message-provider
    ></n-config-provider>
</template>

<style scoped></style>
