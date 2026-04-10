<script setup lang="ts">
import { computed, reactive, watch } from 'vue';
import {
    NAlert,
    NButton,
    NForm,
    NFormItem,
    NInput,
    NInputNumber,
    NModal,
    NSpace,
    NSwitch,
} from 'naive-ui';
import { useI18n } from 'vue-i18n';
import { createDefaultTapeFsFormatParam, type TapeFsFormatParam } from '@/api/modules/tasks';

const props = withDefaults(
    defineProps<{
        show: boolean;
        loading?: boolean;
        title?: string;
        submitText?: string;
        description?: string;
        initialFormatParam?: Partial<TapeFsFormatParam> | null;
        barcode?: string;
        volumeName?: string;
        allowBarcodeEdit?: boolean;
    }>(),
    {
        loading: false,
        title: '',
        submitText: '',
        description: '',
        initialFormatParam: null,
        barcode: '',
        volumeName: '',
        allowBarcodeEdit: true,
    },
);

const emit = defineEmits<{
    (e: 'update:show', value: boolean): void;
    (e: 'submit', value: TapeFsFormatParam): void;
    (e: 'cancel'): void;
}>();

const { t } = useI18n();

const form = reactive<TapeFsFormatParam>(createDefaultTapeFsFormatParam());

const modalTitle = computed(() => props.title || t('formatForm.title'));
const submitLabel = computed(() => props.submitText || t('formatForm.submit'));
const canSubmit = computed(
    () => form.barcode.trim().length > 0 && form.volumeName.trim().length > 0,
);

function resetForm() {
    Object.assign(
        form,
        createDefaultTapeFsFormatParam(
            props.barcode,
            props.volumeName || props.barcode,
            props.initialFormatParam ?? undefined,
        ),
    );
}

function normalizeInteger(value: number | null | undefined, fallback: number): number {
    return typeof value === 'number' && Number.isFinite(value)
        ? Math.max(0, Math.trunc(value))
        : fallback;
}

function closeDialog() {
    emit('update:show', false);
}

function handleCancel() {
    emit('cancel');
    closeDialog();
}

function handleSubmit() {
    if (!canSubmit.value) {
        return;
    }

    emit('submit', {
        ...createDefaultTapeFsFormatParam(props.barcode, props.volumeName || props.barcode),
        ...form,
        barcode: form.barcode.trim().toUpperCase(),
        volumeName: form.volumeName.trim(),
        mediaPool: form.mediaPool.trim(),
        extraPartitionCount: normalizeInteger(form.extraPartitionCount, 1),
        blockSize: normalizeInteger(form.blockSize, 524288),
        capacity: normalizeInteger(form.capacity, 65535),
        p0Size: normalizeInteger(form.p0Size, 1),
        p1Size: normalizeInteger(form.p1Size, 65535),
        encryptionKey: form.encryptionKey?.trim() ? form.encryptionKey.trim() : null,
    });
}

watch(
    () => [props.show, props.initialFormatParam, props.barcode, props.volumeName],
    ([show]) => {
        if (show) {
            resetForm();
        }
    },
    { immediate: true, deep: true },
);
</script>

<template>
    <n-modal
        :show="show"
        preset="card"
        style="max-width: 760px"
        :title="modalTitle"
        :bordered="false"
        @update:show="emit('update:show', $event)"
    >
        <n-space vertical :size="12">
            <n-alert v-if="description" type="warning">
                {{ description }}
            </n-alert>

            <n-form label-placement="left" label-width="160">
                <n-form-item :label="t('formatForm.labels.barcode')">
                    <n-input
                        v-model:value="form.barcode"
                        :disabled="!allowBarcodeEdit"
                        :placeholder="t('formatForm.placeholders.barcode')"
                    />
                </n-form-item>

                <n-form-item :label="t('formatForm.labels.volumeName')">
                    <n-input
                        v-model:value="form.volumeName"
                        :placeholder="t('formatForm.placeholders.volumeName')"
                    />
                </n-form-item>

                <n-form-item :label="t('formatForm.labels.mediaPool')">
                    <n-input
                        v-model:value="form.mediaPool"
                        :placeholder="t('formatForm.placeholders.mediaPool')"
                    />
                </n-form-item>

                <n-form-item :label="t('formatForm.labels.extraPartitionCount')">
                    <n-input-number
                        v-model:value="form.extraPartitionCount"
                        :min="1"
                        :max="1"
                        style="width: 100%"
                    />
                </n-form-item>

                <n-form-item :label="t('formatForm.labels.blockSize')">
                    <n-input-number v-model:value="form.blockSize" :min="1" style="width: 100%" />
                </n-form-item>

                <n-form-item :label="t('formatForm.labels.immediateMode')">
                    <n-switch v-model:value="form.immediateMode" />
                </n-form-item>

                <n-form-item :label="t('formatForm.labels.capacity')">
                    <n-input-number
                        v-model:value="form.capacity"
                        :min="0"
                        :max="65535"
                        style="width: 100%"
                    />
                </n-form-item>

                <n-form-item :label="t('formatForm.labels.p0Size')">
                    <n-input-number
                        v-model:value="form.p0Size"
                        :min="1"
                        :max="65535"
                        style="width: 100%"
                    />
                </n-form-item>

                <n-form-item :label="t('formatForm.labels.p1Size')">
                    <n-input-number
                        v-model:value="form.p1Size"
                        :min="0"
                        :max="65535"
                        style="width: 100%"
                    />
                </n-form-item>

                <n-form-item :label="t('formatForm.labels.encryptionKey')">
                    <n-input
                        v-model:value="form.encryptionKey"
                        :placeholder="t('formatForm.placeholders.encryptionKey')"
                    />
                </n-form-item>
            </n-form>

            <n-alert type="info">
                {{ t('formatForm.encryptionHint') }}
            </n-alert>

            <n-space justify="end">
                <n-button @click="handleCancel">
                    {{ t('task.cancel') }}
                </n-button>
                <n-button
                    type="primary"
                    :loading="loading"
                    :disabled="!canSubmit"
                    @click="handleSubmit"
                >
                    {{ submitLabel }}
                </n-button>
            </n-space>
        </n-space>
    </n-modal>
</template>
