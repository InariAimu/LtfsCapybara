<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import { NAlert, NCard, NModal, NSpace, NSpin, NTabPane, NTag, NTabs, useMessage } from 'naive-ui';
import { useI18n } from 'vue-i18n';
import FormatParamDialog from '@/components/FormatParamDialog.vue';
import { tapeMachineApi, type TapeMachineSnapshot } from '@/api/modules/tapemachine';
import {
    createDefaultTapeFsFormatParam,
    taskApi,
    type TapeFsFormatParam,
    type TapeFsTaskGroup,
} from '@/api/modules/tasks';
import { useExecutionStore } from '@/stores/executionStore';
import { useFileStore } from '@/stores/fileStore';
import TapeMachineLogsTab from '@/views/tape-machine/TapeMachineLogsTab.vue';
import TapeMachineOperationsTab from '@/views/tape-machine/TapeMachineOperationsTab.vue';
import TapeMachineTasksTab from '@/views/tape-machine/TapeMachineTasksTab.vue';

interface Props {
    tapeDriveId?: string | null;
}

const props = defineProps<Props>();

const { t } = useI18n();
const message = useMessage();
const executionStore = useExecutionStore();
const fileStore = useFileStore();
type TapeMachineOperation = 'thread' | 'load' | 'unthread' | 'eject' | 'rewind' | 'read-info' | 'format';

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
const blockingOperation = computed(() => pendingOperation.value);
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
const blockingOperationLabel = computed(() => {
    if (!blockingOperation.value) {
        return '';
    }

    const translationKeyByOperation: Record<TapeMachineOperation, string> = {
        thread: 'tapeMachine.actions.threadTape',
        load: 'tapeMachine.actions.loadTape',
        unthread: 'tapeMachine.actions.unthreadTape',
        eject: 'tapeMachine.actions.ejectTape',
        rewind: 'tapeMachine.actions.rewindTape',
        'read-info': 'tapeMachine.actions.readInfo',
        format: 'tapeMachine.actions.formatTape',
    };

    return t(translationKeyByOperation[blockingOperation.value]);
});

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
                        <tape-machine-operations-tab
                            :operation-busy="operationBusy"
                            :pending-operation="pendingOperation"
                            :can-format-tape="canFormatTape"
                            :snapshot="snapshot"
                            @run-action="runAction"
                            @open-format-dialog="openFormatDialog"
                        />
                    </n-tab-pane>

                    <n-tab-pane name="tasks" :tab="t('tapeMachine.tabs.tasks')">
                        <tape-machine-tasks-tab
                            :scsi-metrics-enabled="scsiMetricsEnabled"
                            :is-metrics-toggle-loading="isMetricsToggleLoading"
                            :has-loaded-tape="hasLoadedTape"
                            :snapshot="snapshot"
                            :matching-task-groups="matchingTaskGroups"
                            :can-run-tasks="canRunTasks"
                            :active-execution="activeExecution"
                            :active-execution-performance="activeExecutionPerformance"
                            :active-execution-channel-error-rates="activeExecutionChannelErrorRates"
                            :active-execution-highest-error-rate="activeExecutionHighestErrorRate"
                            :active-execution-speed-history="activeExecutionSpeedHistory"
                            :active-execution-channel-error-rate-history="activeExecutionChannelErrorRateHistory"
                            @run-task-group="runTaskGroup"
                            @update-scsi-metrics-enabled="handleUpdateScsiMetricsEnabled"
                        />
                    </n-tab-pane>

                    <n-tab-pane name="logs" :tab="t('tapeMachine.tabs.logs')">
                        <tape-machine-logs-tab :drive-logs="driveLogs" />
                    </n-tab-pane>
                </n-tabs>
            </n-space>
        </n-card>

        <format-param-dialog
            v-model:show="showFormatDialog"
            :loading="pendingOperation === 'format'"
            :title="t('tapeMachine.formatDialogTitle')"
            :submit-text="t('tapeMachine.actions.formatTape')"
            :description="t('tapeMachine.formatWarning')"
            :initial-format-param="formatDialogDefaults"
            :barcode="snapshot?.loadedBarcode ?? ''"
            :volume-name="snapshot?.ltfsVolumeName ?? snapshot?.loadedBarcode ?? ''"
            @submit="handleFormatTape"
        />

        <n-modal
            :show="!!blockingOperation"
            :mask-closable="false"
            :close-on-esc="false"
            :closable="false"
            :trap-focus="true"
            :auto-focus="false"
            preset="card"
            class="operation-blocking-modal"
            :title="t('tapeMachine.operationInProgressTitle')"
        >
            <div class="operation-blocking-modal__body">
                <n-spin size="large" />
                <div class="operation-blocking-modal__text">
                    {{ t('tapeMachine.operationInProgress', { action: blockingOperationLabel }) }}
                </div>
            </div>
        </n-modal>
    </div>
</template>

<style scoped>
.tape-machine-page {
    padding: 10px;
}

:deep(.operation-blocking-modal.n-modal) {
    width: 100vw;
    max-width: none;
    margin: 0;
}

:deep(.operation-blocking-modal .n-card) {
    width: 100vw;
    min-height: 100vh;
    margin: 0;
    border-radius: 0;
    display: flex;
    align-items: center;
    justify-content: center;
}

:deep(.operation-blocking-modal .n-card-header) {
    justify-content: center;
    padding-bottom: 0;
}

.operation-blocking-modal__body {
    min-height: 50vh;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: 16px;
    text-align: center;
}

.operation-blocking-modal__text {
    font-size: 16px;
}
</style>
