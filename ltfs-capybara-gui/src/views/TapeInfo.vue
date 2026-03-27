<script setup lang="ts">
import { NCard, NDataTable } from 'naive-ui';

const columns: any[] = [
    { title: 'Wrap', key: 'wrap' },
    { title: 'Start Block', key: 'startBlock' },
    { title: 'End Block', key: 'endBlock' },
    { title: 'Filemark', key: 'filemark' },
    { title: 'Set', key: 'set' },
    { title: 'Capacity', key: 'capacity' },
];
const data: any[] = [];
</script>

<template>
    <div class="tape-info">
        <n-card title="Data on Tape" size="small" class="tape-info-card">
            <n-empty description="No data available" />
        </n-card>
        <n-card title="Wrap Analysis" size="small" class="tape-info-card">
            <n-data-table :columns="columns" :data="data" />
        </n-card>
    </div>
</template>

<style scoped>
.tape-info {
    height: 100%;
    padding: 0;
    box-sizing: border-box;
}

.tape-info-card {
    
}
</style>
