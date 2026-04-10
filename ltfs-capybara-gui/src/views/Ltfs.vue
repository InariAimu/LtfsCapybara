<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import { NAlert, NEmpty, NLayout, NLayoutHeader, NLayoutSider, NTree, useMessage } from 'naive-ui';
import type { TreeOption } from 'naive-ui';
import PathBar from './PathBar.vue';
import FileList from './FileList.vue';
import { tapeMachineApi, type TapeMachineSnapshot } from '@/api/modules/tapemachine';
import { localTapeApi } from '@/api/modules/localtapes';
import { useFileStore } from '@/stores/fileStore';
import { makeScopedPathKey, normalizePath } from '@/utils/path';
import formatFileSize from '@/utils/formatFileSize';
import { useI18n } from 'vue-i18n';

interface Props {
    tapeDriveId?: string | null;
}

interface LtfsTreeNode extends TreeOption {
    key: string;
    label: string;
    path: string;
    isLeaf?: boolean;
    children?: LtfsTreeNode[];
}

const props = defineProps<Props>();
const { t } = useI18n();
const message = useMessage();
const store = useFileStore();

const loading = ref(false);
const treeLoading = ref(false);
const snapshot = ref<TapeMachineSnapshot | null>(null);
const selectedKeys = ref<string[]>([]);
const expandedKeys = ref<string[]>([]);
const treeData = ref<LtfsTreeNode[]>([]);
const nodeMap = new Map<string, LtfsTreeNode>();

const currentTapeName = computed(() => snapshot.value?.loadedBarcode ?? '');
const noTapeLoaded = computed(() => !snapshot.value || snapshot.value.state === 'Empty' || !snapshot.value.loadedBarcode);
const unformatted = computed(() => Boolean(snapshot.value?.loadedBarcode) && snapshot.value?.hasLtfsFilesystem === false);
const ready = computed(() => Boolean(snapshot.value?.loadedBarcode) && snapshot.value?.hasLtfsFilesystem === true);
const canBrowse = computed(() => ready.value && Boolean(currentTapeName.value));

function resetTreeState() {
    treeData.value = [];
    selectedKeys.value = [];
    expandedKeys.value = [];
    nodeMap.clear();
    store.setFiles([]);
    store.setCurrentPath('/');
}

function createNode(path: string, label: string, isLeaf = false): LtfsTreeNode {
    return {
        key: makeScopedPathKey(currentTapeName.value, path),
        label,
        path,
        isLeaf,
        children: isLeaf ? undefined : [],
    };
}

function getNode(path: string) {
    return nodeMap.get(normalizePath(path));
}

function setNodeChildren(parentPath: string, children: LtfsTreeNode[]) {
    const normalizedParent = normalizePath(parentPath);
    const parent = getNode(normalizedParent);
    if (!parent) {
        return;
    }

    parent.children = children;
    parent.isLeaf = children.length === 0;
    for (const child of children) {
        nodeMap.set(normalizePath(child.path), child);
    }
}

async function fetchDirectory(path: string) {
    if (!currentTapeName.value) {
        return null;
    }

    const normalizedPath = normalizePath(path);
    const response =
        normalizedPath === '/'
            ? await localTapeApi.getRoot(currentTapeName.value)
            : await localTapeApi.getPath(currentTapeName.value, normalizedPath);
    return response.data;
}

function applyDirectoryItems(path: string, items: any[]) {
    const normalizedPath = normalizePath(path);
    const directories = items
        .filter(item => item.type !== 'file')
        .map(item => createNode(
            normalizedPath === '/' ? `/${item.name}` : `${normalizedPath}/${item.name}`,
            item.name,
        ));
    setNodeChildren(normalizedPath, directories);

    const files = items
        .filter(item => item.type === 'file')
        .map(item => ({
            ...item,
            key: item.index ?? `file:${normalizedPath}:${item.name}`,
            size: formatFileSize(Number(item.size) || 0),
        }));
    const mappedDirectories = items
        .filter(item => item.type !== 'file')
        .map(item => ({
            ...item,
            key: item.index ?? `dir:${normalizedPath}:${item.name}`,
            size: '-',
        }));

    store.setFiles([...mappedDirectories, ...files]);
}

async function loadPath(path: string, ensureAncestors = false) {
    if (!canBrowse.value) {
        return;
    }

    const normalizedPath = normalizePath(path);
    loading.value = true;
    try {
        if (ensureAncestors && normalizedPath !== '/') {
            const segments = normalizedPath.split('/').filter(Boolean);
            let currentPath = '/';
            for (const segment of segments) {
                const nextPath = currentPath === '/' ? `/${segment}` : `${currentPath}/${segment}`;
                if (!getNode(nextPath)) {
                    const currentData = await fetchDirectory(currentPath);
                    if (currentData) {
                        const dirs = (currentData.items || []).filter(item => item.type !== 'file');
                        const children = dirs.map(item => createNode(
                            currentPath === '/' ? `/${item.name}` : `${currentPath}/${item.name}`,
                            item.name,
                        ));
                        setNodeChildren(currentPath, children);
                    }
                }
                currentPath = nextPath;
            }
        }

        const data = await fetchDirectory(normalizedPath);
        if (!data) {
            message.error(t('messages.failedToLoadPath'));
            return;
        }

        applyDirectoryItems(normalizedPath, data.items || []);
        store.setCurrentLocation(currentTapeName.value, normalizedPath);
        selectedKeys.value = [makeScopedPathKey(currentTapeName.value, normalizedPath)];

        if (normalizedPath !== '/') {
            const paths: string[] = [];
            let current = normalizedPath;
            while (current !== '/') {
                current = current.includes('/') ? current.slice(0, current.lastIndexOf('/')) || '/' : '/';
                paths.push(makeScopedPathKey(currentTapeName.value, current));
            }
            expandedKeys.value = Array.from(new Set([...expandedKeys.value, ...paths]));
        }
    } catch (error) {
        console.error('Failed to load LTFS path', error);
        message.error(t('messages.unableToOpenPath', { path: normalizedPath }));
    } finally {
        loading.value = false;
    }
}

async function loadSnapshot() {
    if (!props.tapeDriveId || props.tapeDriveId === 'none') {
        snapshot.value = null;
        resetTreeState();
        return;
    }

    treeLoading.value = true;
    try {
        const response = await tapeMachineApi.getState(props.tapeDriveId);
        snapshot.value = response.data;

        if (!ready.value || !currentTapeName.value) {
            resetTreeState();
            return;
        }

        const rootNode = createNode('/', currentTapeName.value);
        treeData.value = [rootNode];
        nodeMap.clear();
        nodeMap.set('/', rootNode);
        store.setNoLtfsState(currentTapeName.value, false);
        await loadPath('/');
    } catch (error) {
        console.error('Failed to load LTFS tape snapshot', error);
        snapshot.value = null;
        resetTreeState();
    } finally {
        treeLoading.value = false;
    }
}

async function handleSelect(keys: Array<string | number>, options: Array<TreeOption | null>) {
    const option = options[0] as LtfsTreeNode | null;
    const key = keys[0];
    if (!option || typeof key !== 'string') {
        return;
    }

    selectedKeys.value = [key];
    await loadPath(option.path, true);
}

async function handleLoad(node: TreeOption) {
    const option = node as LtfsTreeNode;
    if (!option) {
        return;
    }

    await loadPath(option.path, true);
}

watch(
    () => props.tapeDriveId,
    () => {
        void loadSnapshot();
    },
    { immediate: true },
);

watch(
    () => store.localTapeListRevision,
    () => {
        if (canBrowse.value) {
            void loadPath(store.currentPath || '/');
        }
    },
);

watch(
    () => store.tapeDriveStateRevision,
    () => {
        void loadSnapshot();
    },
);

onMounted(() => {
    void loadSnapshot();
});
</script>

<template>
    <n-layout class="ltfs-page">
        <n-layout-header bordered>
            <path-bar
                :tape-name="currentTapeName"
                :path="store.currentPath"
                @navigate="loadPath($event, true)"
                @refresh="loadPath(store.currentPath || '/', true)"
            />
        </n-layout-header>
        <n-layout has-sider position="absolute" style="top: 43px; bottom: 0">
            <n-layout-sider bordered content-style="padding: 0px 5px 0 10px;">
                <n-empty v-if="treeLoading" :description="t('ltfs.loading')" style="margin-top: 20px" />
                <n-empty v-else-if="noTapeLoaded" :description="t('ltfs.notAvailable')" style="margin-top: 20px" />
                <n-empty v-else-if="unformatted" :description="t('ltfs.unformatted')" style="margin-top: 20px" />
                <n-tree
                    v-else
                    block-line
                    :data="treeData"
                    :selected-keys="selectedKeys"
                    :expanded-keys="expandedKeys"
                    :default-expand-all="true"
                    @update:selected-keys="handleSelect"
                    @load="handleLoad"
                    @update:expanded-keys="expandedKeys = $event as string[]"
                />
            </n-layout-sider>
            <n-layout>
                <div class="ltfs-content">
                    <n-alert v-if="noTapeLoaded" type="info" style="margin: 10px">
                        {{ t('ltfs.notAvailable') }}
                    </n-alert>
                    <n-alert v-else-if="unformatted" type="warning" style="margin: 10px">
                        {{ t('ltfs.unformatted') }}
                    </n-alert>
                    <file-list v-else class="file-list-content" :deletable="false" />
                </div>
            </n-layout>
        </n-layout>
    </n-layout>
</template>

<style scoped>
.ltfs-page {
    height: 100%;
}

.ltfs-content {
    height: 100%;
    display: flex;
    flex-direction: column;
}

.file-list-content {
    flex: 1;
    min-height: 0;
}
</style>
