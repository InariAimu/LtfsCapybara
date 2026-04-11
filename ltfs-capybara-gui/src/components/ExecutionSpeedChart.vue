<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import type { TaskExecutionSpeedSample } from '@/api/modules/tasks';
import { executionChartLayout } from '@/components/executionChartLayout';

const props = withDefaults(
    defineProps<{
        samples: TaskExecutionSpeedSample[];
        maxY?: number;
    }>(),
    {
        maxY: 200,
    },
);

const chartWidth = executionChartLayout.chartWidth;
const chartHeight = executionChartLayout.chartHeight;
const padding = executionChartLayout.padding;
const innerWidth = chartWidth - padding.left - padding.right;
const innerHeight = chartHeight - padding.top - padding.bottom;
const windowTicks = executionChartLayout.windowTicks;
const plotCanvas = ref<HTMLCanvasElement | null>(null);
const plotWrapper = ref<HTMLDivElement | null>(null);
const wrapperStyle = computed(() => ({
    '--execution-chart-legend-width': `${executionChartLayout.legendWidth}px`,
}));
let resizeObserver: ResizeObserver | null = null;

const latestTick = computed(() => {
    const lastSample = props.samples[props.samples.length - 1];
    return lastSample?.timestampUtcTicks ?? 0;
});
const windowStartTick = computed(() => Math.max(0, latestTick.value - windowTicks));

const yTicks = computed(() => [0, 50, 100, 150, 200].filter(value => value <= props.maxY));
const yAxisLabelWidth = padding.left - 8;
const yAxisLabels = computed(() =>
    yTicks.value.map(tick => ({
        tick,
        top: `${((padding.top + (1 - tick / props.maxY) * innerHeight) / chartHeight) * 100}%`,
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

function clamp(value: number, min: number, max: number) {
    return Math.min(max, Math.max(min, value));
}

function drawSpeedChart() {
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

    context.strokeStyle = 'rgba(148, 163, 184, 0.18)';
    context.lineWidth = 1;
    for (const tick of yTicks.value) {
        const y = padding.top + (1 - tick / props.maxY) * innerHeight;
        context.beginPath();
        context.moveTo(padding.left, y);
        context.lineTo(padding.left + innerWidth, y);
        context.stroke();
    }

    context.strokeStyle = 'rgba(100, 116, 139, 0.45)';
    context.lineWidth = 1.2;
    context.beginPath();
    context.moveTo(padding.left, padding.top);
    context.lineTo(padding.left, padding.top + innerHeight);
    context.lineTo(padding.left + innerWidth, padding.top + innerHeight);
    context.stroke();

    const denominator = Math.max(1, latestTick.value - windowStartTick.value);
    const normalizedSamples = props.samples.map(sample => {
        const xRatio =
            latestTick.value <= windowStartTick.value
                ? 1
                : clamp((sample.timestampUtcTicks - windowStartTick.value) / denominator, 0, 1);
        const yRatio = clamp(sample.speedMBPerSecond / props.maxY, 0, 1);

        return {
            x: padding.left + xRatio * innerWidth,
            y: padding.top + (1 - yRatio) * innerHeight,
        };
    });

    if (normalizedSamples.length > 0) {
        context.beginPath();
        context.moveTo(normalizedSamples[0].x, padding.top + innerHeight);
        for (const point of normalizedSamples) {
            context.lineTo(point.x, point.y);
        }
        context.lineTo(
            normalizedSamples[normalizedSamples.length - 1].x,
            padding.top + innerHeight,
        );
        context.closePath();
        context.fillStyle = 'rgba(14, 165, 233, 0.16)';
        context.fill();

        context.beginPath();
        context.moveTo(normalizedSamples[0].x, normalizedSamples[0].y);
        for (let index = 1; index < normalizedSamples.length; index += 1) {
            const point = normalizedSamples[index];
            context.lineTo(point.x, point.y);
        }
        context.strokeStyle = 'rgba(2, 132, 199, 0.96)';
        context.lineWidth = 2.5;
        context.lineCap = 'round';
        context.lineJoin = 'round';
        context.stroke();
    }

    context.restore();
}

onMounted(() => {
    drawSpeedChart();

    if (plotWrapper.value && typeof ResizeObserver !== 'undefined') {
        resizeObserver = new ResizeObserver(() => {
            drawSpeedChart();
        });
        resizeObserver.observe(plotWrapper.value);
    }
});

onBeforeUnmount(() => {
    resizeObserver?.disconnect();
    resizeObserver = null;
});

watch(
    () => [props.samples, props.maxY],
    () => {
        drawSpeedChart();
    },
    { deep: true },
);
</script>

<template>
    <div class="speed-chart-shell" :style="wrapperStyle">
        <div ref="plotWrapper" class="speed-chart-plot">
            <canvas ref="plotCanvas" class="speed-chart"></canvas>
            <div class="chart-overlay" aria-hidden="true">
                <div
                    v-for="label in yAxisLabels"
                    :key="`tick-${label.tick}`"
                    class="y-axis-label"
                    :style="{ top: label.top, width: `${yAxisLabelWidth}px` }"
                >
                    {{ label.tick }}
                </div>
                <div
                    v-for="label in timeLabels"
                    :key="label.key"
                    class="time-label"
                    :style="{ left: label.left, justifyContent: label.justify }"
                >
                    {{ label.text }}
                </div>
            </div>
        </div>
        <div class="speed-chart-gutter" aria-hidden="true"></div>
    </div>
</template>

<style scoped>
.speed-chart-shell {
    width: 100%;
    min-height: 220px;
    display: grid;
    grid-template-columns: minmax(0, 1fr) var(--execution-chart-legend-width);
    gap: 0;
}

.speed-chart-plot {
    min-width: 0;
    height: 220px;
    position: relative;
}

.speed-chart {
    width: 100%;
    height: 220px;
    display: block;
}

.speed-chart-gutter {
    width: var(--execution-chart-legend-width);
}

.chart-overlay {
    position: absolute;
    inset: 0;
    pointer-events: none;
}

.y-axis-label {
    position: absolute;
    left: 0;
    transform: translateY(-50%);
    color: rgba(100, 116, 139, 0.92);
    font-size: 11px;
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
    font-size: 11px;
    line-height: 1;
}
</style>
