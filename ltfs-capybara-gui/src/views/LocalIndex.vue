<script setup lang="ts">
import { NLayout, NLayoutHeader, NLayoutSider, useMessage } from 'naive-ui';
import { useI18n } from 'vue-i18n';
import PathBar from './PathBar.vue';
import FileTreeList from './FileTreeList.vue';
import FileList from './FileList.vue';
import { localTapeApi } from '@/api/modules/localtapes';
import formatFileSize from '@/utils/formatFileSize';
import { useFileStore } from '@/stores/fileStore';

const store = useFileStore();
const { t } = useI18n();
const message = useMessage();

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
                <file-list style="height: 100%" />
            </n-layout>
        </n-layout>
    </n-layout>
</template>

<style scoped>
.local-index-page {
    height: 100%;
}
</style>
