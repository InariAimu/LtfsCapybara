<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { NLayout, NLayoutHeader, NLayoutSider, NCard, NButton, NEmpty, useMessage } from 'naive-ui';
import { useI18n } from 'vue-i18n';
import PathBar from './PathBar.vue';
import FileTreeList from './FileTreeList.vue';
import FileList from './FileList.vue';
import ActionBar from './ActionBar.vue';
import TapeInfo from './TapeInfo.vue';
import { localTapeApi } from '@/api/modules/localtapes';
import { taskApi } from '@/api/modules/tasks';
import formatFileSize from '@/utils/formatFileSize';
import { useFileStore } from '@/stores/fileStore';

const store = useFileStore();
const { t } = useI18n();
const message = useMessage();
const showTapeInfo = ref(false);
const isRootNodeSelected = computed(
    () => Boolean(store.currentTapeName) && store.currentPath === '/',
);
const showNoLtfsCard = computed(
    () =>
        isRootNodeSelected.value &&
        store.noLtfsFilesystem &&
        store.noLtfsTapeName.toLowerCase() === store.currentTapeName.toLowerCase(),
);

watch(isRootNodeSelected, isRoot => {
    if (!isRoot) {
        showTapeInfo.value = false;
    }
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

        const files = (((res as any).data.items || []) as any[])
            .filter((item: any) => item.type === 'file')
            .map((item: any) => ({
                ...item,
                key: item.index,
                size: formatFileSize(Number(item.size) || 0),
            }));

        store.setFiles(files);
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

    try {
        const response = await taskApi.addFormatTask(tapeName, {
            barcode: tapeName,
            volumeName: tapeName,
            extraPartitionCount: 1,
            blockSize: 524288,
            immediateMode: true,
            capacity: 65535,
            p0Size: 1,
            p1Size: 65535,
        });
        store.upsertTaskGroup(response.data);
        message.success(t('task.addFormatTaskSuccess'));
    } catch (err) {
        console.error('handleAddFormatTask error', err);
        message.error(t('task.addFormatTaskFailed'));
    }
}

function normalizePath(path: string): string {
    const trimmed = (path || '/').trim();
    if (!trimmed) {
        return '/';
    }

    const withSlashes = trimmed.replace(/\\/g, '/');
    const compact = withSlashes.replace(/\/{2,}/g, '/');
    return compact.startsWith('/') ? compact : `/${compact}`;
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
                <file-tree-list :show-tape-info="showTapeInfo" />
            </n-layout-sider>
            <n-layout>
                <div class="file-list-pane">
                    <action-bar
                        :show-tape-info-toggle="isRootNodeSelected"
                        :show-tape-info="showTapeInfo"
                        @update:show-tape-info="showTapeInfo = $event"
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
                    <file-list v-else class="file-list-content" />
                </div>
            </n-layout>
        </n-layout>
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
