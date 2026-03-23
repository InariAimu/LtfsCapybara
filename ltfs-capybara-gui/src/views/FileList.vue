<script setup lang="ts">
import type { DataTableBaseColumn, DataTableColumns, DataTableFilterState } from 'naive-ui';
import { NDataTable } from 'naive-ui';
import { reactive } from 'vue';

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
    },
    {
        title: 'Create Time',
        key: 'createTime',
        width: 150,
    },
    {
        title: 'Modify Time',
        key: 'modifyTime',
        width: 150,
    },
    {
        title: 'Update Time',
        key: 'updateTime',
        width: 150,
    },
]);

const data = [
    {
        key: 1,
        name: 'file1.txt',
        size: 1024,
        index: 0,
        crc64: '1234567890abcdef',
        createTime: '2024-01-01 12:00:00',
        modifyTime: '2024-01-02 12:00:00',
        updateTime: '2024-01-03 12:00:00',
    },
    {
        key: 2,
        name: 'file2.txt',
        size: 2048,
        index: 1,
        crc64: 'abcdef1234567890',
        createTime: '2024-01-04 12:00:00',
        modifyTime: '2024-01-05 12:00:00',
        updateTime: '2024-01-06 12:00:00',
    },
];
</script>

<template>
    <n-data-table :size="'medium'" :columns="columns" :data="data" />
</template>
