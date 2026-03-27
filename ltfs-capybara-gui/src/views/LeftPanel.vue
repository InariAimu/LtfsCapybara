<script setup lang="ts">
import type { MenuOption } from 'naive-ui';
import type { Component } from 'vue';
import {
    SettingsOutline,
    FileTrayFullSharp,
    FileTraySharp,
    FileTrayStackedSharp,
    SwapVerticalOutline,
    DocumentTextSharp,
    FishSharp,
} from '@vicons/ionicons5';
import { NIcon, NMenu, NLayoutSider } from 'naive-ui';
import { h, ref, computed } from 'vue';
import { useI18n } from 'vue-i18n';

function renderIcon(icon: Component) {
    return () => h(NIcon, null, { default: () => h(icon) });
}

interface Props {
    selectedKey?: string;
}

const props = withDefaults(defineProps<Props>(), {
    selectedKey: 'localindex',
});

const emit = defineEmits<{
    select: [key: string];
}>();

const { t } = useI18n();

const menuOptions = computed<MenuOption[]>(() => [
    {
        label: t('menu.tapeMachine'),
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
        label: t('menu.tapeLibrary'),
        key: 'tape-library',
        icon: renderIcon(FileTrayStackedSharp),
    },
    {
        label: t('menu.ltfs'),
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
        label: t('menu.localIndex'),
        key: 'localindex',
        icon: renderIcon(DocumentTextSharp),
    },
    {
        label: t('menu.task'),
        key: 'task',
        icon: renderIcon(SwapVerticalOutline),
    },
    {
        label: t('menu.settings'),
        key: 'settings',
        icon: renderIcon(SettingsOutline),
    },
]);

const inverted = ref(true);

function handleMenuSelect(key: string) {
    emit('select', key);
}
</script>

<template>
    <n-layout-sider bordered :collapsed-width="48" :width="180" :inverted="inverted">
        <n-menu
            :indent="16"
            :inverted="inverted"
            :collapsed-width="64"
            :collapsed-icon-size="22"
            :options="menuOptions"
            :value="props.selectedKey"
            @update:value="handleMenuSelect"
        />
    </n-layout-sider>
</template>

<style scoped></style>
