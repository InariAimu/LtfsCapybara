<script setup lang="ts">
import { computed, ref } from 'vue';
import type { PartitionInfo, UsageInfo, WrapInfo, WrapTableRow } from '@/api/types/tapeInfo';
import TapeMetadataCard from '@/views/tape-info/TapeMetadataCard.vue';
import TapePartitionsCard from '@/views/tape-info/TapePartitionsCard.vue';
import TapeUsageCard from '@/views/tape-info/TapeUsageCard.vue';
import TapeWrapAnalysisCard from '@/views/tape-info/TapeWrapAnalysisCard.vue';
import { useTapeInfo } from '@/composables/useTapeInfo';
import { particleTypeMap } from '@/constants/tape';
import { useFileStore } from '@/stores/fileStore';
import { getCapacityCellBackground } from '@/utils/tapeCapacityColor';
import { formatRelativeAgeFromNow, getRelativeAgeColor } from '@/utils/tapeDateSemantic';
import { getLtoFormatStyle } from '@/utils/tapeFormatStyle';

type PartitionBar = {
    key: number;
    label: string;
    used: number;
    loss: number;
    allocated: number;
    usedPercent: number;
    lossPercent: number;
};

const store = useFileStore();
const hideSensitive = ref(false);
const { tapeInfo, loading } = useTapeInfo(() => store.currentTapeName);

const metaViewModel = computed(() => {
    const info = tapeInfo.value;
    if (!info) {
        return {
            particleTypeLabel: particleTypeMap[0],
            mfgAgeText: '',
            mfgAgeColor: '',
            formatStyle: getLtoFormatStyle(undefined, undefined),
        };
    }

    return {
        particleTypeLabel: particleTypeMap[info.manufacturer.particleType] ?? particleTypeMap[0],
        mfgAgeText: formatRelativeAgeFromNow(info.manufacturer.mfgDate),
        mfgAgeColor: getRelativeAgeColor(info.manufacturer.mfgDate),
        formatStyle: getLtoFormatStyle(info.manufacturer.format, info.manufacturer.tapeVendor),
    };
});

const usageMetrics = computed(() => {
    const info = tapeInfo.value;
    const usageValues = info ? Object.values(info.usages) : [];
    const usageInfo: UsageInfo | null = usageValues.length > 0 ? usageValues[0] : null;

    if (!info || !usageInfo) {
        return {
            usageInfo,
            totalWrite: 0,
            totalRead: 0,
            fveText: '0.00',
            hasFatalSuspendedWrites: false,
        };
    }

    const totalWrite = usageInfo.lifeSetsWritten * info.manufacturer.kBytesPerSet * 1024;
    const totalRead = usageInfo.lifeSetsRead * info.manufacturer.kBytesPerSet * 1024;
    const fullVolumeSize =
        info.manufacturer.kBytesPerSet *
        1024 *
        info.manufacturer.tapePhysicInfo.nWraps *
        info.manufacturer.tapePhysicInfo.setsPerWrap;

    const fveText =
        fullVolumeSize <= 0
            ? '0.00'
            : (() => {
                  const fveSize = (totalWrite + totalRead) / fullVolumeSize;
                  return `${fveSize.toFixed(2)} ( ${((fveSize / 260) * 100).toFixed(2)}% )`;
              })();

    return {
        usageInfo,
        totalWrite,
        totalRead,
        fveText,
        hasFatalSuspendedWrites: Number(usageInfo.lifeFatalSusWrites ?? 0) > 0,
    };
});

const partitionMetrics = computed(() => {
    const info = tapeInfo.value;
    if (!info) {
        return {
            bars: [] as PartitionBar[],
            totalCapacity: 0,
            estimatedCapacityLoss: 0,
            partitionCount: 0,
        };
    }

    const partitions = Object.values(info.partitions);
    let totalCapacity = 0;
    let estimatedCapacityLoss = 0;

    const bars = partitions.map((item: PartitionInfo, idx: number) => {
        const allocated = Math.max(item.allocatedSize, 0);
        const used = Math.max(item.usedSize, 0);
        const loss = Math.max(item.estimatedLossSize, 0);

        totalCapacity += allocated;
        estimatedCapacityLoss += loss;

        let usedPercent = 0;
        let lossPercent = 0;
        if (allocated > 0) {
            usedPercent = (used / allocated) * 100;
            lossPercent = (loss / allocated) * 100;
            const totalPercent = usedPercent + lossPercent;
            if (totalPercent > 100) {
                const scale = 100 / totalPercent;
                usedPercent *= scale;
                lossPercent *= scale;
            }
        }

        return {
            key: idx,
            label: `[${item.wrapCount} wraps]`,
            used,
            loss,
            allocated,
            usedPercent,
            lossPercent,
        };
    });

    return {
        bars,
        totalCapacity,
        estimatedCapacityLoss,
        partitionCount: partitions.length,
    };
});

const wrapMetrics = computed(() => {
    const info = tapeInfo.value;
    if (!info) {
        return {
            rows: [] as WrapTableRow[],
            segments: [] as Array<{ key: number; backgroundColor?: string }>,
        };
    }

    const setsPerWrap = info.manufacturer.tapePhysicInfo.setsPerWrap;
    const rows = (info.wraps ?? []).map((item: WrapInfo, idx: number) => {
        const key = item.index ?? idx;
        return {
            key,
            wrap: key,
            startBlock: item.startBlock,
            endBlock: item.endBlock,
            filemark: item.fileMarkCount,
            set: `${item.set} / ${setsPerWrap}`,
            capacity:
                item.capacity > 0
                    ? `${item.capacity.toFixed(2)}%`
                    : item.type === 2
                      ? 'EOD'
                      : item.type === 3
                        ? 'GUARD'
                        : '0',
            rawCapacity: item.capacity,
            rawType: item.type,
            backgroundColor: getCapacityCellBackground(item.capacity, item.type),
        };
    });

    return {
        rows,
        segments: rows.map(item => ({
            key: item.key,
            backgroundColor: item.backgroundColor,
        })),
    };
});
</script>

<template>
    <div class="tape-info">
        <tape-metadata-card
            :tape-info="tapeInfo"
            :meta="metaViewModel"
            v-model:hide-sensitive="hideSensitive"
        />
        <tape-usage-card :usage="usageMetrics" v-model:hide-sensitive="hideSensitive" />
        <tape-partitions-card :partition="partitionMetrics" />
        <tape-wrap-analysis-card :loading="loading" :wrap="wrapMetrics" />
    </div>
</template>

<style scoped>
.tape-info {
    height: 100%;
    padding: 0;
    box-sizing: border-box;
}
</style>
