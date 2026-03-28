<script setup lang="ts">
import type { DataTableColumns } from 'naive-ui';
import { NCard, NDataTable } from 'naive-ui';
import type { WrapTableRow } from '@/api/types/tapeInfo';

interface WrapColorSegment {
    key: number;
    backgroundColor?: string;
}

interface WrapMetrics {
    rows: WrapTableRow[];
    segments: WrapColorSegment[];
}

interface Props {
    loading: boolean;
    wrap: WrapMetrics;
}

const props = defineProps<Props>();

const columns: DataTableColumns<WrapTableRow> = [
    { title: 'Wrap', key: 'wrap' },
    { title: 'Start Block', key: 'startBlock' },
    { title: 'End Block', key: 'endBlock' },
    { title: 'Filemark', key: 'filemark' },
    {
        title: 'Set',
        key: 'set',
        cellProps: (row: WrapTableRow) => ({
            style: {
                backgroundColor: row.backgroundColor,
            },
        }),
    },
    {
        title: 'Capacity',
        key: 'capacity',
        cellProps: (row: WrapTableRow) => ({
            style: {
                backgroundColor: row.backgroundColor,
            },
        }),
    },
];
</script>

<template>
    <n-card title="Wrap Analysis" size="small" class="tape-info-card">
        <div
            v-if="props.wrap.segments.length"
            class="wrap-colorbar"
            aria-label="Wrap capacity colorbar"
        >
            <div
                v-for="segment in props.wrap.segments"
                :key="segment.key"
                class="wrap-colorbar-segment"
                :style="{ backgroundColor: segment.backgroundColor }"
            />
        </div>
        <n-data-table
            :columns="columns"
            :data="props.wrap.rows"
            :loading="loading"
            :striped="true"
        />
    </n-card>
</template>

<style scoped>
.tape-info-card {
    margin-bottom: 0;
}

.wrap-colorbar {
    display: flex;
    height: 24px;
    margin-bottom: 16px;
    border: 1px solid #d9d9d9;
    border-radius: 3px;
    overflow: hidden;
    background-color: #f5f5f5;
}

.wrap-colorbar-segment {
    flex: 1;
    min-width: 0;
    border-right: 1px solid rgba(0, 0, 0, 0.08);
}

.wrap-colorbar-segment:last-child {
    border-right: none;
}
</style>
