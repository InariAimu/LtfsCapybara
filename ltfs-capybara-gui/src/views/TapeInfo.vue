<script setup lang="ts">
import type { DataTableColumns } from 'naive-ui';
import { computed, onBeforeUnmount, ref, watch } from 'vue';
import { AlertCircleSharp, Warning } from '@vicons/ionicons5';
import { NCard, NDataTable, NIcon, NSwitch, NTable, NTag } from 'naive-ui';
import { localCmApi } from '@/api/modules/localcm';
import type { PartitionInfo, TapeInfo, WrapInfo, WrapTableRow } from '@/api/types/tapeInfo';
import { particleTypeMap } from '@/constants/tape';
import { useFileStore } from '@/stores/fileStore';
import { getCapacityCellBackground } from '@/utils/tapeCapacityColor';
import { formatRelativeAgeFromNow, getRelativeAgeColor } from '@/utils/tapeDateSemantic';
import { getLtoFormatStyle } from '@/utils/tapeFormatStyle';
import { formatFileSize } from '@/utils/formatFileSize';

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
const hideLastDriveSn = ref(false);

const wrapColorSegments = computed(() => {
    return wrapData.value.map(item => ({
        key: item.key,
        backgroundColor: item.backgroundColor,
    }));
});

const formatStyle = computed(() => {
    if (!tapeInfo.value) {
        return getLtoFormatStyle(undefined, undefined);
    }
    return getLtoFormatStyle(
        tapeInfo.value.manufacturer.format,
        tapeInfo.value.manufacturer.tapeVendor,
    );
});

const loading = ref(false);

const tapeMfgDateAgo = computed(() => {
    if (!tapeInfo.value) {
        return '';
    }
    return formatRelativeAgeFromNow(tapeInfo.value.manufacturer.mfgDate);
});

const tapeMfgDateAgoColor = computed(() => {
    if (!tapeInfo.value) {
        return '';
    }
    return getRelativeAgeColor(tapeInfo.value.manufacturer.mfgDate);
});

const partitionBars = computed(() => {
    if (!tapeInfo.value) {
        return [];
    }
    const partitions = Object.values(tapeInfo.value.partitions);

    return partitions.map((item: PartitionInfo, idx: number) => {
        const allocated = Math.max(item.allocatedSize, 0);
        const used = Math.max(item.usedSize, 0);
        const loss = Math.max(item.estimatedLossSize, 0);

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
});

const estimatedCapacityLoss = computed(() => {
    if (!tapeInfo.value) {
        return 0;
    }
    const partitions = Object.values(tapeInfo.value.partitions);
    const totalLoss = partitions.reduce((acc: number, item: PartitionInfo) => {
        return acc + Math.max(item.estimatedLossSize, 0);
    }, 0);
    return totalLoss;
});

const totalCapacity = computed(() => {
    if (!tapeInfo.value) {
        return 0;
    }
    const partitions = Object.values(tapeInfo.value.partitions);
    const totalAllocated = partitions.reduce((acc: number, item: PartitionInfo) => {
        return acc + Math.max(item.allocatedSize, 0);
    }, 0);
    return totalAllocated;
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

    loading.value = true;
    try {
        const res = await localCmApi.get(tapeName);
        if (requestId !== activeRequestId.value) {
            return;
        }

        tapeInfo.value = res?.data ?? null;
        if (!tapeInfo.value) {
            wrapData.value = [];
            return;
        }

        const displaySet = (set: number) => {
            return `${set} / ${tapeInfo.value!.manufacturer.tapePhysicInfo.setsPerWrap}`;
        };

        const wraps = tapeInfo.value?.wraps ?? [];
        wrapData.value = wraps.map((item: WrapInfo, idx: number) => ({
            key: item.index ?? idx,
            wrap: item.index ?? idx,
            startBlock: item.startBlock,
            endBlock: item.endBlock,
            filemark: item.fileMarkCount,
            set: displaySet(item.set),
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

const usageInfo = computed(() => {
    if (!tapeInfo.value) {
        return null;
    }
    const usagePages = tapeInfo.value.usages;
    const usageList = Object.values(usagePages);
    if (usageList.length === 0) {
        return null;
    }
    return usageList[0];
});

const totalWrite = computed(() => {
    if (!tapeInfo.value || !usageInfo.value) {
        return 0;
    }
    return usageInfo.value.lifeSetsWritten * tapeInfo.value.manufacturer.kBytesPerSet * 1024;
});

const totalRead = computed(() => {
    if (!tapeInfo.value || !usageInfo.value) {
        return 0;
    }
    return usageInfo.value.lifeSetsRead * tapeInfo.value.manufacturer.kBytesPerSet * 1024;
});

const fve = computed(() => {
    if (!tapeInfo.value) {
        return '0.00';
    }
    const fullVolumeSize =
        tapeInfo.value.manufacturer.kBytesPerSet *
        1024 *
        tapeInfo.value.manufacturer.tapePhysicInfo.nWraps *
        tapeInfo.value.manufacturer.tapePhysicInfo.setsPerWrap;
    if (fullVolumeSize <= 0) {
        return '0.00';
    }
    const fveSize = (totalWrite.value + totalRead.value) / fullVolumeSize;
    return `${fveSize.toFixed(2)} ( ${((fveSize / 260) * 100).toFixed(2)}% )`;
});

const hasFatalSuspendedWrites = computed(() => {
    return Number(usageInfo.value?.lifeFatalSusWrites ?? 0) > 0;
});

onBeforeUnmount(() => {
    activeRequestId.value += 1;
});
</script>

<template>
    <div class="tape-info">
        <n-card title="Tape Info" size="small" class="tape-info-card">
            <n-table striped>
                <tbody>
                    <tr>
                        <td style="width: 40%">Barcode</td>
                        <td>
                            <div class="usage-value-row">
                            <span
                                class="usage-sensitive-value"
                                :class="{ 'usage-sensitive-value-blurred': hideLastDriveSn }"
                                >{{ tapeInfo?.applicationSpecific.barCode || '' }}</span
                            >
                            <n-switch v-model:value="hideLastDriveSn" size="small" />
                            </div>
                        </td>
                    </tr>
                    <tr>
                        <td>Application</td>
                        <td>
                            <span>{{ tapeInfo?.applicationSpecific.vendor || '' }}</span
                            >&nbsp; <span>{{ tapeInfo?.applicationSpecific.name || '' }}</span
                            >&nbsp;
                            <span>{{ tapeInfo?.applicationSpecific.version || '' }}</span>
                        </td>
                    </tr>
                    <tr>
                        <td>Format</td>
                        <td>
                            <div class="format-cell">
                                <span>{{ tapeInfo?.manufacturer.format || '' }}</span>
                                <n-tag :type="'success'" :size="'tiny'">{{
                                    particleTypeMap[tapeInfo?.manufacturer.particleType ?? 0]
                                }}</n-tag>
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
                        <td>
                            <div class="usage-value-row">
                            <span
                                class="usage-sensitive-value"
                                :class="{ 'usage-sensitive-value-blurred': hideLastDriveSn }"
                                >{{ tapeInfo?.manufacturer.cartridgeSN || '' }}
                            </span>
                            <n-switch v-model:value="hideLastDriveSn" size="small" />
                            </div>
                        </td>
                    </tr>
                    <tr>
                        <td>Tape Vendor</td>
                        <td>
                            <span>{{ tapeInfo?.manufacturer.tapeVendor || '' }}</span
                            >&nbsp;@ <span>{{ tapeInfo?.manufacturer.mfgDate || '' }}</span
                            ><span
                                v-if="tapeMfgDateAgo"
                                :style="{ color: tapeMfgDateAgoColor, paddingLeft: '10px' }"
                            >
                                {{ tapeMfgDateAgo }}
                            </span>
                        </td>
                    </tr>
                    <tr>
                        <td>Media Vendor</td>
                        <td>
                            <span>{{ tapeInfo?.mediaManufacturer.vendor || '' }}</span
                            >&nbsp;@ <span>{{ tapeInfo?.mediaManufacturer.mfgDate || '' }}</span>
                        </td>
                    </tr>
                </tbody>
            </n-table>
        </n-card>
        <n-card title="Medium Usage" size="small" class="tape-info-card">
            <n-table striped>
                <tbody>
                    <tr>
                        <td style="width: 40%">LastDrive SN</td>
                        <td>
                            <div class="usage-value-row">
                                <span
                                    class="usage-sensitive-value"
                                    :class="{ 'usage-sensitive-value-blurred': hideLastDriveSn }"
                                    >{{ usageInfo?.drvSN || '' }}</span
                                >
                                <n-switch v-model:value="hideLastDriveSn" size="small" />
                            </div>
                        </td>
                    </tr>
                    <tr>
                        <td>Load Count</td>
                        <td>
                            {{ usageInfo?.threadCount || '' }}
                        </td>
                    </tr>
                    <tr>
                        <td>Total</td>
                        <td>
                            <n-tag :type="'success'" :size="'tiny'"> Read</n-tag
                            ><span style="margin: 0 16px 0 8px">
                                {{ formatFileSize(totalRead) }}</span
                            >
                            <n-tag :type="'error'" :size="'tiny'"> Write</n-tag>
                            <span style="margin: 0 16px 0 8px">{{
                                formatFileSize(totalWrite)
                            }}</span>
                            <n-tag :type="'info'" :size="'tiny'">FVE</n-tag>
                            <span style="margin: 0 16px 0 8px">{{ fve }}</span>
                        </td>
                    </tr>
                    <tr>
                        <td>RW Retries</td>
                        <td>
                            <n-tag :type="'success'" :size="'tiny'"> Read</n-tag
                            ><span style="margin: 0 16px 0 8px">
                                {{ usageInfo?.lifeReadRetries }}</span
                            >
                            <n-tag :type="'error'" :size="'tiny'"> Write</n-tag>
                            <span style="margin: 0 16px 0 8px">{{
                                usageInfo?.lifeWriteRetries
                            }}</span>
                        </td>
                    </tr>
                    <tr>
                        <td>RW Unrecovered</td>
                        <td>
                            <n-tag :type="'success'" :size="'tiny'"> Read</n-tag
                            ><span style="margin: 0 16px 0 8px">
                                {{ usageInfo?.lifeUnRecovReads }}</span
                            >
                            <n-tag :type="'error'" :size="'tiny'"> Write</n-tag>
                            <span style="margin: 0 16px 0 8px">{{
                                usageInfo?.lifeUnRecovWrites
                            }}</span>
                        </td>
                    </tr>
                    <tr>
                        <td>Suspended Writes / Append</td>
                        <td>
                            {{ usageInfo?.lifeSuspendedWrites }} /
                            {{ usageInfo?.lifeSuspendedAppendWrites }}
                        </td>
                    </tr>
                    <tr>
                        <td>Fatal Suspended Writes</td>
                        <td>
                            <span class="fatal-warning-value">
                                <span>{{ usageInfo?.lifeFatalSusWrites }}</span>
                                <n-icon
                                    v-if="hasFatalSuspendedWrites"
                                    class="fatal-warning-icon"
                                    :size="15"
                                >
                                    <warning />
                                </n-icon>
                            </span>
                        </td>
                    </tr>
                </tbody>
            </n-table>
        </n-card>
        <n-card title="Data on Tape" size="small" class="tape-info-card">
            <n-table striped>
                <tbody>
                    <tr>
                        <td style="width: 40%">Total Partitions</td>
                        <td>{{ Object.keys(tapeInfo?.partitions ?? {}).length }}</td>
                    </tr>
                    <tr>
                        <td>Estimated Capacity Loss</td>
                        <td>
                            {{ formatFileSize(estimatedCapacityLoss) }}
                            {{
                                `( ${((estimatedCapacityLoss / totalCapacity) * 100).toFixed(4)}% )`
                            }}
                        </td>
                    </tr>
                </tbody>
            </n-table>
            <div v-if="partitionBars.length" class="partition-bars">
                <div
                    v-for="partition in partitionBars"
                    :key="partition.key"
                    class="partition-bar-row"
                >
                    <div class="partition-bar-label">
                        <n-tag :type="'success'" :size="'tiny'">P{{ partition.key }}</n-tag
                        >&nbsp;
                        <span
                            >{{ formatFileSize(partition.used) }} /
                            {{ formatFileSize(partition.allocated) }}</span
                        >&nbsp;
                        <span style="padding-left: 5px"
                            >( {{ partition.usedPercent.toFixed(4) }}% )</span
                        >&nbsp;
                        <span style="margin-left: auto"
                            >{{
                                formatFileSize(
                                    partition.allocated - partition.loss - partition.used,
                                )
                            }}
                            available {{ partition.label }}</span
                        >
                    </div>
                    <div class="partition-colorbar" aria-label="Partition usage and loss bar">
                        <div
                            class="partition-colorbar-used"
                            :style="{ width: `${partition.usedPercent}%` }"
                        />
                        <div
                            class="partition-colorbar-loss"
                            :style="{ width: `${partition.lossPercent}%` }"
                        />
                    </div>
                </div>
            </div>
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
            <n-data-table :columns="columns" :data="wrapData" :loading="loading" :striped="true" />
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
    margin-bottom: 0px;
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

.usage-value-row {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 8px;
}

.usage-sensitive-value {
    transition: filter 0.15s ease;
}

.usage-sensitive-value-blurred {
    filter: blur(4px);
}

.fatal-warning-value {
    display: inline-flex;
    align-items: center;
    gap: 6px;
}

.fatal-warning-icon {
    color: #f0ad00;
}
</style>
