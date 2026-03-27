<script setup lang="ts">
import type { DataTableColumns } from 'naive-ui';
import { computed, onBeforeUnmount, ref, watch } from 'vue';
import { NCard, NDataTable, NEmpty, NTable } from 'naive-ui';
import { localCmApi } from '@/api/modules/localcm';
import type { TapeInfo, WrapInfo, WrapTableRow } from '@/api/types/tapeInfo';
import { particleTypeMap } from '@/constants/tape';
import { useFileStore } from '@/stores/fileStore';
import { getCapacityCellBackground } from '@/utils/tapeCapacityColor';
import { formatRelativeAgeFromNow, getRelativeAgeColor } from '@/utils/tapeDateSemantic';
import { getLtoFormatStyle } from '@/utils/tapeFormatStyle';

const store = useFileStore();

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

const wrapData = ref<WrapTableRow[]>([]);
const tapeInfo = ref<TapeInfo | null>(null);
const activeRequestId = ref(0);

const wrapColorSegments = computed(() => {
    return wrapData.value.map(item => ({
        key: item.key,
        backgroundColor: item.backgroundColor,
    }));
});

const formatStyle = computed(() => {
    return getLtoFormatStyle(
        tapeInfo.value?.manufacturer.format,
        tapeInfo.value?.manufacturer.tapeVendor,
    );
});

const loading = ref(false);

const tapeMfgDateAgo = computed(() => {
    return formatRelativeAgeFromNow(tapeInfo.value?.manufacturer.mfgDate);
});

const tapeMfgDateAgoColor = computed(() => {
    return getRelativeAgeColor(tapeInfo.value?.manufacturer.mfgDate);
});

async function refreshTapeInfo() {
    const requestId = ++activeRequestId.value;
    const tapeName = store.currentTapeName;
    if (!tapeName) {
        tapeInfo.value = null;
        wrapData.value = [];
        loading.value = false;
        return;
    }

    const displayCapacity = (info: WrapInfo) => {
        if (info.capacity > 0) {
            return `${info.capacity.toFixed(2)}%`;
        } else {
            if (info.type === 2) {
                return 'EOD';
            } else if (info.type === 3) {
                return 'GUARD';
            } else {
                return '0';
            }
        }
    };

    const displaySet = (set: number) => {
        return `${set} / ${tapeInfo.value?.manufacturer.tapePhysicInfo.setsPerWrap ?? '?'}`;
    };

    loading.value = true;
    try {
        const res = await localCmApi.get(tapeName);
        if (requestId !== activeRequestId.value) {
            return;
        }

        tapeInfo.value = res?.data ?? null;
        const wraps = tapeInfo.value?.wraps ?? [];
        wrapData.value = wraps.map((item: WrapInfo, idx: number) => ({
            key: item.index ?? idx,
            wrap: item.index ?? idx,
            startBlock: item.startBlock ?? '',
            endBlock: item.endBlock ?? '',
            filemark: item.fileMarkCount ?? '',
            set: displaySet(item.set ?? 0),
            capacity: displayCapacity(item),
            rawCapacity: item.capacity,
            rawType: item.type,
            backgroundColor: getCapacityCellBackground(item.capacity, item.type),
        }));
    } catch (err) {
        if (requestId !== activeRequestId.value) {
            return;
        }
        console.error('refreshTapeInfo error', err);
        tapeInfo.value = null;
        wrapData.value = [];
    } finally {
        if (requestId === activeRequestId.value) {
            loading.value = false;
        }
    }
}

watch(
    () => store.currentTapeName,
    () => {
        refreshTapeInfo();
    },
    { immediate: true },
);

onBeforeUnmount(() => {
    activeRequestId.value += 1;
});
</script>

<template>
    <div class="tape-info">
        <n-card title="Application Info" size="small" class="tape-info-card">
            <n-empty description="No data available" />
        </n-card>
        <n-card title="Medium Usage" size="small" class="tape-info-card">
            <n-empty description="No data available" />
        </n-card>
        <n-card title="Medium Identity" size="small" class="tape-info-card">
            <n-table striped>
                <tbody>
                    <tr>
                        <td style="width: 40%;">Format</td>
                        <td>
                            <div class="format-cell">
                                <span>{{ tapeInfo?.manufacturer.format ?? '' }}</span>
                                <span
                                    v-if="formatStyle.color"
                                    class="format-color-swatch"
                                    :class="{ 'format-color-swatch-worm': formatStyle.isWorm }"
                                    :style="{
                                        backgroundColor: formatStyle.color,
                                        '--worm-corner-color': formatStyle.wormCornerColor,
                                    }"
                                    aria-label="LTO format color"
                                />
                            </div>
                        </td>
                    </tr>
                    <tr>
                        <td>Serial Number</td>
                        <td>{{ tapeInfo?.manufacturer.cartridgeSN ?? '' }}</td>
                    </tr>
                    <tr>
                        <td>Tape Vendor</td>
                        <td>{{ tapeInfo?.manufacturer.tapeVendor ?? '' }}</td>
                    </tr>
                    <tr>
                        <td>Tape mfg date</td>
                        <td>
                            {{ tapeInfo?.manufacturer.mfgDate ?? '' }}
                            <span
                                v-if="tapeMfgDateAgo"
                                :style="{ color: tapeMfgDateAgoColor, paddingLeft: '10px' }"
                            >
                                {{ tapeMfgDateAgo }}
                            </span>
                        </td>
                    </tr>
                    <tr>
                        <td>Media Vendor</td>
                        <td>{{ tapeInfo?.mediaManufacturer.vendor ?? '' }}</td>
                    </tr>
                    <tr>
                        <td>Media mfg date</td>
                        <td>{{ tapeInfo?.mediaManufacturer.mfgDate ?? '' }}</td>
                    </tr>
                    <tr>
                        <td>Particle Type</td>
                        <td>{{ particleTypeMap[tapeInfo?.manufacturer.particleType ?? 0] }}</td>
                    </tr>
                </tbody>
            </n-table>
        </n-card>
        <n-card title="Data on Tape" size="small" class="tape-info-card">
            <n-table striped>
                <tbody>
                    <tr>
                        <td style="width: 40%;">Total Partitions</td>
                        <td>{{ Object.keys(tapeInfo?.eoDs ?? {}).length }}</td>
                    </tr>
                </tbody>
            </n-table>
        </n-card>
        <n-card title="Wrap Analysis" size="small" class="tape-info-card">
            <div
                v-if="wrapColorSegments.length"
                class="wrap-colorbar"
                aria-label="Wrap capacity colorbar"
            >
                <div
                    v-for="segment in wrapColorSegments"
                    :key="segment.key"
                    class="wrap-colorbar-segment"
                    :style="{ backgroundColor: segment.backgroundColor }"
                />
            </div>
            <n-data-table :columns="columns" :data="wrapData" :loading="loading" />
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
    margin-bottom: 5px;
}

.format-cell {
    display: inline-flex;
    align-items: center;
    gap: 8px;
}

.format-color-swatch {
    position: relative;
    width: 14px;
    height: 14px;
    border: 1px solid rgba(0, 0, 0, 0.2);
    border-radius: 2px;
    flex-shrink: 0;
}

.format-color-swatch-worm::after {
    content: '';
    position: absolute;
    right: 0;
    bottom: 0;
    width: 0;
    height: 0;
    border-style: solid;
    border-width: 0 0 7px 7px;
    border-color: transparent transparent var(--worm-corner-color, #9aa0a6) transparent;
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
