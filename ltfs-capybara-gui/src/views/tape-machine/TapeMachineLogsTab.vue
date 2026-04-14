<script setup lang="ts">
import { useI18n } from 'vue-i18n';
import { NCard, NEmpty, NSpace, NTag, NThing } from 'naive-ui';
import type { TaskExecutionLogEntry } from '@/api/modules/tasks';

interface Props {
    driveLogs: TaskExecutionLogEntry[];
}

defineProps<Props>();

const { t } = useI18n();

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
</script>

<template>
    <n-space vertical :size="8">
        <n-empty v-if="driveLogs.length === 0" :description="t('tapeMachine.logEmpty')" />
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
</template>