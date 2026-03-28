<script setup lang="ts">
import { computed } from 'vue';
import { NCard, NTable, NTag } from 'naive-ui';
import { formatFileSize } from '@/utils/formatFileSize';

interface PartitionBar {
    key: number;
    label: string;
    used: number;
    loss: number;
    allocated: number;
    usedPercent: number;
    lossPercent: number;
}

interface PartitionMetrics {
    bars: PartitionBar[];
    totalCapacity: number;
    estimatedCapacityLoss: number;
    partitionCount: number;
}

interface Props {
    partition: PartitionMetrics;
}

const props = defineProps<Props>();

const capacityLossPercentText = computed(() => {
    if (props.partition.totalCapacity <= 0) {
        return '( 0.0000% )';
    }

    const percent = (props.partition.estimatedCapacityLoss / props.partition.totalCapacity) * 100;
    return `( ${percent.toFixed(4)}% )`;
});
</script>

<template>
    <n-card title="Data on Tape" size="small" class="tape-info-card">
        <n-table striped>
            <tbody>
                <tr>
                    <td style="width: 40%">Total Partitions</td>
                    <td>{{ props.partition.partitionCount }}</td>
                </tr>
                <tr>
                    <td>Estimated Capacity Loss</td>
                    <td>
                        {{ formatFileSize(props.partition.estimatedCapacityLoss) }}
                        {{ capacityLossPercentText }}
                    </td>
                </tr>
            </tbody>
        </n-table>
        <div v-if="props.partition.bars.length" class="partition-bars">
            <div v-for="item in props.partition.bars" :key="item.key" class="partition-bar-row">
                <div class="partition-bar-label">
                    <n-tag :type="'success'" :size="'tiny'">P{{ item.key }}</n-tag
                    >&nbsp;
                    <span
                        >{{ formatFileSize(item.used) }} /
                        {{ formatFileSize(item.allocated) }}</span
                    >
                    &nbsp;
                    <span style="padding-left: 5px">( {{ item.usedPercent.toFixed(4) }}% )</span>
                    &nbsp;
                    <span style="margin-left: auto"
                        >{{ formatFileSize(item.allocated - item.loss - item.used) }} available
                        {{ item.label }}</span
                    >
                </div>
                <div class="partition-colorbar" aria-label="Partition usage and loss bar">
                    <div
                        class="partition-colorbar-used"
                        :style="{ width: `${item.usedPercent}%` }"
                    />
                    <div
                        class="partition-colorbar-loss"
                        :style="{ width: `${item.lossPercent}%` }"
                    />
                </div>
            </div>
        </div>
    </n-card>
</template>

<style scoped>
.tape-info-card {
    margin-bottom: 0;
}

.partition-bars {
    margin-top: 12px;
}

.partition-bar-row {
    margin-bottom: 10px;
}

.partition-bar-row:last-child {
    margin-bottom: 0;
}

.partition-bar-label {
    display: flex;
    font-size: 12px;
    color: #444;
    margin-bottom: 4px;
}

.partition-colorbar {
    display: flex;
    justify-content: space-between;
    height: 24px;
    border: 1px solid #d9d9d9;
    border-radius: 3px;
    overflow: hidden;
    background-color: #f5f5f5;
}

.partition-colorbar-used {
    height: 100%;
    background-color: #aeedae;
}

.partition-colorbar-loss {
    height: 100%;
    background-image: repeating-linear-gradient(
        135deg,
        #a8a8a8,
        #a8a8a8 6px,
        #d3d3d3 6px,
        #d3d3d3 12px
    );
}
</style>
