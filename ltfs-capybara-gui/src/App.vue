<script setup lang="ts">
import { NMessageProvider } from 'naive-ui';
import { NConfigProvider, GlobalThemeOverrides } from 'naive-ui';
import { computed } from 'vue';
import { dateEnUS, dateZhCN, enUS, zhCN } from 'naive-ui';
import { useI18n } from 'vue-i18n';
import Main from '@/views/Main.vue';

const { locale } = useI18n();

const naiveLocale = computed(() => (locale.value === 'zh-CN' ? zhCN : enUS));
const naiveDateLocale = computed(() => (locale.value === 'zh-CN' ? dateZhCN : dateEnUS));

const themeOverrides: GlobalThemeOverrides = {
    Collapse: {
        itemMargin: '3 0 0 0',
    },
    Tree: {
        lineHeight: '1.0',
        fontSize: '12px',
        nodeWrapperPadding: '2px 0',
        nodeHeight: '28px',
    },
    DataTable: {
        lineHeight: '1.1',
        fontSizeMedium: '12px',
        thPaddingMedium: '8px',
        tdPaddingMedium: '8px',
    },
    Table: {
        lineHeight: '1.1',
        fontSizeMedium: '12px',
        thPaddingMedium: '8px',
        tdPaddingMedium: '8px',
    },
};
</script>

<template>
    <n-config-provider
        :theme-overrides="themeOverrides"
        :locale="naiveLocale"
        :date-locale="naiveDateLocale"
    >
        <n-message-provider> <Main /> </n-message-provider
    ></n-config-provider>
</template>

<style scoped></style>
