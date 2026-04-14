<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import { NLayout, NLayoutHeader, NLayoutSider, NCard, NButton, NEmpty, useMessage } from 'naive-ui';
import { useI18n } from 'vue-i18n';
import FormatParamDialog from '@/components/FormatParamDialog.vue';
import PathBar from './PathBar.vue';
import FileTreeList from './FileTreeList.vue';
import FileList from './FileList.vue';
import ActionBar from './ActionBar.vue';
import TapeInfo from './TapeInfo.vue';
import { localTapeApi } from '@/api/modules/localtapes';
import {
    createDefaultTapeFsFormatParam,
    taskApi,
    type TapeFsFormatParam,
} from '@/api/modules/tasks';
import formatFileSize from '@/utils/formatFileSize';
import { useFileStore } from '@/stores/fileStore';
import { getPathName, normalizePath } from '@/utils/path';

const store = useFileStore();
const { t } = useI18n();
const message = useMessage();
const showTapeInfo = ref(false);
const showFormatTaskDialog = ref(false);
const formatTaskLoading = ref(false);
const treeRefreshToken = ref(0);

const isRootNodeSelected = computed(
    () => Boolean(store.currentTapeName) && store.currentPath === '/',
);
const currentTapeGroup = computed(() =>
    store.taskGroups.find(g => g.tapeBarcode.toLowerCase() === store.currentTapeName.toLowerCase()),
);
const currentPathTask = computed(() => {
    const tasks = currentTapeGroup.value?.tasks ?? [];
    const targetPath = normalizePath(store.currentPath);

    const matched = tasks.filter(task => {
        const path = task.pathTask?.path;
        if (!path) {
            return false;
        }
        return normalizePath(path) === targetPath;
    });

    if (matched.length === 0) {
        return null;
    }

    return matched.reduce((latest, task) =>
        task.createdAtTicks > latest.createdAtTicks ? task : latest,
    );
});
const canDeleteCurrentPathTask = computed(
    () => Boolean(store.currentTapeName) && Boolean(currentPathTask.value),
);
const currentTapeHasFormatTask = computed(() =>
    Boolean(currentTapeGroup.value?.tasks?.some(task => task.type === 'format')),
);
const currentTapeTaskRevision = computed(() => {
    const group = currentTapeGroup.value;
    if (!group) {
        return '';
    }

    const taskIds = group.tasks.map(task => task.id).join('|');
    return `${group.updatedAtTicks}:${group.tasks.length}:${taskIds}`;
});
const isCurrentTapeEditable = computed(() => {
    if (!store.currentTapeName) {
        return false;
    }

    if (!store.noLtfsFilesystem) {
        return true;
    }

    if (store.noLtfsTapeName.toLowerCase() !== store.currentTapeName.toLowerCase()) {
        return true;
    }

    return currentTapeHasFormatTask.value;
});
const showNoLtfsCard = computed(
    () =>
        !showTapeInfo.value &&
        isRootNodeSelected.value &&
        store.noLtfsFilesystem &&
        store.noLtfsTapeName.toLowerCase() === store.currentTapeName.toLowerCase() &&
        !currentTapeHasFormatTask.value,
);

watch(isRootNodeSelected, isRoot => {
    if (!isRoot) {
        showTapeInfo.value = false;
    }
});

watch(currentTapeTaskRevision, async (nextRevision, previousRevision) => {
    if (!store.currentTapeName || !nextRevision || !previousRevision) {
        return;
    }

    treeRefreshToken.value += 1;
    await navigateByPath(store.currentPath);
});

async function navigateByPath(path: string) {
    const tapeName = store.currentTapeName;
    if (!tapeName) {
        message.warning(t('messages.selectTapeFirst'));
        return;
    }

    const targetPath = normalizePath(path);
    store.setLoading(true);
    try {
        const res =
            targetPath === '/'
                ? await localTapeApi.getRoot(tapeName)
                : await localTapeApi.getPath(tapeName, targetPath);

        if (targetPath === '/') {
            store.setNoLtfsState(tapeName, false);
        }

        if (!(res && (res as any).data)) {
            message.error(t('messages.failedToLoadPath'));
            return;
        }

        const items = ((res as any).data.items || []) as any[];
        const directories = items
            .filter((item: any) => item.type !== 'file')
            .map((item: any) => ({
                ...item,
                key: item.index ?? `dir:${targetPath}:${item.name}`,
                size: '-',
            }));
        const files = items
            .filter((item: any) => item.type === 'file')
            .map((item: any) => ({
                ...item,
                key: item.index ?? `file:${targetPath}:${item.name}`,
                size: formatFileSize(Number(item.size) || 0),
            }));

        store.setFiles([...directories, ...files]);
        store.setCurrentPath(targetPath);
    } catch (err) {
        console.error('navigateByPath error', err);
        const status = (err as any)?.response?.status;
        const error = (err as any)?.response?.data?.error;
        const isNoLtfsFilesystemError =
            targetPath === '/' && status === 404 && error === 'No index files found for tape';
        if (isNoLtfsFilesystemError) {
            store.setNoLtfsState(tapeName, true);
            store.setFiles([]);
            return;
        }

        message.error(t('messages.unableToOpenPath', { path: targetPath }));
    } finally {
        store.setLoading(false);
    }
}

async function handleAddFormatTask() {
    const tapeName = store.currentTapeName;
    if (!tapeName) {
        return;
    }

    showFormatTaskDialog.value = true;
}

async function handleSubmitFormatTask(formatParam: TapeFsFormatParam) {
    const tapeName = store.currentTapeName;
    if (!tapeName) {
        return;
    }

    formatTaskLoading.value = true;
    try {
        const response = await taskApi.addFormatTask(tapeName, formatParam);
        store.upsertTaskGroup(response.data);
        showFormatTaskDialog.value = false;
        treeRefreshToken.value += 1;
        message.success(t('task.addFormatTaskSuccess'));
    } catch (err) {
        console.error('handleAddFormatTask error', err);
        message.error(t('task.addFormatTaskFailed'));
    } finally {
        formatTaskLoading.value = false;
    }
}

async function loadTaskGroups() {
    try {
        const response = await taskApi.listGroups();
        store.setTaskGroups(response.data ?? []);
    } catch (err) {
        console.error('loadTaskGroups error', err);
    }
}

async function handleAddServerFolder(localPath: string) {
    const tapeName = store.currentTapeName;
    if (!tapeName) {
        message.warning(t('messages.selectTapeFirst'));
        return;
    }

    if (!isCurrentTapeEditable.value) {
        message.warning(t('task.addFolderDisabled'));
        return;
    }

    const baseName = getPathName(localPath);
    if (!baseName) {
        message.warning(t('task.addServerFolderFailed'));
        return;
    }

    const parentTapePath = normalizePath(store.currentPath);
    const targetTapeRoot =
        parentTapePath === '/' ? `/${baseName}` : `${parentTapePath}/${baseName}`;

    // If target folder already exists in tape filesystem, do nothing.
    try {
        await localTapeApi.getPath(tapeName, targetTapeRoot);
        message.warning(t('task.addServerFolderExists'));
        return;
    } catch (err: any) {
        if (err?.response?.status !== 404) {
            console.error('handleAddServerFolder check error', err);
            message.error(t('task.addServerFolderFailed'));
            return;
        }
    }

    try {
        const response = await taskApi.addServerFolderTask(tapeName, {
            localPath,
            targetPath: targetTapeRoot,
        });
        store.upsertTaskGroup(response.data);

        treeRefreshToken.value += 1;
        message.success(t('task.addServerFolderSuccess'));
    } catch (err) {
        console.error('handleAddServerFolder error', err);
        message.error(t('task.addServerFolderFailed'));
    }
}

onMounted(async () => {
    await loadTaskGroups();
});

async function handleDeleteDir(name: string) {
    const tapeName = store.currentTapeName;
    if (!tapeName) return;

    const parentPath = normalizePath(store.currentPath);
    const targetPath = parentPath === '/' ? `/${name}` : `${parentPath}/${name}`;

    try {
        const response = await localTapeApi.deleteLocalIndexPath(tapeName, targetPath);
        store.upsertTaskGroup(response.data);
        await navigateByPath(parentPath);
        message.success(t('task.deleteDirSuccess'));
    } catch (err) {
        console.error('handleDeleteDir error', err);
        message.error(t('task.deleteDirFailed'));
    }
}

async function handleDeleteCurrentPathTask() {
    const tapeName = store.currentTapeName;
    const taskId = currentPathTask.value?.id;
    if (!tapeName || !taskId) {
        return;
    }

    try {
        const response = await taskApi.deleteTask(tapeName, taskId);
        store.upsertTaskGroup(response.data);
        treeRefreshToken.value += 1;
        await navigateByPath(store.currentPath);
        message.success(t('task.deleteTaskSuccess'));
    } catch (err) {
        console.error('handleDeleteCurrentPathTask error', err);
        message.error(t('task.deleteTaskFailed'));
    }
}
</script>

<template>
    <n-layout class="local-index-page">
        <n-layout-header bordered>
            <path-bar
                :tape-name="store.currentTapeName"
                :path="store.currentPath"
                @navigate="navigateByPath"
                @refresh="navigateByPath(store.currentPath)"
            />
        </n-layout-header>
        <n-layout has-sider position="absolute" style="top: 43px; bottom: 0">
            <n-layout-sider bordered content-style="padding: 0px 5px 0 10px;">
                <file-tree-list :show-tape-info="showTapeInfo" :refresh-token="treeRefreshToken" />
            </n-layout-sider>
            <n-layout>
                <div class="file-list-pane">
                    <action-bar
                        :show-tape-info-toggle="isRootNodeSelected"
                        :show-tape-info="showTapeInfo"
                        :show-add-button="true"
                        :show-delete-button="true"
                        :show-read-button="true"
                        :show-verify-button="true"
                        :add-disabled="!isCurrentTapeEditable"
                        :delete-disabled="!canDeleteCurrentPathTask"
                        @update:show-tape-info="showTapeInfo = $event"
                        @add-server-folder="handleAddServerFolder"
                        @delete-task="handleDeleteCurrentPathTask"
                    />
                    <div v-if="showNoLtfsCard" class="file-list-content no-ltfs-panel">
                        <n-card :title="t('task.noLtfsTitle')" size="small">
                            <n-empty :description="t('messages.noLtfsFilesystem')">
                                <template #extra>
                                    <n-button type="primary" @click="handleAddFormatTask">
                                        {{ t('task.addFormatTask') }}
                                    </n-button>
                                </template>
                            </n-empty>
                        </n-card>
                    </div>
                    <tape-info v-else-if="showTapeInfo" class="file-list-content" />
                    <file-list
                        v-else
                        class="file-list-content"
                        :deletable="isCurrentTapeEditable"
                        @delete-dir="handleDeleteDir"
                    />
                </div>
            </n-layout>
        </n-layout>

        <format-param-dialog
            v-model:show="showFormatTaskDialog"
            :loading="formatTaskLoading"
            :title="t('task.configureFormatTask')"
            :submit-text="t('task.addFormatTask')"
            :description="t('task.configureFormatTaskHint')"
            :initial-format-param="
                createDefaultTapeFsFormatParam(store.currentTapeName, store.currentTapeName)
            "
            :barcode="store.currentTapeName"
            :volume-name="store.currentTapeName"
            @submit="handleSubmitFormatTask"
        />
    </n-layout>
</template>

<style scoped>
.local-index-page {
    height: 100%;
}

.file-list-pane {
    height: 100%;
    display: flex;
    flex-direction: column;
}

.file-list-content {
    flex: 1;
    min-height: 0;
}

.no-ltfs-panel {
    padding: 10px;
}
</style>
