<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { NCard, NEmpty, NFlex, NInput, NButton, NTag, NText, useMessage } from 'naive-ui';
import { useI18n } from 'vue-i18n';
import { taskApi, type LtfsTaskGroup, type LtfsTaskItem } from '@/api/modules/tasks';
import { useFileStore } from '@/stores/fileStore';

const { t } = useI18n();
const message = useMessage();
const store = useFileStore();

const isLoading = ref(false);
const editingBarcode = ref('');
const editingName = ref('');

const groups = computed(() =>
    [...store.taskGroups].sort((a, b) => a.tapeBarcode.localeCompare(b.tapeBarcode)),
);

function formatTimestamp(ticks: number): string {
    if (!ticks) {
        return '-';
    }

    const date = new Date(ticks / 10000 - 62135596800000);
    return Number.isNaN(date.getTime()) ? '-' : date.toLocaleString();
}

function taskTypeLabel(taskType: string): string {
    switch (taskType) {
        case 'write':
            return t('task.typeWrite');
        case 'replace':
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

async function loadTaskGroups() {
    isLoading.value = true;
    try {
        const response = await taskApi.listGroups();
        store.setTaskGroups(response.data ?? []);
    } catch (err) {
        console.error('loadTaskGroups error', err);
        message.error(t('task.loadTaskGroupsFailed'));
    } finally {
        isLoading.value = false;
    }
}

function beginRename(group: LtfsTaskGroup) {
    editingBarcode.value = group.tapeBarcode;
    editingName.value = group.name;
}

function cancelRename() {
    editingBarcode.value = '';
    editingName.value = '';
}

async function saveRename(group: LtfsTaskGroup) {
    const name = editingName.value.trim();
    if (!name) {
        message.warning(t('task.groupNameRequired'));
        return;
    }

    try {
        const response = await taskApi.renameGroup(group.tapeBarcode, name);
        store.upsertTaskGroup(response.data);
        cancelRename();
        message.success(t('task.renameGroupSuccess'));
    } catch (err) {
        console.error('saveRename error', err);
        message.error(t('task.renameGroupFailed'));
    }
}

async function deleteTask(group: LtfsTaskGroup, task: LtfsTaskItem) {
    try {
        const response = await taskApi.deleteTask(group.tapeBarcode, task.id);
        store.upsertTaskGroup(response.data);
        message.success(t('task.deleteTaskSuccess'));
    } catch (err) {
        console.error('deleteTask error', err);
        message.error(t('task.deleteTaskFailed'));
    }
}

onMounted(async () => {
    await loadTaskGroups();
});
</script>

<template>
    <div class="task-page">
        <n-card :title="t('menu.task')" size="small" :segmented="{ content: true }">
            <template #header-extra>
                <n-button tertiary size="small" :loading="isLoading" @click="loadTaskGroups">
                    {{ t('task.refresh') }}
                </n-button>
            </template>

            <n-empty v-if="groups.length === 0" :description="t('task.emptyGroups')" />

            <div v-else class="group-list">
                <n-card
                    v-for="group in groups"
                    :key="group.tapeBarcode"
                    size="small"
                    class="group-card"
                    :title="group.tapeBarcode"
                >
                    <n-flex align="center" justify="space-between" style="margin-bottom: 8px">
                        <n-flex align="center">
                            <n-text depth="3">{{ t('task.groupName') }}</n-text>
                            <template v-if="editingBarcode === group.tapeBarcode">
                                <n-input v-model:value="editingName" size="small" style="width: 220px" />
                                <n-button size="small" type="primary" @click="saveRename(group)">
                                    {{ t('task.save') }}
                                </n-button>
                                <n-button size="small" @click="cancelRename">
                                    {{ t('task.cancel') }}
                                </n-button>
                            </template>
                            <template v-else>
                                <n-text>{{ group.name }}</n-text>
                            </template>
                        </n-flex>
                        <n-button
                            v-if="editingBarcode !== group.tapeBarcode"
                            size="small"
                            tertiary
                            @click="beginRename(group)"
                        >
                            {{ t('task.renameGroup') }}
                        </n-button>
                    </n-flex>

                    <n-empty v-if="group.tasks.length === 0" :description="t('task.emptyTasks')" />

                    <n-flex v-else vertical :size="8">
                        <n-card
                            v-for="task in group.tasks"
                            :key="task.id"
                            size="small"
                            embedded
                            class="task-item"
                        >
                            <n-flex align="center" justify="space-between" :wrap="false">
                                <n-flex align="center" :wrap="false">
                                    <n-tag size="small" type="info">
                                        {{ taskTypeLabel(task.type) }}
                                    </n-tag>
                                    <n-text depth="3">{{ task.id }}</n-text>
                                </n-flex>

                                <n-button size="tiny" type="error" tertiary @click="deleteTask(group, task)">
                                    {{ t('task.deleteTask') }}
                                </n-button>
                            </n-flex>
                            <n-text depth="3" style="display: block; margin-top: 6px">
                                {{ t('task.createdAt') }}: {{ formatTimestamp(task.createdAtTicks) }}
                            </n-text>
                        </n-card>
                    </n-flex>
                </n-card>
            </div>
        </n-card>
    </div>
</template>

<style scoped>
.task-page {
    padding: 10px;
}

.group-list {
    display: flex;
    flex-direction: column;
    gap: 10px;
}

.group-card {
    border-radius: 8px;
}

.task-item {
    border: 1px solid var(--n-border-color);
}
</style>
