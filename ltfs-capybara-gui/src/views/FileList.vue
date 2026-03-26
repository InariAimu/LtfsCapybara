<script setup lang="ts">
import type { DataTableColumns } from 'naive-ui';
import { NDataTable } from 'naive-ui';
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import { useFileStore } from '@/stores/fileStore';

interface Row {
    key: number;
    name: string;
    size: number;
    index: number;
    crc64: string;
    createTime: string;
    modifyTime: string;
    updateTime: string;
}

const { t } = useI18n();

const columns = computed<DataTableColumns<Row>>(() => [
    {
        type: 'selection',
    },
    {
        title: t('table.name'),
        key: 'name',
        width: 150,
        fixed: 'left',
        ellipsis: {
            tooltip: true,
        },
        resizable: true,
    },
    {
        title: t('table.size'),
        key: 'size',
        width: 100,
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
    },
    {
        title: t('table.createTime'),
        key: 'createTime',
        width: 160,
        ellipsis: true,
        resizable: true,
    },
    {
        title: t('table.modifyTime'),
        key: 'modifyTime',
        width: 160,
        ellipsis: true,
        resizable: true,
    },
    {
        title: t('table.updateTime'),
        key: 'updateTime',
        width: 160,
        ellipsis: true,
        resizable: true,
    },
]);

const store = useFileStore();
</script>

<template>
    <n-data-table :size="'medium'" :columns="columns" :data="store.files" />
</template>

<style>
.n-data-table-wrapper {
    overflow: auto auto !important;
}
</style>
