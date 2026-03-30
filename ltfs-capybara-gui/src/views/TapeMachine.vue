<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { NAlert, NButton, NCard, NSpace, NTag, useMessage } from 'naive-ui';
import { useI18n } from 'vue-i18n';
import {
    tapeMachineApi,
    type TapeMachineAction,
    type TapeMachineSnapshot,
} from '@/api/modules/tapemachine';
import TapeInfo from '@/views/TapeInfo.vue';

interface Props {
    tapeDriveId?: string | null;
}

const props = defineProps<Props>();

const { t } = useI18n();
const message = useMessage();

const loading = ref(false);
const actionLoading = ref(false);
const snapshot = ref<TapeMachineSnapshot | null>(null);
const error = ref<string | null>(null);

const stateByIndex = ['Unknown', 'Empty', 'Loaded', 'Threaded', 'Faulted'] as const;
const actionByIndex = ['ThreadTape', 'LoadTape', 'UnthreadTape', 'EjectTape', 'ReadInfo'] as const;

const hasSelectedDrive = computed(() => !!props.tapeDriveId && props.tapeDriveId !== 'none');

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

watch(
    () => props.tapeDriveId,
    () => {
        void loadState();
    },
    { immediate: true },
);
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
                    <n-tag v-if="snapshot?.isFake" type="warning" size="small">
                        {{ t('tapeMachine.fakeDevice') }}
                    </n-tag>
                </n-space>

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
                </n-space>

                <tape-info
                    v-if="snapshot?.cartridgeMemory"
                    :tape-info-data="snapshot.cartridgeMemory"
                    :loading="actionLoading"
                />
            </n-space>
        </n-card>
    </div>
</template>

<style scoped>
.tape-machine-page {
    padding: 10px;
}
</style>
