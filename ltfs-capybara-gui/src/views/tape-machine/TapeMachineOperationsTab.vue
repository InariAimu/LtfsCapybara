<script setup lang="ts">
import { useI18n } from 'vue-i18n';
import { NAlert, NButton, NSpace } from 'naive-ui';
import type { TapeMachineSnapshot } from '@/api/modules/tapemachine';
import TapeInfo from '@/views/TapeInfo.vue';

type TapeMachineOperation = 'thread' | 'load' | 'unthread' | 'eject' | 'read-info' | 'format';
type TapeMachineAction = Exclude<TapeMachineOperation, 'format'>;

interface Props {
    operationBusy: boolean;
    pendingOperation: TapeMachineOperation | null;
    canFormatTape: boolean;
    snapshot: TapeMachineSnapshot | null;
}

const props = defineProps<Props>();

const emit = defineEmits<{
    runAction: [action: TapeMachineAction];
    openFormatDialog: [];
}>();

const { t } = useI18n();

function isOperationLoading(action: TapeMachineOperation) {
    return props.pendingOperation === action;
}
</script>

<template>
    <n-space vertical :size="12">
        <n-space>
            <n-button
                :loading="isOperationLoading('thread')"
                :disabled="operationBusy"
                @click="emit('runAction', 'thread')"
            >
                {{ t('tapeMachine.actions.threadTape') }}
            </n-button>
            <n-button
                :loading="isOperationLoading('load')"
                :disabled="operationBusy"
                @click="emit('runAction', 'load')"
            >
                {{ t('tapeMachine.actions.loadTape') }}
            </n-button>
            <n-button
                :loading="isOperationLoading('unthread')"
                :disabled="operationBusy"
                @click="emit('runAction', 'unthread')"
            >
                {{ t('tapeMachine.actions.unthreadTape') }}
            </n-button>
            <n-button
                :loading="isOperationLoading('eject')"
                :disabled="operationBusy"
                @click="emit('runAction', 'eject')"
            >
                {{ t('tapeMachine.actions.ejectTape') }}
            </n-button>
            <n-button
                type="primary"
                :loading="isOperationLoading('read-info')"
                :disabled="operationBusy"
                @click="emit('runAction', 'read-info')"
            >
                {{ t('tapeMachine.actions.readInfo') }}
            </n-button>
            <n-button
                type="warning"
                :loading="isOperationLoading('format')"
                :disabled="operationBusy || !canFormatTape"
                @click="emit('openFormatDialog')"
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
</template>