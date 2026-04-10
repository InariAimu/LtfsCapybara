<script setup lang="ts">
import { computed, h, onMounted, ref } from 'vue';
import {
    NLayout,
    NLayoutSider,
    NTree,
    NDataTable,
    NTag,
    NFlex,
    NText,
    NButton,
    NEmpty,
    NAlert,
    useMessage,
} from 'naive-ui';
import type { TreeOption, DataTableColumns } from 'naive-ui';
import { useI18n } from 'vue-i18n';
import { taskApi } from '@/api/modules/tasks';
import { useFileStore } from '@/stores/fileStore';
import { useExecutionStore } from '@/stores/executionStore';
import {
    getParentPath,
    getPathName,
    isDirectChild,
    makeScopedPathKey,
    normalizePath,
} from '@/utils/path';

const { t } = useI18n();
const message = useMessage();
const store = useFileStore();
const executionStore = useExecutionStore();

const isLoading = ref(false);
const selectedKeys = ref<string[]>([]);
const selectedTape = ref('');
const selectedPath = ref('/');

const activeExecution = computed(() =>
    executionStore.executions.find(execution => execution.tapeBarcode === selectedTape.value) ?? null,
);

const canExecute = computed(
    () => Boolean(selectedTape.value) && Boolean(store.currentTapeDriveId) && !executionStore.activeExecution,
);

// ─── Tree ────────────────────────────────────────────────────────────────────

interface TaskTreeNode extends TreeOption {
    tapeName: string;
    path: string;
    taskAction?: string;
}

function buildTree(groups: typeof store.taskGroups): TaskTreeNode[] {
    return groups.map(group => {
        const dirPaths = new Set<string>(['/']);
        const folderActionMap = new Map<string, string>();
        const sorted = [...group.tasks].sort((a, b) => a.createdAtTicks - b.createdAtTicks);

        for (const task of sorted) {
            const pathTask = task.pathTask;
            if (pathTask?.isDirectory) {
                const p = normalizePath(pathTask.path);
                dirPaths.add(p);
                folderActionMap.set(p, pathTask.operation);
                let anc = getParentPath(p);
                while (anc !== '/') {
                    dirPaths.add(anc);
                    anc = getParentPath(anc);
                }
            }
            if (pathTask && !pathTask.isDirectory) {
                let dir = getParentPath(normalizePath(pathTask.path));
                while (dir !== '/') {
                    dirPaths.add(dir);
                    dir = getParentPath(dir);
                }
            }
        }

        const nodeMap = new Map<string, TaskTreeNode>();
        const root: TaskTreeNode = {
            key: makeScopedPathKey(group.tapeBarcode, '/'),
            label: group.tapeBarcode,
            tapeName: group.tapeBarcode,
            path: '/',
            children: [],
        };
        nodeMap.set('/', root);

        [...dirPaths]
            .filter(p => p !== '/')
            .sort((a, b) => a.split('/').length - b.split('/').length)
            .forEach(p => {
                const node: TaskTreeNode = {
                    key: makeScopedPathKey(group.tapeBarcode, p),
                    label: getPathName(p),
                    tapeName: group.tapeBarcode,
                    path: p,
                    taskAction: folderActionMap.get(p),
                    children: [],
                };
                nodeMap.set(p, node);
            });

        for (const [p, node] of nodeMap) {
            if (p === '/') continue;
            const parentNode = nodeMap.get(getParentPath(p)) ?? root;
            (parentNode.children as TaskTreeNode[]).push(node);
        }

        for (const node of nodeMap.values()) {
            if ((node.children as TaskTreeNode[])?.length === 0) {
                node.isLeaf = true;
                delete node.children;
            }
        }

        return root;
    });
}

const treeData = computed(() => buildTree(store.taskGroups));

const renderLabel = ({ option }: { option: TreeOption }) => {
    const node = option as TaskTreeNode;
    if (!node.taskAction) return node.label as string;
    const type = node.taskAction === 'delete' ? 'error' : 'success';
    const text = node.taskAction === 'delete' ? t('task.actionRemove') : t('task.actionAdd');
    return h(
        NFlex,
        { align: 'center', size: 4, wrap: false },
        {
            default: () => [
                h(NText, null, { default: () => node.label as string }),
                h(NTag, { size: 'tiny', type }, { default: () => text }),
            ],
        },
    );
};

function handleUpdateSelectedKeys(
    keys: Array<string | number>,
    _options: Array<TreeOption | null>,
) {
    const key = keys[0] as string | undefined;
    if (!key) return;
    const sep = key.indexOf('::');
    if (sep === -1) return;
    selectedTape.value = key.substring(0, sep);
    selectedPath.value = key.substring(sep + 2);
    selectedKeys.value = [key];
}

// ─── Table ───────────────────────────────────────────────────────────────────

interface TaskTableRow {
    key: string;
    name: string;
    fullPath: string;
    itemType: 'file' | 'folder' | 'format';
    taskAction: string;
    createdAtTicks: number;
    taskId: string;
}

function rowSortOrder(row: TaskTableRow): number {
    if (row.itemType === 'format') return 0;
    if (row.itemType === 'folder') return 1;
    return 2;
}

function createTaskRow(
    task: { id: string; createdAtTicks: number },
    itemType: TaskTableRow['itemType'],
    fullPath: string,
    taskAction: string,
    name = itemType === 'format' ? t('task.typeFormat') : getPathName(fullPath),
): TaskTableRow {
    return {
        key: task.id,
        name,
        fullPath,
        itemType,
        taskAction,
        createdAtTicks: task.createdAtTicks,
        taskId: task.id,
    };
}

const tableRows = computed<TaskTableRow[]>(() => {
    if (!selectedTape.value) return [];
    const group = store.taskGroups.find(
        g => g.tapeBarcode.toLowerCase() === selectedTape.value.toLowerCase(),
    );
    if (!group) return [];

    const rows: TaskTableRow[] = [];

    if (selectedPath.value === '/') {
        for (const task of group.tasks) {
            if (task.type === 'format') {
                rows.push(createTaskRow(task, 'format', '/', 'format'));
            }
        }
    }

    for (const task of group.tasks) {
        const pathTask = task.pathTask;
        if (!pathTask) {
            continue;
        }

        const taskPath = normalizePath(pathTask.path);
        if (isDirectChild(selectedPath.value, taskPath)) {
            rows.push(
                createTaskRow(
                    task,
                    pathTask.isDirectory ? 'folder' : 'file',
                    taskPath,
                    pathTask.operation,
                ),
            );
        }
    }

    return rows.sort((a, b) => {
        const orderDiff = rowSortOrder(a) - rowSortOrder(b);
        if (orderDiff !== 0) return orderDiff;
        if (a.createdAtTicks !== b.createdAtTicks) return a.createdAtTicks - b.createdAtTicks;
        return a.fullPath.localeCompare(b.fullPath);
    });
});

function formatTimestamp(ticks: number): string {
    if (!ticks) return '-';
    const date = new Date(ticks / 10000 - 62135596800000);
    return Number.isNaN(date.getTime()) ? '-' : date.toLocaleString();
}

function actionTagType(
    action: string,
    itemType: string,
): 'success' | 'warning' | 'error' | 'info' | 'default' {
    if (itemType === 'format') return 'warning';
    switch (action.toLowerCase()) {
        case 'add':
            return 'success';
        case 'rename':
            return 'info';
        case 'replace':
        case 'update':
            return 'warning';
        case 'delete':
            return 'error';
        default:
            return 'default';
    }
}

function actionLabel(action: string, itemType: string): string {
    if (itemType === 'format') return t('task.typeFormat');
    switch (action.toLowerCase()) {
        case 'add':
            return t('task.actionAdd');
        case 'delete':
            return t('task.actionRemove');
        case 'rename':
            return 'rename';
        case 'replace':
        case 'update':
            return t('task.typeReplace');
        default:
            return action;
    }
}

const columns = computed<DataTableColumns<TaskTableRow>>(() => [
    {
        title: t('table.name'),
        key: 'name',
        ellipsis: { tooltip: true },
        render(row) {
            const icon = row.itemType === 'file' ? '📄' : row.itemType === 'format' ? '💾' : '📁';
            return `${icon} ${row.name}`;
        },
    },
    {
        title: t('table.taskType'),
        key: 'taskAction',
        width: 90,
        render(row) {
            return h(
                NTag,
                { size: 'small', type: actionTagType(row.taskAction, row.itemType) },
                { default: () => actionLabel(row.taskAction, row.itemType) },
            );
        },
    },
    {
        title: t('task.createdAt'),
        key: 'createdAtTicks',
        width: 160,
        render(row) {
            return formatTimestamp(row.createdAtTicks);
        },
    },
    {
        title: '',
        key: '_actions',
        width: 80,
        render(row) {
            return h(
                NButton,
                {
                    size: 'tiny',
                    type: 'error',
                    tertiary: true,
                    onClick: () => handleDeleteTask(row),
                },
                { default: () => t('task.deleteTask') },
            );
        },
    },
]);

// ─── Actions ─────────────────────────────────────────────────────────────────

async function loadTaskGroups() {
    isLoading.value = true;
    try {
        const res = await taskApi.listGroups();
        store.setTaskGroups(res.data ?? []);
    } catch (err) {
        console.error('loadTaskGroups error', err);
        message.error(t('task.loadTaskGroupsFailed'));
    } finally {
        isLoading.value = false;
    }
}

async function handleDeleteTask(row: TaskTableRow) {
    try {
        const res = await taskApi.deleteTask(selectedTape.value, row.taskId);
        store.upsertTaskGroup(res.data);
        message.success(t('task.deleteTaskSuccess'));
    } catch (err) {
        console.error('handleDeleteTask error', err);
        message.error(t('task.deleteTaskFailed'));
    }
}

async function handleExecuteTasks() {
    if (!selectedTape.value) {
        message.warning(t('task.selectNodeHint'));
        return;
    }

    if (!store.currentTapeDriveId) {
        message.warning(t('task.selectDriveFirst'));
        return;
    }

    try {
        const res = await taskApi.executeGroup(selectedTape.value, store.currentTapeDriveId);
        executionStore.upsertExecution(res.data);
        message.success(t('task.executionStarted'));
    } catch (err) {
        console.error('handleExecuteTasks error', err);
        message.error(t('task.executionStartFailed'));
    }
}

onMounted(loadTaskGroups);
</script>

<template>
    <n-layout class="task-page" has-sider>
        <n-layout-sider bordered content-style="padding: 8px 6px 8px 10px;">
            <n-flex vertical :size="8" style="height: 100%">
                <n-flex align="center" justify="space-between" style="flex-shrink: 0">
                    <n-text strong>{{ t('menu.task') }}</n-text>
                    <n-flex>
                        <n-button size="small" tertiary :loading="isLoading" @click="loadTaskGroups">
                            {{ t('task.refresh') }}
                        </n-button>
                        <n-button size="small" type="primary" :disabled="!canExecute" @click="handleExecuteTasks">
                            {{ t('task.execute') }}
                        </n-button>
                    </n-flex>
                </n-flex>
                <n-empty v-if="treeData.length === 0" :description="t('task.emptyGroups')" />
                <n-tree
                    v-else
                    block-line
                    :data="treeData"
                    :selected-keys="selectedKeys"
                    :default-expand-all="true"
                    :render-label="renderLabel"
                    @update:selected-keys="handleUpdateSelectedKeys"
                />
            </n-flex>
        </n-layout-sider>
        <n-layout content-style="padding: 10px;">
            <n-empty
                v-if="!selectedTape"
                :description="t('task.selectNodeHint')"
                style="margin-top: 40px"
            />
            <template v-else>
                <n-alert v-if="activeExecution" type="info" style="margin-bottom: 12px;">
                    <div>{{ activeExecution.status }}</div>
                    <div v-if="activeExecution.progress?.statusMessage">
                        {{ activeExecution.progress.statusMessage }}
                    </div>
                    <div v-if="activeExecution.pendingIncident?.message">
                        {{ activeExecution.pendingIncident.message }}
                    </div>
                </n-alert>
                <n-data-table :columns="columns" :data="tableRows" size="small" />
            </template>
        </n-layout>
    </n-layout>
</template>

<style scoped>
.task-page {
    height: 100%;
}
</style>
