<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import {
    NAlert,
    NButton,
    NCard,
    NEmpty,
    NSpace,
    NTabPane,
    NTag,
    NTabs,
    NThing,
    useMessage,
} from 'naive-ui';
import { useI18n } from 'vue-i18n';
import FormatParamDialog from '@/components/FormatParamDialog.vue';
import {
    tapeMachineApi,
    type TapeMachineAction,
    type TapeMachineSnapshot,
} from '@/api/modules/tapemachine';
import {
    createDefaultTapeFsFormatParam,
    taskApi,
    type TapeFsFormatParam,
    type TapeFsTaskGroup,
} from '@/api/modules/tasks';
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

const loading = ref(false);
const actionLoading = ref(false);
const formatLoading = ref(false);
const taskLoading = ref(false);
const snapshot = ref<TapeMachineSnapshot | null>(null);
const error = ref<string | null>(null);
const activeTab = ref('operations');
const showFormatDialog = ref(false);

const stateByIndex = ['Unknown', 'Empty', 'Loaded', 'Threaded', 'Faulted'] as const;
const actionByIndex = ['ThreadTape', 'LoadTape', 'UnthreadTape', 'EjectTape', 'ReadInfo'] as const;

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
const driveLogs = computed(() =>
    executionStore.logs.filter(log => log.tapeDriveId === props.tapeDriveId),
);
const hasLoadedTape = computed(() => snapshot.value?.state && snapshot.value.state !== 'Empty');
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

function normalizeAction(action: unknown): string {
    if (typeof action === 'string') {
        return action;
    }

    if (typeof action === 'number') {
        return actionByIndex[action] ?? '';
    }

    return '';
}

const stateLabel = computed(() => {
    if (!snapshot.value) {
        return t('tapeMachine.state.unknown');
    }

    const state = normalizeState(snapshot.value.state).toLowerCase();
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

function can(action: TapeMachineAction) {
    const allowedActions = snapshot.value?.allowedActions ?? [];
    return allowedActions.some(allowedAction => normalizeAction(allowedAction) === action);
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

async function runAction(action: 'thread' | 'load' | 'unthread' | 'eject' | 'read-info') {
    if (!hasSelectedDrive.value || !props.tapeDriveId) {
        message.warning(t('tapeMachine.errors.selectDriveFirst'));
        return;
    }

    actionLoading.value = true;
    error.value = null;
    try {
        const res = await tapeMachineApi.execute(props.tapeDriveId, action);
        console.log('Action result', res.data);
        snapshot.value = res.data;
    } catch (err: any) {
        console.error('runAction error', err);
        const msg = err?.response?.data?.message;
        error.value = msg || t('tapeMachine.errors.actionFailed');
    } finally {
        actionLoading.value = false;
    }
}

async function runTaskGroup(group: TapeFsTaskGroup) {
    if (!props.tapeDriveId || !canRunTasks.value) {
        return;
    }

    try {
        const res = await taskApi.executeGroup(group.tapeBarcode, props.tapeDriveId);
        executionStore.upsertExecution(res.data);
        message.success(t('task.executionStarted'));
        activeTab.value = 'tasks';
    } catch (err: any) {
        console.error('runTaskGroup error', err);
        const msg = err?.response?.data?.message;
        message.error(msg || t('task.executionStartFailed'));
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

    formatLoading.value = true;
    error.value = null;
    try {
        const res = await tapeMachineApi.format(props.tapeDriveId, formatParam);
        snapshot.value = res.data;
        showFormatDialog.value = false;
        message.success(t('tapeMachine.formatSuccess'));
    } catch (err: any) {
        console.error('handleFormatTape error', err);
        const msg = err?.response?.data?.message;
        error.value = msg || t('tapeMachine.errors.formatFailed');
        message.error(msg || t('tapeMachine.errors.formatFailed'));
    } finally {
        formatLoading.value = false;
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
                                    :loading="actionLoading"
                                    :disabled="loading || !can('ThreadTape')"
                                    @click="runAction('thread')"
                                >
                                    {{ t('tapeMachine.actions.threadTape') }}
                                </n-button>
                                <n-button
                                    :loading="actionLoading"
                                    :disabled="loading || !can('LoadTape')"
                                    @click="runAction('load')"
                                >
                                    {{ t('tapeMachine.actions.loadTape') }}
                                </n-button>
                                <n-button
                                    :loading="actionLoading"
                                    :disabled="loading || !can('UnthreadTape')"
                                    @click="runAction('unthread')"
                                >
                                    {{ t('tapeMachine.actions.unthreadTape') }}
                                </n-button>
                                <n-button
                                    :loading="actionLoading"
                                    :disabled="loading || !can('EjectTape')"
                                    @click="runAction('eject')"
                                >
                                    {{ t('tapeMachine.actions.ejectTape') }}
                                </n-button>
                                <n-button
                                    type="primary"
                                    :loading="actionLoading"
                                    :disabled="loading || !can('ReadInfo')"
                                    @click="runAction('read-info')"
                                >
                                    {{ t('tapeMachine.actions.readInfo') }}
                                </n-button>
                                <n-button
                                    type="warning"
                                    :loading="formatLoading"
                                    :disabled="loading || actionLoading || !canFormatTape"
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
                                :loading="actionLoading"
                            />
                        </n-space>
                    </n-tab-pane>

                    <n-tab-pane name="tasks" :tab="t('tapeMachine.tabs.tasks')">
                        <n-space vertical :size="12">
                            <n-alert v-if="!hasLoadedTape" type="info">
                                {{ t('tapeMachine.noTapeLoaded') }}
                            </n-alert>
                            <n-alert
                                v-else-if="snapshot?.hasLtfsFilesystem === false"
                                type="warning"
                            >
                                {{ t('tapeMachine.autoFormatHint') }}
                            </n-alert>
                            <n-alert v-if="activeExecution" type="info">
                                {{
                                    t('tapeMachine.executionStatus', {
                                        status: activeExecution.status,
                                    })
                                }}
                                <span v-if="activeExecution.progress?.statusMessage">
                                    · {{ activeExecution.progress.statusMessage }}
                                </span>
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
            :loading="formatLoading"
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
.tape-machine-page {
    padding: 10px;
}
</style>
