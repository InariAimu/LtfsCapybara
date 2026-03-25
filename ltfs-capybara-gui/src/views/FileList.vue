<script setup lang="ts">
import type { DataTableBaseColumn, DataTableColumns, DataTableFilterState } from 'naive-ui';
import { NDataTable } from 'naive-ui';
import { reactive, computed } from 'vue';
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

const columns = reactive<DataTableColumns<Row>>([
    {
        type: 'selection',
    },
    {
        title: 'Name',
        key: 'name',
        width: 150,
        fixed: 'left',
        ellipsis: true,
        resizable: true,
    },
    {
        title: 'Size',
        key: 'size',
        width: 100,
    },
    {
        title: 'Index',
        key: 'index',
        width: 60,
    },
    {
        title: 'CRC64',
        key: 'crc64',
        width: 150,
        ellipsis: true,
    },
    {
        title: 'Create Time',
        key: 'createTime',
        width: 160,
        ellipsis: true,
        resizable: true,
    },
    {
        title: 'Modify Time',
        key: 'modifyTime',
        width: 160,
        ellipsis: true,
        resizable: true,
    },
    {
        title: 'Update Time',
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
