<script setup lang="ts">
import type { DataTableColumns, DataTableInst } from 'naive-ui';
import { NButton, NDataTable } from 'naive-ui';
import { computed, h, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { useFileStore } from '@/stores/fileStore';

interface Row {
    key: number | string;
    name: string;
    size: number | string;
    index: number;
    crc64: string;
    modifyTime: string;
    type?: string;
    task?: string;
}

const { t } = useI18n();

const props = defineProps<{
    deletable?: boolean;
}>();

const emit = defineEmits<{
    (e: 'delete-dir', name: string): void;
}>();

const columns = computed<DataTableColumns<Row>>(() => [
    {
        type: 'selection',
    },
    {
        title: t('table.name'),
        key: 'name',
        width: 250,
        fixed: 'left',
        ellipsis: {
            tooltip: true,
        },
        resizable: true,
    },
    {
        title: t('table.task'),
        key: 'task',
        width: 70,
        // render(row) {
        //     const task = String(row.task || '').toLowerCase();
        //     if (!task) {
        //         return '-';
        //     }

        //     return h(
        //         NTag,
        //         { size: 'tiny', type: task === 'delete' ? 'warning' : 'success' },
        //         { default: () => task },
        //     );
        // },
    },
    {
        title: t('table.size'),
        key: 'size',
        width: 90,
    },
    {
        title: t('table.index'),
        key: 'index',
        width: 60,
    },
    {
        title: t('table.crc64'),
        key: 'crc64',
        width: 150,
        ellipsis: true,
        resizable: true,
    },
    {
        title: t('table.modifyTime'),
        key: 'modifyTime',
        width: 160,
        ellipsis: true,
    },
    {
        title: '',
        key: '_actions',
        width: 75,
        render(row) {
            if (row.type !== 'dir' || !props.deletable || row.task === 'delete') {
                return h('span');
            }
            return h(
                NButton,
                {
                    size: 'tiny',
                    type: 'error',
                    tertiary: true,
                    onClick: () => emit('delete-dir', row.name),
                },
                { default: () => t('task.deleteDir') },
            );
        },
    },
]);

const store = useFileStore();
const tableRef = ref<DataTableInst | null>(null);

function handleCheckedRowKeysUpdate(keys: Array<string | number>) {
    store.setSelectedFileKeys(keys);
}

watch(
    () => store.currentPath,
    () => tableRef.value?.scrollTo({ top: 0 }),
);
</script>

<template>
    <n-data-table
        ref="tableRef"
        size="medium"
        :columns="columns"
        :data="store.files"
        :row-key="(row: Row) => row.key"
        :checked-row-keys="store.selectedFileKeys"
        @update:checked-row-keys="handleCheckedRowKeysUpdate"
    />
</template>

<style>
.n-data-table-wrapper {
    overflow: auto auto !important;
}
</style>
