<script setup lang="ts">
import { computed, h, ref } from 'vue';
import type { DataTableColumns } from 'naive-ui';
import { NCard, NDataTable, NFlex, NIcon, NPopover, NSwitch } from 'naive-ui';
import { useI18n } from 'vue-i18n';
import { AlertCircle } from '@vicons/ionicons5';
import type { WrapTableRow } from '@/api/types/tapeInfo';

interface WrapColorSegment {
    key: number;
    backgroundColor?: string;
}

interface WrapMetrics {
    rows: WrapTableRow[];
    segments: WrapColorSegment[];
}

interface HorizontalSegmentItem {
    segment?: WrapColorSegment;
    isSpace?: boolean;
}

interface Props {
    loading: boolean;
    wrap: WrapMetrics;
}

type DisplayWrapTableRow = WrapTableRow & {
    wrapDisplay?: string;
};

const props = defineProps<Props>();
const { t } = useI18n();
const orderedLayoutEnabled = ref(false);

const forwardSegments = computed(() => props.wrap.segments.filter((_, index) => index % 2 === 0));

const reverseSegments = computed(() => props.wrap.segments.filter((_, index) => index % 2 === 1));

const orderedHorizontalSegments = computed<HorizontalSegmentItem[]>(() => {
    const segments = props.wrap.segments;
    if (segments.length === 0) return [];

    // Divide into 4 groups of equal size
    const groupSize = Math.ceil(segments.length / 4);
    const groups: WrapColorSegment[][] = [];
    for (let i = 0; i < 4; i++) {
        const start = i * groupSize;
        const end = Math.min(start + groupSize, segments.length);
        groups[i] = segments.slice(start, end);
    }

    // Reorder groups from left to right: [3, 1, 0, 2]
    const reorderedGroups = [groups[3], groups[1], groups[0], groups[2]];

    // Process each group: even indices first, then odd indices in reverse.
    const result: HorizontalSegmentItem[] = [];

    reorderedGroups.forEach((group, groupIdx) => {
        // Top half: even indices (0, 2, 4, ...)
        const evenItems = group.filter((_, idx) => idx % 2 === 0);
        evenItems.forEach(item => {
            result.push({ segment: item, isSpace: false });
        });

        // Bottom half: odd indices (1, 3, 5, ...) in reverse order (9, 7, 5, 3, 1)
        const oddItems = group.filter((_, idx) => idx % 2 === 1);
        oddItems.reverse().forEach(item => {
            result.push({ segment: item, isSpace: false });
        });

        // Add 1px space between groups (except after last group)
        if (groupIdx < 3) {
            result.push({ isSpace: true });
        }
    });

    return result;
});

const overallSegments = computed<HorizontalSegmentItem[]>(() => {
    if (!orderedLayoutEnabled.value) {
        return props.wrap.segments.map(segment => ({ segment, isSpace: false }));
    }
    return orderedHorizontalSegments.value;
});

const displayRows = computed<DisplayWrapTableRow[]>(() => {
    const rows = props.wrap.rows;
    if (rows.length <= 1) {
        return rows;
    }

    const isCollapsibleEmptyRow = (row: WrapTableRow) =>
        row.rawCapacity === 0 && row.rawType !== 2 && row.rawType !== 3;

    const hasEodTail = rows[rows.length - 1].rawType === 2;
    const mergeEnd = hasEodTail ? rows.length - 2 : rows.length - 1;
    if (mergeEnd < 0) {
        return rows;
    }

    let tailStart = mergeEnd + 1;
    for (let i = mergeEnd; i >= 0; i -= 1) {
        if (isCollapsibleEmptyRow(rows[i])) {
            tailStart = i;
            continue;
        }
        break;
    }

    const zeroTailCount = mergeEnd - tailStart + 1;
    if (zeroTailCount <= 1) {
        return rows;
    }

    const headRows = rows.slice(0, tailStart);
    const firstTailRow = rows[tailStart];
    const lastTailRow = rows[mergeEnd];
    const mergedTailRow: DisplayWrapTableRow = {
        ...firstTailRow,
        wrapDisplay: `${firstTailRow.wrap}-${lastTailRow.wrap}`,
    };

    const suffixRows = hasEodTail ? [rows[rows.length - 1]] : [];

    return [...headRows, mergedTailRow, ...suffixRows];
});

const columns = computed<DataTableColumns<DisplayWrapTableRow>>(() => [
    {
        title: t('tapeInfo.wrap.wrap'),
        key: 'wrap',
        render: (row: DisplayWrapTableRow) => row.wrapDisplay ?? row.wrap,
    },
    { title: t('tapeInfo.wrap.startBlock'), key: 'startBlock' },
    { title: t('tapeInfo.wrap.endBlock'), key: 'endBlock' },
    { title: t('tapeInfo.wrap.filemark'), key: 'filemark' },
    {
        title: t('tapeInfo.wrap.set'),
        key: 'set',
        cellProps: (row: WrapTableRow) => ({
            style: {
                backgroundColor: row.backgroundColor,
            },
        }),
    },
    {
        title: t('tapeInfo.wrap.capacity'),
        key: 'capacity',
        cellProps: (row: WrapTableRow) => ({
            style: {
                backgroundColor: row.backgroundColor,
            },
        }),
    },
]);

function createTitle(): ReturnType<typeof h> {
    return h(
        NFlex,
        {
            align: 'center',
            style: { gap: '6px' },
        },
        {
            default: () => [
                h('span', t('tapeInfo.wrap.title')),
                h(
                    NPopover,
                    {
                        trigger: 'hover',
                        placement: 'top',
                    },
                    {
                        default: () =>
                            t('tapeInfo.wrap.capacityTooltip')
                                .split('\n')
                                .flatMap((line, i, arr) =>
                                    i < arr.length - 1 ? [line, h('br')] : [line],
                                ),
                        trigger: () =>
                            h(
                                NIcon,
                                {
                                    size: 16,
                                    color: '#4098fc',
                                },
                                {
                                    default: () => h(AlertCircle),
                                },
                            ),
                    },
                ),
            ],
        },
    );
}
</script>

<template>
    <n-card :title="createTitle" size="small" class="tape-info-card">
        <div v-if="props.wrap.segments.length" class="wrap-colorbars">
            <div
                class="wrap-colorbar-group"
                :aria-label="`${t('tapeInfo.wrap.colorbarAriaLabel')} forward`"
            >
                <n-flex align="center" justify="space-between">
                    <span class="colorbar-title"
                        >{{ t('tapeInfo.wrap.overall') }} - {{ props.wrap.segments.length }}
                        {{ t('tapeInfo.wrap.wraps') }}</span
                    >
                    <span class="colorbar-title" style="margin-left: auto"
                        >{{ t('tapeInfo.wrap.vertical') }}</span
                    >
                    <n-switch v-model:value="orderedLayoutEnabled" size="small" />
                </n-flex>
                <div class="wrap-colorbar">
                    <div
                        v-for="(item, idx) in overallSegments"
                        :key="item.isSpace ? `space-${idx}` : item.segment?.key"
                        class="wrap-colorbar-segment"
                        :class="{ 'wrap-colorbar-space': item.isSpace }"
                        :style="{
                            backgroundColor: item.isSpace
                                ? 'transparent'
                                : item.segment?.backgroundColor,
                        }"
                    />
                </div>
            </div>

            <div
                class="wrap-colorbar-group"
                :aria-label="`${t('tapeInfo.wrap.colorbarAriaLabel')} forward`"
            >
                <span class="colorbar-title">{{ t('tapeInfo.wrap.forward') }}</span>
                <div class="wrap-colorbar wrap-colorbar-half">
                    <div
                        v-for="segment in forwardSegments"
                        :key="segment.key"
                        class="wrap-colorbar-segment"
                        :style="{ backgroundColor: segment.backgroundColor }"
                    />
                </div>
            </div>

            <div
                class="wrap-colorbar-group"
                :aria-label="`${t('tapeInfo.wrap.colorbarAriaLabel')} reverse`"
            >
                <span class="colorbar-title">{{ t('tapeInfo.wrap.reverse') }}</span>
                <div class="wrap-colorbar wrap-colorbar-half">
                    <div
                        v-for="segment in reverseSegments"
                        :key="segment.key"
                        class="wrap-colorbar-segment"
                        :style="{ backgroundColor: segment.backgroundColor }"
                    />
                </div>
            </div>
        </div>

        <n-data-table :columns="columns" :data="displayRows" :loading="loading" :striped="true" />
    </n-card>
</template>

<style scoped>
.tape-info-card {
    margin-bottom: 0;
}

.colorbar-title {
    font-size: 12px;
    margin-top: 2px;
    margin-bottom: 2px;
}

.wrap-colorbars {
    display: flex;
    flex-direction: column;
    margin-bottom: 8px;
    gap: 5px;
}

.wrap-colorbar-group {
    display: flex;
    flex-direction: column;
}

.wrap-colorbar-title {
    font-size: 12px;
    font-weight: 600;
    line-height: 1;
    text-transform: lowercase;
    color: #595959;
}

.wrap-colorbar {
    display: flex;
    height: 24px;
    border: 1px solid #d9d9d9;
    border-radius: 3px;
    overflow: hidden;
    background-color: #f5f5f5;
}

.wrap-colorbar-half {
    height: 14px !important;
}

.wrap-colorbar-segment {
    flex: 1;
    min-width: 0;
    border-right: 1px solid rgba(0, 0, 0, 0.08);
}

.wrap-colorbar-segment:last-child {
    border-right: none;
}

.wrap-colorbar-space {
    flex: 0 0 1px;
    min-width: 1px;
    background: black !important;
    border-right: none !important;
}
</style>
