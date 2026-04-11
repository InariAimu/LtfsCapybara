<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import type {
    TaskExecutionChannelErrorHistorySample,
    TaskExecutionChannelErrorRate,
} from '@/api/modules/tasks';
import { executionChartLayout } from '@/components/executionChartLayout';

const props = defineProps<{
    history: TaskExecutionChannelErrorHistorySample[];
    latestRates: TaskExecutionChannelErrorRate[];
}>();

const chartWidth = executionChartLayout.chartWidth;
const chartHeight = executionChartLayout.chartHeight;
const padding = executionChartLayout.padding;
const legendWidth = executionChartLayout.legendWidth;
const innerWidth = chartWidth - padding.left - padding.right;
const innerHeight = chartHeight - padding.top - padding.bottom;
const rows = executionChartLayout.channelCount;
const rowHeight = innerHeight / rows;
const windowTicks = executionChartLayout.windowTicks;
const plotCanvas = ref<HTMLCanvasElement | null>(null);
const plotWrapper = ref<HTMLDivElement | null>(null);

let resizeObserver: ResizeObserver | null = null;

const latestTick = computed(() => props.history[props.history.length - 1]?.timestampUtcTicks ?? 0);
const windowStartTick = computed(() => Math.max(0, latestTick.value - windowTicks));
const wrapperStyle = computed(() => ({
    '--execution-chart-legend-width': `${legendWidth}px`,
}));
const rowLabelWidth = padding.left - 8;
const orderedLatestRates = computed(() => {
    const latestByChannel = new Map(props.latestRates.map(rate => [rate.channelNumber, rate]));
    return Array.from({ length: rows }, (_, index) => {
        const channelNumber = index + 1;
        return (
            latestByChannel.get(channelNumber) ?? {
                channelNumber,
                displayValue: '-',
                isNegativeInfinity: false,
                heatLevel: 0,
                errorRateLog10: null,
            }
        );
    });
});
const latestRateLabels = computed(() =>
    orderedLatestRates.value.map((rate, index) => ({
        channelNumber: rate.channelNumber,
        displayValue: rate.displayValue,
        top: `${((padding.top + index * rowHeight + rowHeight * 0.5) / chartHeight) * 100}%`,
        left: `calc(100% + 8px)`,
        color: interpolateColor(rate),
    })),
);
const channelLabels = computed(() =>
    Array.from({ length: rows }, (_, index) => ({
        channelNumber: index + 1,
        top: `${((padding.top + index * rowHeight + rowHeight * 0.5) / chartHeight) * 100}%`,
    })),
);
const timeLabels = computed(() => [
    {
        key: 'start',
        text: '-10m',
        left: `${(padding.left / chartWidth) * 100}%`,
        justify: 'flex-start',
    },
    {
        key: 'middle',
        text: '-5m',
        left: `${((padding.left + innerWidth / 2) / chartWidth) * 100}%`,
        justify: 'center',
    },
    {
        key: 'end',
        text: 'now',
        left: `${((padding.left + innerWidth) / chartWidth) * 100}%`,
        justify: 'flex-end',
    },
]);

function interpolateColor(
    rate: Pick<TaskExecutionChannelErrorRate, 'isNegativeInfinity' | 'errorRateLog10'>,
) {
    if (rate.isNegativeInfinity || (rate.errorRateLog10 ?? -99) <= -6) {
        return 'rgb(34, 197, 94)';
    }

    const value = rate.errorRateLog10 ?? -6;
    if (value <= -4) {
        const t = Math.max(0, Math.min(1, (value + 6) / 2));
        const r = Math.round(34 + (250 - 34) * t);
        const g = Math.round(197 + (204 - 197) * t);
        const b = Math.round(94 + (21 - 94) * t);
        return `rgb(${r}, ${g}, ${b})`;
    }

    const t = Math.max(0, Math.min(1, (value + 4) / 1.02));
    const r = Math.round(250 + (220 - 250) * t);
    const g = Math.round(204 + (38 - 204) * t);
    const b = Math.round(21 + (38 - 21) * t);
    return `rgb(${r}, ${g}, ${b})`;
}

function clamp(value: number, min: number, max: number) {
    return Math.min(max, Math.max(min, value));
}

function drawHeatmap() {
    const canvas = plotCanvas.value;
    const wrapper = plotWrapper.value;
    if (!canvas || !wrapper) {
        return;
    }

    const width = Math.max(1, wrapper.clientWidth);
    const height = Math.max(1, wrapper.clientHeight);
    const dpr = window.devicePixelRatio || 1;
    canvas.width = Math.round(width * dpr);
    canvas.height = Math.round(height * dpr);

    const context = canvas.getContext('2d');
    if (!context) {
        return;
    }

    context.setTransform(dpr, 0, 0, dpr, 0, 0);
    context.clearRect(0, 0, width, height);

    const scaleX = width / chartWidth;
    const scaleY = height / chartHeight;
    context.save();
    context.scale(scaleX, scaleY);

    context.fillStyle = 'rgba(248, 250, 252, 0.9)';
    context.fillRect(padding.left, padding.top, innerWidth, innerHeight);

    context.strokeStyle = 'rgba(148, 163, 184, 0.16)';
    context.lineWidth = 1;
    for (let row = 1; row < rows; row += 1) {
        const y = padding.top + row * rowHeight;
        context.beginPath();
        context.moveTo(padding.left, y);
        context.lineTo(padding.left + innerWidth, y);
        context.stroke();
    }

    const denominator = Math.max(1, latestTick.value - windowStartTick.value);
    for (let sampleIndex = 0; sampleIndex < props.history.length; sampleIndex += 1) {
        const sample = props.history[sampleIndex];
        const nextSample =
            sampleIndex < props.history.length - 1 ? props.history[sampleIndex + 1] : null;
        const startRatio =
            latestTick.value <= windowStartTick.value
                ? 1
                : clamp((sample.timestampUtcTicks - windowStartTick.value) / denominator, 0, 1);
        const endRatio = nextSample
            ? clamp((nextSample.timestampUtcTicks - windowStartTick.value) / denominator, 0, 1)
            : 1;
        const x = padding.left + startRatio * innerWidth;
        const widthUnits = Math.max(1.5, (endRatio - startRatio) * innerWidth);

        for (const rate of sample.channelErrorRates) {
            const channelIndex = rate.channelNumber - 1;
            if (channelIndex < 0 || channelIndex >= rows) {
                continue;
            }

            context.fillStyle = interpolateColor(rate);
            context.fillRect(
                x,
                padding.top + channelIndex * rowHeight,
                widthUnits,
                Math.max(4, rowHeight - 1),
            );
        }
    }

    context.strokeStyle = 'rgba(100, 116, 139, 0.45)';
    context.lineWidth = 1.2;
    context.beginPath();
    context.moveTo(padding.left, padding.top + innerHeight);
    context.lineTo(padding.left + innerWidth, padding.top + innerHeight);
    context.stroke();

    context.restore();
}

onMounted(() => {
    drawHeatmap();

    if (plotWrapper.value && typeof ResizeObserver !== 'undefined') {
        resizeObserver = new ResizeObserver(() => {
            drawHeatmap();
        });
        resizeObserver.observe(plotWrapper.value);
    }
});

onBeforeUnmount(() => {
    resizeObserver?.disconnect();
    resizeObserver = null;
});

watch(
    () => [props.history, props.latestRates],
    () => {
        drawHeatmap();
    },
    { deep: true },
);
</script>

<template>
    <div class="error-history-shell" :style="wrapperStyle">
        <div ref="plotWrapper" class="error-history-plot">
            <canvas ref="plotCanvas" class="error-history-canvas"></canvas>
            <div class="chart-overlay" aria-hidden="true">
                <div
                    v-for="label in channelLabels"
                    :key="`channel-${label.channelNumber}`"
                    class="channel-label"
                    :style="{ top: label.top, width: `${rowLabelWidth}px` }"
                >
                    CH{{ label.channelNumber }}
                </div>
                <div
                    v-for="label in timeLabels"
                    :key="label.key"
                    class="time-label"
                    :style="{ left: label.left, justifyContent: label.justify }"
                >
                    {{ label.text }}
                </div>
                <div
                    v-for="label in latestRateLabels"
                    :key="`value-${label.channelNumber}`"
                    class="value-label"
                    :style="{ top: label.top, left: label.left, color: label.color }"
                >
                    {{ label.displayValue }}
                </div>
            </div>
        </div>
        <div class="error-history-gutter" aria-hidden="true"></div>
    </div>
</template>

<style scoped>
.error-history-shell {
    width: 100%;
    display: grid;
    grid-template-columns: minmax(0, 1fr) var(--execution-chart-legend-width);
    gap: 0;
}

.error-history-plot {
    min-width: 0;
    height: 220px;
    position: relative;
}

.error-history-canvas {
    width: 100%;
    height: 220px;
    display: block;
}

.chart-overlay {
    position: absolute;
    inset: 0;
    pointer-events: none;
}

.channel-label {
    position: absolute;
    left: 0;
    transform: translateY(-50%);
    color: rgba(100, 116, 139, 0.92);
    font-size: 10px;
    line-height: 1;
    text-align: right;
}

.time-label {
    position: absolute;
    bottom: 8px;
    width: 56px;
    transform: translateX(-50%);
    display: flex;
    color: rgba(100, 116, 139, 0.92);
    font-size: 10px;
    line-height: 1;
}

.error-history-gutter {
    width: var(--execution-chart-legend-width);
    height: 220px;
}

.value-label {
    position: absolute;
    font-size: 10px;
    line-height: 1;
    transform: translateY(-50%);
    white-space: nowrap;
}
</style>
