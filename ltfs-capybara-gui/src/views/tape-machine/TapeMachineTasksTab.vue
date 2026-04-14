<script setup lang="ts">
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import { NAlert, NButton, NCard, NEmpty, NSpace, NSwitch, NTag } from 'naive-ui';
import type {
    TapeFsTaskGroup,
    TaskExecutionChannelErrorHistorySample,
    TaskExecutionChannelErrorRate,
    TaskExecutionSnapshot,
    TaskExecutionSpeedSample,
    TaskExecutionTapePerformance,
} from '@/api/modules/tasks';
import type { TapeMachineSnapshot } from '@/api/modules/tapemachine';
import ExecutionChannelHeatBar from '@/components/ExecutionChannelHeatBar.vue';
import ExecutionSpeedChart from '@/components/ExecutionSpeedChart.vue';

interface Props {
    scsiMetricsEnabled: boolean;
    isMetricsToggleLoading: boolean;
    hasLoadedTape: boolean;
    snapshot: TapeMachineSnapshot | null;
    matchingTaskGroups: TapeFsTaskGroup[];
    canRunTasks: boolean;
    activeExecution: TaskExecutionSnapshot | null;
    activeExecutionPerformance: TaskExecutionTapePerformance | null;
    activeExecutionChannelErrorRates: TaskExecutionChannelErrorRate[] | null;
    activeExecutionHighestErrorRate: TaskExecutionChannelErrorRate | null;
    activeExecutionSpeedHistory: TaskExecutionSpeedSample[];
    activeExecutionChannelErrorRateHistory: TaskExecutionChannelErrorHistorySample[];
}

const props = defineProps<Props>();

const emit = defineEmits<{
    runTaskGroup: [group: TapeFsTaskGroup];
    updateScsiMetricsEnabled: [value: boolean];
}>();

const { t } = useI18n();

const activeExecutionElapsed = computed(() =>
    formatDurationFromTicks(props.activeExecution?.startedAtTicks),
);
const activeExecutionEta = computed(() =>
    formatRemainingSeconds(props.activeExecution?.progress?.estimatedRemainingSeconds),
);

function formatPerformanceRate(rate: number): string {
    if (!Number.isFinite(rate) || rate < 0) {
        return '-';
    }

    return `${rate.toFixed(rate >= 100 ? 0 : 1)} MB/s`;
}

function formatCompressionRatio(ratio: number): string {
    if (!Number.isFinite(ratio) || ratio <= 0) {
        return '-';
    }

    return `${ratio.toFixed(2)}x`;
}

function formatPercent(value: number | null | undefined): string {
    if (!Number.isFinite(value ?? Number.NaN)) {
        return '-';
    }

    return `${(value ?? 0).toFixed(1)}%`;
}

function formatHighestErrorRate(): string {
    return props.activeExecutionHighestErrorRate?.displayValue ?? '-';
}

function formatCurrentItemStatus(): string {
    const progress = props.activeExecution?.progress;
    if (!progress?.currentItemName) {
        return '-';
    }

    return `${progress.currentItemName} (${formatPercent(progress.currentItemPercentComplete)})`;
}

function formatDurationFromTicks(startedAtTicks: number | null | undefined): string {
    if (!startedAtTicks) {
        return '-';
    }

    const startedAtMs = startedAtTicks / 10000 - 62135596800000;
    const elapsedMs = Date.now() - startedAtMs;
    if (!Number.isFinite(elapsedMs) || elapsedMs < 0) {
        return '-';
    }

    const totalSeconds = Math.floor(elapsedMs / 1000);
    const hours = Math.floor(totalSeconds / 3600);
    const minutes = Math.floor((totalSeconds % 3600) / 60);
    const seconds = totalSeconds % 60;

    if (hours > 0) {
        return `${hours}:${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`;
    }

    return `${minutes}:${String(seconds).padStart(2, '0')}`;
}

function formatRemainingSeconds(seconds: number | null | undefined): string {
    if (!Number.isFinite(seconds) || (seconds ?? 0) < 0) {
        return '-';
    }

    const totalSeconds = Math.round(seconds ?? 0);
    const hours = Math.floor(totalSeconds / 3600);
    const minutes = Math.floor((totalSeconds % 3600) / 60);
    const remainingSeconds = totalSeconds % 60;

    if (hours > 0) {
        return `${hours}:${String(minutes).padStart(2, '0')}:${String(remainingSeconds).padStart(2, '0')}`;
    }

    return `${minutes}:${String(remainingSeconds).padStart(2, '0')}`;
}

function getTaskTypeLabel(taskType: string): string {
    switch ((taskType || '').toLowerCase()) {
        case 'add':
            return t('task.actionAdd');
        case 'rename':
            return t('task.typeRename');
        case 'update':
            return t('task.typeReplace');
        case 'delete':
            return t('task.typeDelete');
        case 'read':
            return t('task.typeRead');
        case 'format':
            return t('task.typeFormat');
        default:
            return taskType;
    }
}

function getTaskTypeTagType(
    taskType: string,
): 'success' | 'warning' | 'error' | 'info' | 'default' {
    switch ((taskType || '').toLowerCase()) {
        case 'add':
            return 'success';
        case 'rename':
            return 'info';
        case 'update':
        case 'format':
            return 'warning';
        case 'delete':
            return 'error';
        default:
            return 'default';
    }
}

function summarizeTaskTypes(group: TapeFsTaskGroup) {
    const counts = new Map<string, number>();
    const order = ['format', 'add', 'update', 'rename', 'delete', 'read'];

    for (const task of group.tasks) {
        const key = String(task.type || '').toLowerCase();
        counts.set(key, (counts.get(key) ?? 0) + 1);
    }

    return order
        .filter(key => counts.has(key))
        .map(key => ({
            key,
            count: counts.get(key) ?? 0,
            label: getTaskTypeLabel(key),
            type: getTaskTypeTagType(key),
        }));
}
</script>

<template>
    <n-space vertical :size="12">
        <n-space justify="space-between" align="center">
            <span>{{ t('task.scsiMetrics') }}</span>
            <n-switch
                :value="scsiMetricsEnabled"
                :loading="isMetricsToggleLoading"
                @update:value="emit('updateScsiMetricsEnabled', $event)"
            />
        </n-space>
        <n-alert v-if="!hasLoadedTape" type="info">
            {{ t('tapeMachine.noTapeLoaded') }}
        </n-alert>
        <n-alert v-else-if="snapshot?.hasLtfsFilesystem === false" type="warning">
            {{ t('tapeMachine.autoFormatHint') }}
        </n-alert>

        <n-empty
            v-if="hasLoadedTape && matchingTaskGroups.length === 0"
            :description="t('tapeMachine.noMatchingTaskGroups')"
        />

        <n-space v-else vertical :size="12">
            <n-card
                v-for="group in matchingTaskGroups"
                :key="group.tapeBarcode"
                size="small"
                embedded
            >
                <n-space justify="space-between" align="center">
                    <n-space vertical :size="4">
                        <strong>{{ group.name || group.tapeBarcode }}</strong>
                        <span>
                            {{
                                t('tapeMachine.taskCount', {
                                    count: group.tasks.length,
                                })
                            }}
                        </span>
                        <n-space :size="6">
                            <n-tag
                                v-for="summary in summarizeTaskTypes(group)"
                                :key="`${group.tapeBarcode}:${summary.key}`"
                                size="small"
                                :type="summary.type"
                            >
                                {{ summary.label }} × {{ summary.count }}
                            </n-tag>
                        </n-space>
                    </n-space>
                    <n-button
                        type="primary"
                        size="small"
                        :disabled="!canRunTasks"
                        @click="emit('runTaskGroup', group)"
                    >
                        {{ t('tapeMachine.runTaskGroup') }}
                    </n-button>
                </n-space>
            </n-card>
        </n-space>

        <n-card v-if="activeExecution" size="small" :title="t('tapeMachine.executionMetricsTitle')">
            <div class="execution-status-line">
                <strong>
                    {{
                        t('tapeMachine.executionStatus', {
                            status: activeExecution.status,
                        })
                    }}
                </strong>
                <span v-if="activeExecution.progress?.statusMessage">
                    {{ activeExecution.progress.statusMessage }}
                </span>
                <span>
                    {{ formatCurrentItemStatus() }}
                </span>
            </div>
            <div class="execution-summary-grid">
                <div class="summary-cell">
                    <span class="summary-label">{{ t('task.executionProgress') }}</span>
                    <strong>{{ formatPercent(activeExecution.progress?.percentComplete) }}</strong>
                </div>
                <div class="summary-cell">
                    <span class="summary-label">{{ t('task.executionElapsed') }}</span>
                    <strong>{{ activeExecutionElapsed }}</strong>
                </div>
                <div class="summary-cell">
                    <span class="summary-label">{{ t('task.executionEta') }}</span>
                    <strong>{{ activeExecutionEta }}</strong>
                </div>
                <div class="summary-cell">
                    <span class="summary-label">{{ t('task.performanceCurrentRate') }}</span>
                    <strong>
                        {{
                            formatPerformanceRate(
                                activeExecution.progress?.instantSpeedMBPerSecond ?? -1,
                            )
                        }}
                    </strong>
                </div>
                <div class="summary-cell">
                    <span class="summary-label">{{ t('task.performanceAverageRate') }}</span>
                    <strong>
                        {{
                            formatPerformanceRate(
                                activeExecution.progress?.averageSpeedMBPerSecond ?? -1,
                            )
                        }}
                    </strong>
                </div>
                <div class="summary-cell">
                    <span class="summary-label">{{ t('task.highestErrorRate') }}</span>
                    <strong>{{ formatHighestErrorRate() }}</strong>
                </div>
                <div class="summary-cell">
                    <span class="summary-label">{{ t('task.performanceCompressionRatio') }}</span>
                    <strong>
                        {{
                            formatCompressionRatio(
                                activeExecutionPerformance?.compressionRatio ?? -1,
                            )
                        }}
                    </strong>
                </div>
            </div>
            <div v-if="!scsiMetricsEnabled" class="execution-metrics-disabled">
                {{ t('task.scsiMetricsDisabledHint') }}
            </div>
        </n-card>

        <div v-if="activeExecution" class="execution-visual-stack">
            <n-card size="small" title="Statistic">
                <div class="statistics-section">
                    <div class="statistics-section-title">
                        {{ t('tapeMachine.errorHeatmapTitle') }}
                    </div>
                    <ExecutionChannelHeatBar
                        v-if="
                            activeExecutionChannelErrorRateHistory.length &&
                            activeExecutionChannelErrorRates?.length &&
                            scsiMetricsEnabled
                        "
                        :history="activeExecutionChannelErrorRateHistory"
                        :latest-rates="activeExecutionChannelErrorRates"
                    />
                    <n-empty
                        v-else
                        :description="
                            scsiMetricsEnabled
                                ? t('tapeMachine.noErrorRateData')
                                : t('task.scsiMetricsDisabledHint')
                        "
                    />
                </div>
                <div class="statistics-section">
                    <div class="statistics-section-title">
                        {{ t('tapeMachine.speedChartTitle') }}
                    </div>
                    <ExecutionSpeedChart
                        v-if="activeExecutionSpeedHistory.length"
                        :samples="activeExecutionSpeedHistory"
                    />
                    <n-empty v-else :description="t('tapeMachine.noSpeedHistory')" />
                </div>
            </n-card>
        </div>
    </n-space>
</template>

<style scoped>
.execution-summary-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
    gap: 10px;
    margin-top: 8px;
}

.execution-metrics-disabled {
    margin-top: 8px;
    font-size: 12px;
}

.execution-status-line {
    display: flex;
    gap: 10px;
    align-items: center;
    flex-wrap: wrap;
}

.summary-cell {
    border: 1px solid rgba(148, 163, 184, 0.18);
    border-radius: 8px;
    padding: 10px;
    display: flex;
    flex-direction: column;
    gap: 4px;
}

.summary-label {
    font-size: 11px;
    opacity: 0.7;
}

.execution-visual-stack {
    display: grid;
    grid-template-columns: 1fr;
    gap: 12px;
}

.statistics-section {
    display: flex;
    flex-direction: column;
    gap: 8px;
}

.statistics-section + .statistics-section {
    margin-top: 16px;
}

.statistics-section-title {
    font-size: 12px;
    font-weight: 600;
}
</style>