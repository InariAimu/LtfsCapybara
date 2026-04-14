<script setup lang="ts">
import { computed, ref } from 'vue';
import { NSwitch, NButton, NDropdown } from 'naive-ui';
import type { DropdownOption } from 'naive-ui';
import { useI18n } from 'vue-i18n';
import DirectoryChooseDialog from '@/components/DirectoryChooseDialog.vue';

const { t } = useI18n();

const {
    showTapeInfoToggle,
    showTapeInfo,
    addDisabled = false,
    deleteDisabled = false,
    readDisabled = true,
    verifyDisabled = true,
    showAddButton = true,
    showDeleteButton = true,
    showReadButton = false,
    showVerifyButton = false,
} = defineProps<{
    showTapeInfoToggle: boolean;
    showTapeInfo: boolean;
    addDisabled?: boolean;
    deleteDisabled?: boolean;
    readDisabled?: boolean;
    verifyDisabled?: boolean;
    showAddButton?: boolean;
    showDeleteButton?: boolean;
    showReadButton?: boolean;
    showVerifyButton?: boolean;
}>();

const emit = defineEmits<{
    (e: 'update:showTapeInfo', value: boolean): void;
    (e: 'add-server-folder', localPath: string): void;
    (e: 'delete-task'): void;
    (e: 'read-selected', localPath: string): void;
    (e: 'verify-selected'): void;
}>();

const showFolderDialog = ref(false);
const showReadTargetDialog = ref(false);

const addOptions = computed<DropdownOption[]>(() => [
    {
        label: t('task.addServerFiles'),
        key: 'add-server-files',
        disabled: true,
    },
    {
        label: t('task.addServerFolder'),
        key: 'add-server-folder',
        disabled: addDisabled,
    },
    { type: 'divider', key: 'divider' },
    {
        label: t('task.addLocalFiles'),
        key: 'add-local-files',
        disabled: true,
    },
    {
        label: t('task.addLocalFolder'),
        key: 'add-local-folder',
        disabled: true,
    },
]);

function handleSelect(key: string) {
    if (key === 'add-server-folder') {
        showFolderDialog.value = true;
    }
}

function handleFolderConfirm(localPath: string) {
    showFolderDialog.value = false;
    emit('add-server-folder', localPath);
}

function updateShowTapeInfo(value: boolean) {
    emit('update:showTapeInfo', value);
}

function handleReadConfirm(localPath: string) {
    showReadTargetDialog.value = false;
    emit('read-selected', localPath);
}
</script>

<template>
    <div class="action-bar">
        <n-switch
            v-if="showTapeInfoToggle"
            :value="showTapeInfo"
            @update:value="updateShowTapeInfo"
        >
            <template #checked> Tape Info </template>
            <template #unchecked> File List </template>
        </n-switch>
        <n-dropdown v-if="showAddButton" trigger="click" :options="addOptions" @select="handleSelect">
            <n-button size="small" :disabled="addDisabled">
                {{ t('task.add') }}
            </n-button>
        </n-dropdown>
        <n-button
            v-if="showReadButton"
            size="small"
            :disabled="readDisabled"
            @click="showReadTargetDialog = true"
        >
            {{ t('task.addReadTask') }}
        </n-button>
        <n-button
            v-if="showVerifyButton"
            size="small"
            type="warning"
            :disabled="verifyDisabled"
            @click="emit('verify-selected')"
        >
            {{ t('task.addVerifyTask') }}
        </n-button>
        <n-button
            v-if="showDeleteButton"
            size="small"
            type="error"
            :disabled="deleteDisabled"
            @click="emit('delete-task')"
        >
            {{ t('task.deleteTask') }}
        </n-button>

        <directory-choose-dialog
            v-model:show="showFolderDialog"
            :title="t('task.addServerFolderTitle')"
            @confirm="handleFolderConfirm"
            @cancel="showFolderDialog = false"
        />
        <directory-choose-dialog
            v-model:show="showReadTargetDialog"
            :title="t('task.readTargetFolderTitle')"
            @confirm="handleReadConfirm"
            @cancel="showReadTargetDialog = false"
        />
    </div>
</template>

<style scoped>
.action-bar {
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 8px 12px;
    border-bottom: 1px solid var(--n-border-color);
    background: var(--n-color);
}
</style>
