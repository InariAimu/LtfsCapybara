<script setup lang="ts">
import { NSwitch, NButton } from 'naive-ui';

const props = defineProps<{
    showTapeInfoToggle: boolean;
    showTapeInfo: boolean;
}>();

const emit = defineEmits<{
    (e: 'update:showTapeInfo', value: boolean): void;
}>();

function updateShowTapeInfo(value: boolean) {
    emit('update:showTapeInfo', value);
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
        <n-button :size="'small'"> Placeholder Action </n-button>
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
