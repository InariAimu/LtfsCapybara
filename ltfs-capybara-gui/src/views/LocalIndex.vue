<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { NLayout, NLayoutHeader, NLayoutSider, useMessage } from 'naive-ui';
import { useI18n } from 'vue-i18n';
import PathBar from './PathBar.vue';
import FileTreeList from './FileTreeList.vue';
import FileList from './FileList.vue';
import ActionBar from './ActionBar.vue';
import TapeInfo from './TapeInfo.vue';
import { localTapeApi } from '@/api/modules/localtapes';
import formatFileSize from '@/utils/formatFileSize';
import { useFileStore } from '@/stores/fileStore';

const store = useFileStore();
const { t } = useI18n();
const message = useMessage();
const showTapeInfo = ref(false);
const isRootNodeSelected = computed(() => Boolean(store.currentTapeName) && store.currentPath === '/');

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
        message.error(t('messages.unableToOpenPath', { path: targetPath }));
    } finally {
        store.setLoading(false);
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
        <n-layout has-sider position="absolute" style="top: 42px; bottom: 0">
            <n-layout-sider bordered content-style="padding: 5px 10px 0 10px;">
                <file-tree-list />
            </n-layout-sider>
            <n-layout>
                <div class="file-list-pane">
                    <action-bar
                        :show-tape-info-toggle="isRootNodeSelected"
                        :show-tape-info="showTapeInfo"
                        @update:show-tape-info="showTapeInfo = $event"
                    />
                    <tape-info v-if="showTapeInfo" class="file-list-content" />
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
</style>
