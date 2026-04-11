<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import {
    NAlert,
    NButton,
    NCard,
    NEmpty,
    NSwitch,
    NSpace,
    NTabPane,
    NTag,
    NTabs,
    NThing,
    useMessage,
} from 'naive-ui';
import { useI18n } from 'vue-i18n';
import FormatParamDialog from '@/components/FormatParamDialog.vue';
import { tapeMachineApi, type TapeMachineSnapshot } from '@/api/modules/tapemachine';
import {
    createDefaultTapeFsFormatParam,
    taskApi,
    type TapeFsFormatParam,
    type TapeFsTaskGroup,
} from '@/api/modules/tasks';
import ExecutionChannelHeatBar from '@/components/ExecutionChannelHeatBar.vue';
import ExecutionSpeedChart from '@/components/ExecutionSpeedChart.vue';
import { useExecutionStore } from '@/stores/executionStore';
import { useFileStore } from '@/stores/fileStore';
import TapeInfo from '@/views/TapeInfo.vue';

interface Props {
    tapeDriveId?: string | null;
}

const props = defineProps<Props>();

const { t } = useI18n();
const message = useMessage();
const executionStore = useExecutionStore();
const fileStore = useFileStore();
type TapeMachineOperation = 'thread' | 'load' | 'unthread' | 'eject' | 'read-info' | 'format';

const loading = ref(false);
const taskLoading = ref(false);
const isMetricsToggleLoading = ref(false);
const pendingOperation = ref<TapeMachineOperation | null>(null);
const snapshot = ref<TapeMachineSnapshot | null>(null);
const error = ref<string | null>(null);
const activeTab = ref('operations');
const showFormatDialog = ref(false);

const stateByIndex = ['Unknown', 'Empty', 'Loaded'] as const;

const hasSelectedDrive = computed(() => !!props.tapeDriveId && props.tapeDriveId !== 'none');
const taskGroups = computed(() => fileStore.taskGroups);
const driveExecutions = computed(() =>
    executionStore.executions.filter(execution => execution.tapeDriveId === props.tapeDriveId),
);
const activeExecution = computed(
    () =>
        driveExecutions.value.find(execution =>
            ['pending', 'running', 'waiting-for-confirmation'].includes(execution.status),
        ) ?? null,
);
const activeExecutionPerformance = computed(
    () => activeExecution.value?.progress?.tapePerformance ?? null,
);
const activeExecutionChannelErrorRates = computed(
    () => activeExecution.value?.progress?.channelErrorRates ?? null,
);
const activeExecutionHighestErrorRate = computed(
    () => activeExecution.value?.progress?.highestChannelErrorRate ?? null,
);
const activeExecutionSpeedHistory = computed(
    () => activeExecution.value?.progress?.speedHistory ?? [],
);
const activeExecutionChannelErrorRateHistory = computed(
    () => activeExecution.value?.progress?.channelErrorRateHistory ?? [],
);
const operationBusy = computed(() => loading.value || pendingOperation.value !== null);
const activeExecutionElapsed = computed(() =>
    formatDurationFromTicks(activeExecution.value?.startedAtTicks),
);
const activeExecutionEta = computed(() =>
    formatRemainingSeconds(activeExecution.value?.progress?.estimatedRemainingSeconds),
);
const scsiMetricsEnabled = computed(
    () => activeExecution.value?.scsiMetricsEnabled ?? executionStore.scsiMetricsEnabledPreference,
);
const driveLogs = computed(() =>
    executionStore.logs.filter(log => log.tapeDriveId === props.tapeDriveId),
);
const normalizedSnapshotState = computed(() => normalizeState(snapshot.value?.state));
const hasLoadedTape = computed(() => normalizedSnapshotState.value === 'Loaded');
const matchingTaskGroups = computed(() => {
    const barcode = snapshot.value?.loadedBarcode?.toLowerCase();
    if (!barcode) {
        return [];
    }

    return taskGroups.value.filter(group => group.tapeBarcode.toLowerCase() === barcode);
});
const canRunTasks = computed(
    () => hasSelectedDrive.value && hasLoadedTape.value && !activeExecution.value,
);
const canFormatTape = computed(
    () => hasSelectedDrive.value && hasLoadedTape.value && !activeExecution.value,
);
const formatDialogDefaults = computed(() =>
    createDefaultTapeFsFormatParam(
        snapshot.value?.loadedBarcode ?? '',
        snapshot.value?.ltfsVolumeName ?? snapshot.value?.loadedBarcode ?? '',
    ),
);

function normalizeState(state: unknown): string {
    if (typeof state === 'string') {
        return state;
    }

    if (typeof state === 'number') {
        return stateByIndex[state] ?? 'Unknown';
    }

    return 'Unknown';
}

const stateLabel = computed(() => {
    if (!snapshot.value) {
        return t('tapeMachine.state.unknown');
    }

    const state = normalizedSnapshotState.value.toLowerCase();
    return t(`tapeMachine.state.${state}`);
});

const filesystemLabel = computed(() => {
    if (!snapshot.value || snapshot.value.hasLtfsFilesystem === null) {
        return t('tapeMachine.filesystem.unknown');
    }

    return snapshot.value.hasLtfsFilesystem
        ? t('tapeMachine.filesystem.ready')
        : t('tapeMachine.filesystem.missing');
});

function formatTicks(ticks: number | null | undefined): string {
    if (!ticks) {
        return '-';
    }

    const date = new Date(ticks / 10000 - 62135596800000);
    return Number.isNaN(date.getTime()) ? '-' : date.toLocaleString();
}

function formatLogLevel(level: string): 'info' | 'warning' | 'error' | 'default' {
    switch ((level || '').toLowerCase()) {
        case 'error':
            return 'error';
        case 'warning':
            return 'warning';
        case 'info':
            return 'info';
        default:
            return 'default';
    }
}

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
    if (!Number.isFinite(value ?? NaN)) {
        return '-';
    }

    return `${(value ?? 0).toFixed(1)}%`;
}

function formatHighestErrorRate(): string {
    return activeExecutionHighestErrorRate.value?.displayValue ?? '-';
}

function formatCurrentItemStatus(): string {
    const progress = activeExecution.value?.progress;
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

function isOperationLoading(action: TapeMachineOperation) {
    return pendingOperation.value === action;
}

async function loadState() {
    if (!hasSelectedDrive.value || !props.tapeDriveId) {
        snapshot.value = null;
        error.value = null;
        return;
    }

    loading.value = true;
    error.value = null;
    try {
        const res = await tapeMachineApi.getState(props.tapeDriveId);
        snapshot.value = res.data;
    } catch (err) {
        console.error('loadState error', err);
        error.value = t('tapeMachine.errors.loadStateFailed');
    } finally {
        loading.value = false;
    }
}

async function loadTaskGroups() {
    taskLoading.value = true;
    try {
        const res = await taskApi.listGroups();
        fileStore.setTaskGroups(res.data ?? []);
    } catch (err) {
        console.error('loadTaskGroups error', err);
        message.error(t('task.loadTaskGroupsFailed'));
    } finally {
        taskLoading.value = false;
    }
}

async function runAction(action: Exclude<TapeMachineOperation, 'format'>) {
    if (!hasSelectedDrive.value || !props.tapeDriveId) {
        message.warning(t('tapeMachine.errors.selectDriveFirst'));
        return;
    }

    pendingOperation.value = action;
    error.value = null;
    try {
        const res = await tapeMachineApi.execute(props.tapeDriveId, action);
        console.log('Action result', res.data);
        snapshot.value = res.data;
        fileStore.bumpTapeDriveStateRevision();
        if (action === 'read-info') {
            fileStore.bumpLocalTapeListRevision();
        }
    } catch (err: any) {
        console.error('runAction error', err);
        const msg = err?.response?.data?.message;
        error.value = msg || t('tapeMachine.errors.actionFailed');
    } finally {
        if (pendingOperation.value === action) {
            pendingOperation.value = null;
        }
    }
}

async function runTaskGroup(group: TapeFsTaskGroup) {
    if (!props.tapeDriveId || !canRunTasks.value) {
        return;
    }

    try {
        const res = await taskApi.executeGroup(
            group.tapeBarcode,
            props.tapeDriveId,
            executionStore.scsiMetricsEnabledPreference,
        );
        executionStore.upsertExecution(res.data);
        message.success(t('task.executionStarted'));
        activeTab.value = 'tasks';
    } catch (err: any) {
        console.error('runTaskGroup error', err);
        const msg = err?.response?.data?.message;
        message.error(msg || t('task.executionStartFailed'));
    }
}

async function handleUpdateScsiMetricsEnabled(value: boolean) {
    executionStore.setScsiMetricsEnabledPreference(value);

    if (!activeExecution.value) {
        return;
    }

    isMetricsToggleLoading.value = true;
    try {
        const res = await taskApi.updateScsiMetrics(activeExecution.value.executionId, value);
        executionStore.upsertExecution(res.data);
    } catch (err) {
        console.error('handleUpdateScsiMetricsEnabled error', err);
        message.error(t('task.scsiMetricsUpdateFailed'));
    } finally {
        isMetricsToggleLoading.value = false;
    }
}

function openFormatDialog() {
    if (!canFormatTape.value) {
        return;
    }

    showFormatDialog.value = true;
}

async function handleFormatTape(formatParam: TapeFsFormatParam) {
    if (!props.tapeDriveId || !canFormatTape.value) {
        return;
    }

    pendingOperation.value = 'format';
    error.value = null;
    try {
        const res = await tapeMachineApi.format(props.tapeDriveId, formatParam);
        snapshot.value = res.data;
        fileStore.bumpTapeDriveStateRevision();
        fileStore.bumpLocalTapeListRevision();
        showFormatDialog.value = false;
        message.success(t('tapeMachine.formatSuccess'));
    } catch (err: any) {
        console.error('handleFormatTape error', err);
        const msg = err?.response?.data?.message;
        error.value = msg || t('tapeMachine.errors.formatFailed');
        message.error(msg || t('tapeMachine.errors.formatFailed'));
    } finally {
        if (pendingOperation.value === 'format') {
            pendingOperation.value = null;
        }
    }
}

watch(
    () => props.tapeDriveId,
    () => {
        void loadState();
        void loadTaskGroups();
    },
    { immediate: true },
);

onMounted(() => {
    void loadTaskGroups();
});
</script>

<template>
    <div class="tape-machine-page">
        <n-card :title="t('menu.tapeMachine')" size="small">
            <n-space vertical :size="12">
                <n-alert v-if="!hasSelectedDrive" type="info">
                    {{ t('tapeMachine.selectDriveHint') }}
                </n-alert>

                <n-alert v-else-if="error" type="error">
                    {{ error }}
                </n-alert>

                <n-space v-if="hasSelectedDrive" align="center" :size="12">
                    <n-tag type="info" size="small">{{ props.tapeDriveId }}</n-tag>
                    <n-tag type="default" size="small">{{ stateLabel }}</n-tag>
                    <n-tag type="warning" size="small">{{ filesystemLabel }}</n-tag>
                    <n-tag v-if="snapshot?.loadedBarcode" type="success" size="small">
                        {{ snapshot.loadedBarcode }}
                    </n-tag>
                    <n-tag v-if="snapshot?.isFake" type="warning" size="small">
                        {{ t('tapeMachine.fakeDevice') }}
                    </n-tag>
                </n-space>

                <n-tabs v-if="hasSelectedDrive" v-model:value="activeTab" type="line" animated>
                    <n-tab-pane name="operations" :tab="t('tapeMachine.tabs.operations')">
                        <n-space vertical :size="12">
                            <n-space>
                                <n-button
                                    :loading="isOperationLoading('thread')"
                                    :disabled="operationBusy"
                                    @click="runAction('thread')"
                                >
                                    {{ t('tapeMachine.actions.threadTape') }}
                                </n-button>
                                <n-button
                                    :loading="isOperationLoading('load')"
                                    :disabled="operationBusy"
                                    @click="runAction('load')"
                                >
                                    {{ t('tapeMachine.actions.loadTape') }}
                                </n-button>
                                <n-button
                                    :loading="isOperationLoading('unthread')"
                                    :disabled="operationBusy"
                                    @click="runAction('unthread')"
                                >
                                    {{ t('tapeMachine.actions.unthreadTape') }}
                                </n-button>
                                <n-button
                                    :loading="isOperationLoading('eject')"
                                    :disabled="operationBusy"
                                    @click="runAction('eject')"
                                >
                                    {{ t('tapeMachine.actions.ejectTape') }}
                                </n-button>
                                <n-button
                                    type="primary"
                                    :loading="isOperationLoading('read-info')"
                                    :disabled="operationBusy"
                                    @click="runAction('read-info')"
                                >
                                    {{ t('tapeMachine.actions.readInfo') }}
                                </n-button>
                                <n-button
                                    type="warning"
                                    :loading="isOperationLoading('format')"
                                    :disabled="operationBusy || !canFormatTape"
                                    @click="openFormatDialog"
                                >
                                    {{ t('tapeMachine.actions.formatTape') }}
                                </n-button>
                            </n-space>

                            <n-alert v-if="snapshot?.loadedBarcode" type="info">
                                {{
                                    t('tapeMachine.currentTape', {
                                        barcode: snapshot.loadedBarcode,
                                    })
                                }}
                                <span v-if="snapshot.ltfsVolumeName">
                                    ·
                                    {{
                                        t('tapeMachine.currentVolume', {
                                            name: snapshot.ltfsVolumeName,
                                        })
                                    }}
                                </span>
                            </n-alert>

                            <tape-info
                                v-if="snapshot?.cartridgeMemory"
                                :tape-info-data="snapshot.cartridgeMemory"
                                :loading="operationBusy"
                            />
                        </n-space>
                    </n-tab-pane>

                    <n-tab-pane name="tasks" :tab="t('tapeMachine.tabs.tasks')">
                        <n-space vertical :size="12">
                            <n-space justify="space-between" align="center">
                                <span>{{ t('task.scsiMetrics') }}</span>
                                <n-switch
                                    :value="scsiMetricsEnabled"
                                    :loading="isMetricsToggleLoading"
                                    @update:value="handleUpdateScsiMetricsEnabled"
                                />
                            </n-space>
                            <n-alert v-if="!hasLoadedTape" type="info">
                                {{ t('tapeMachine.noTapeLoaded') }}
                            </n-alert>
                            <n-alert
                                v-else-if="snapshot?.hasLtfsFilesystem === false"
                                type="warning"
                            >
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
                                            @click="runTaskGroup(group)"
                                        >
                                            {{ t('tapeMachine.runTaskGroup') }}
                                        </n-button>
                                    </n-space>
                                </n-card>
                            </n-space>

                            <n-card
                                v-if="activeExecution"
                                size="small"
                                :title="t('tapeMachine.executionMetricsTitle')"
                            >
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
                                        <span class="summary-label">{{
                                            t('task.executionProgress')
                                        }}</span>
                                        <strong>{{
                                            formatPercent(activeExecution.progress?.percentComplete)
                                        }}</strong>
                                    </div>
                                    <div class="summary-cell">
                                        <span class="summary-label">{{
                                            t('task.executionElapsed')
                                        }}</span>
                                        <strong>{{ activeExecutionElapsed }}</strong>
                                    </div>
                                    <div class="summary-cell">
                                        <span class="summary-label">{{
                                            t('task.executionEta')
                                        }}</span>
                                        <strong>{{ activeExecutionEta }}</strong>
                                    </div>
                                    <div class="summary-cell">
                                        <span class="summary-label">{{
                                            t('task.performanceCurrentRate')
                                        }}</span>
                                        <strong>{{
                                            formatPerformanceRate(
                                                activeExecution.progress?.instantSpeedMBPerSecond ??
                                                    -1,
                                            )
                                        }}</strong>
                                    </div>
                                    <div class="summary-cell">
                                        <span class="summary-label">{{
                                            t('task.performanceAverageRate')
                                        }}</span>
                                        <strong>{{
                                            formatPerformanceRate(
                                                activeExecution.progress?.averageSpeedMBPerSecond ??
                                                    -1,
                                            )
                                        }}</strong>
                                    </div>
                                    <div class="summary-cell">
                                        <span class="summary-label">{{
                                            t('task.highestErrorRate')
                                        }}</span>
                                        <strong>{{ formatHighestErrorRate() }}</strong>
                                    </div>
                                    <div class="summary-cell">
                                        <span class="summary-label">{{
                                            t('task.performanceCompressionRatio')
                                        }}</span>
                                        <strong>{{
                                            formatCompressionRatio(
                                                activeExecutionPerformance?.compressionRatio ?? -1,
                                            )
                                        }}</strong>
                                    </div>
                                </div>
                                <div v-if="!scsiMetricsEnabled" class="execution-metrics-disabled">
                                    {{ t('task.scsiMetricsDisabledHint') }}
                                </div>
                            </n-card>

                            <div v-if="activeExecution" class="execution-visual-stack">
                                <n-card size="small" :title="t('tapeMachine.errorHeatmapTitle')">
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
                                </n-card>
                                <n-card size="small" :title="t('tapeMachine.speedChartTitle')">
                                    <ExecutionSpeedChart
                                        v-if="activeExecutionSpeedHistory.length"
                                        :samples="activeExecutionSpeedHistory"
                                    />
                                    <n-empty
                                        v-else
                                        :description="t('tapeMachine.noSpeedHistory')"
                                    />
                                </n-card>
                            </div>
                        </n-space>
                    </n-tab-pane>

                    <n-tab-pane name="logs" :tab="t('tapeMachine.tabs.logs')">
                        <n-space vertical :size="8">
                            <n-empty
                                v-if="driveLogs.length === 0"
                                :description="t('tapeMachine.logEmpty')"
                            />
                            <n-card v-for="log in driveLogs" :key="log.logId" size="small" embedded>
                                <n-thing>
                                    <template #header>
                                        <n-space align="center" :size="8">
                                            <n-tag size="small" :type="formatLogLevel(log.level)">
                                                {{ log.level }}
                                            </n-tag>
                                            <span>{{ formatTicks(log.createdAtTicks) }}</span>
                                        </n-space>
                                    </template>
                                    {{ log.message }}
                                </n-thing>
                            </n-card>
                        </n-space>
                    </n-tab-pane>
                </n-tabs>
            </n-space>
        </n-card>

        <format-param-dialog
            v-model:show="showFormatDialog"
            :loading="isOperationLoading('format')"
            :title="t('tapeMachine.formatDialogTitle')"
            :submit-text="t('tapeMachine.actions.formatTape')"
            :description="t('tapeMachine.formatWarning')"
            :initial-format-param="formatDialogDefaults"
            :barcode="snapshot?.loadedBarcode ?? ''"
            :volume-name="snapshot?.ltfsVolumeName ?? snapshot?.loadedBarcode ?? ''"
            @submit="handleFormatTape"
        />
    </div>
</template>

<style scoped>
.execution-performance-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
    gap: 4px 12px;
    margin-top: 8px;
    font-size: 12px;
}

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
</style>

<style scoped>
.tape-machine-page {
    padding: 10px;
}
</style>
