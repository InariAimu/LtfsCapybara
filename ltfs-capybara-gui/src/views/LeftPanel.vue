<script setup lang="ts">
import type { MenuOption } from 'naive-ui';
import type { Component } from 'vue';
import {
    Home,
    SettingsOutline,
    FileTrayFullSharp,
    FileTraySharp,
    FileTrayStackedSharp,
    SwapVerticalOutline,
    DocumentTextSharp,
    Fish,
    BeakerOutline,
} from '@vicons/ionicons5';
import { NIcon, NMenu, NLayoutSider } from 'naive-ui';
import { h, ref, computed, onMounted } from 'vue';
import { useI18n } from 'vue-i18n';
import { tapeDriveApi } from '@/api/modules/tapedrives';

function renderIcon(icon: Component) {
    return () => h(NIcon, null, { default: () => h(icon) });
}

interface Props {
    selectedKey?: string;
}

const props = withDefaults(defineProps<Props>(), {
    selectedKey: 'overview',
});

const emit = defineEmits<{
    select: [key: string];
}>();

const { t } = useI18n();

type FlatMenuOption = {
    label: string;
    key: string;
    disabled?: boolean;
};

const tapeMachineChildren = ref<FlatMenuOption[]>([]);

async function loadTapeDrives() {
    try {
        const response = await tapeDriveApi.list();
        const drives = response.data ?? [];
        tapeMachineChildren.value = drives.map(drive => ({
            label: drive.displayName || drive.devicePath,
            key: `tape-machine:${drive.id}`,
        }));
    } catch (err) {
        console.error('Failed to load tape drives', err);
        tapeMachineChildren.value = [];
    }

    if (tapeMachineChildren.value.length === 0) {
        tapeMachineChildren.value = [
            {
                label: t('menu.noTapeDrives'),
                key: 'tape-machine:none',
                disabled: true,
            },
        ];
    }
}

const menuOptions = computed<MenuOption[]>(() => [
    {
        label: t('menu.overview'),
        key: 'overview',
        icon: renderIcon(Home),
    },
    {
        label: t('menu.tapeMachine'),
        key: 'tape-machine-operations',
        icon: renderIcon(FileTrayFullSharp),
        children: tapeMachineChildren.value,
    },
    {
        label: t('menu.tapeLibrary'),
        key: 'tape-library',
        icon: renderIcon(FileTrayStackedSharp),
        disabled: true,
    },
    {
        label: t('menu.ltfs'),
        key: 'ltfs',
        icon: renderIcon(FileTraySharp),
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
        label: t('menu.aiChat'),
        key: 'ai-chat',
        icon: renderIcon(Fish),
    },
    {
        label: t('menu.test'),
        key: 'test',
        icon: renderIcon(BeakerOutline),
    },
    {
        label: t('menu.settings'),
        key: 'settings',
        icon: renderIcon(SettingsOutline),
    },
]);

const inverted = ref(true);

onMounted(() => {
    void loadTapeDrives();
});

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
