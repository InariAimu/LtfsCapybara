<script setup lang="ts">
import { computed, h } from 'vue';
import type { DataTableColumns } from 'naive-ui';
import { NCard, NDataTable, NFlex, NIcon, NPopover } from 'naive-ui';
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

interface Props {
    loading: boolean;
    wrap: WrapMetrics;
}

type DisplayWrapTableRow = WrapTableRow & {
    wrapDisplay?: string;
};

const props = defineProps<Props>();
const { t } = useI18n();

const forwardSegments = computed(() => props.wrap.segments.filter((_, index) => index % 2 === 0));

const reverseSegments = computed(() => props.wrap.segments.filter((_, index) => index % 2 === 1));

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
                <span class="colorbar-title"
                    >{{ t('tapeInfo.wrap.overall') }} - {{ props.wrap.segments.length }}
                    {{ t('tapeInfo.wrap.wraps') }}</span
                >
                <div class="wrap-colorbar">
                    <div
                        v-for="segment in props.wrap.segments"
                        :key="segment.key"
                        class="wrap-colorbar-segment"
                        :style="{ backgroundColor: segment.backgroundColor }"
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
</style>
