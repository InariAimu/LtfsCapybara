<script setup lang="ts">
import { NSwitch, NButton } from 'naive-ui';
import { useI18n } from 'vue-i18n';

const { t } = useI18n();

const { showTapeInfoToggle, showTapeInfo } = defineProps<{
    showTapeInfoToggle: boolean;
    showTapeInfo: boolean;
    addFolderDisabled?: boolean;
}>();

const emit = defineEmits<{
    (e: 'update:showTapeInfo', value: boolean): void;
    (e: 'add-folder'): void;
}>();

function updateShowTapeInfo(value: boolean) {
    emit('update:showTapeInfo', value);
}

function handleAddFolder() {
    emit('add-folder');
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
        <n-button :size="'small'" :disabled="addFolderDisabled" @click="handleAddFolder">
            {{ t('task.addFolder') }}
        </n-button>
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
