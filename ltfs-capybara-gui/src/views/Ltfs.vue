<script setup lang="ts">
import { computed, h, onMounted, ref, watch } from 'vue';
import { NAlert, NEmpty, NLayout, NLayoutHeader, NLayoutSider, NTree, useMessage } from 'naive-ui';
import type { TreeOption } from 'naive-ui';
import PathBar from './PathBar.vue';
import FileList from './FileList.vue';
import ActionBar from './ActionBar.vue';
import { tapeMachineApi, type TapeMachineSnapshot } from '@/api/modules/tapemachine';
import { localTapeApi } from '@/api/modules/localtapes';
import { taskApi } from '@/api/modules/tasks';
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
const noTapeLoaded = computed(
    () => !snapshot.value || snapshot.value.state === 'Empty' || !snapshot.value.loadedBarcode,
);
const unformatted = computed(
    () => Boolean(snapshot.value?.loadedBarcode) && snapshot.value?.hasLtfsFilesystem === false,
);
const ready = computed(
    () => Boolean(snapshot.value?.loadedBarcode) && snapshot.value?.hasLtfsFilesystem === true,
);
const canBrowse = computed(() => ready.value && Boolean(currentTapeName.value));
const selectedFileItems = computed(() => {
    const selectedKeySet = new Set(store.selectedFileKeys.map(key => String(key)));
    return store.files.filter(item => selectedKeySet.has(String(item.key)));
});
const canQueueSelection = computed(() => canBrowse.value && selectedFileItems.value.length > 0);

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

    treeData.value = [...treeData.value];
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
        .map(item =>
            createNode(
                normalizedPath === '/' ? `/${item.name}` : `${normalizedPath}/${item.name}`,
                item.name,
            ),
        );
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

function buildTapeItemPath(name: string) {
    const currentPath = normalizePath(store.currentPath || '/');
    return currentPath === '/' ? `/${name}` : `${currentPath}/${name}`;
}

interface QueuedReadTaskEntry {
    sourcePath: string;
    targetPath: string;
    isDirectoryMarker: boolean;
}

interface QueuedVerifyTaskEntry {
    sourcePath: string;
    isDirectoryMarker: boolean;
}

async function collectVerifySourcePaths(path: string): Promise<string[]> {
    const normalizedPath = normalizePath(path);

    const data = await fetchDirectory(normalizedPath);
    if (!data) {
        return [];
    }

    const verifyPaths: string[] = [];
    for (const item of data.items || []) {
        const itemPath = normalizedPath === '/' ? `/${item.name}` : `${normalizedPath}/${item.name}`;
        if (item.type === 'file') {
            verifyPaths.push(itemPath);
            continue;
        }

        const childPaths = await collectVerifySourcePaths(itemPath);
        verifyPaths.push(...childPaths);
    }

    return verifyPaths;
}

async function collectReadTaskEntries(
    sourcePath: string,
    targetPath: string,
): Promise<QueuedReadTaskEntry[]> {
    const normalizedSourcePath = normalizePath(sourcePath);
    const entries: QueuedReadTaskEntry[] = [
        {
            sourcePath: normalizedSourcePath,
            targetPath,
            isDirectoryMarker: true,
        },
    ];
    const data = await fetchDirectory(normalizedSourcePath);
    if (!data) {
        return entries;
    }

    for (const item of data.items || []) {
        const itemSourcePath =
            normalizedSourcePath === '/' ? `/${item.name}` : `${normalizedSourcePath}/${item.name}`;
        const itemTargetPath = buildLocalTargetPath(targetPath, String(item.name ?? ''));
        if (item.type === 'file') {
            entries.push({
                sourcePath: itemSourcePath,
                targetPath: itemTargetPath,
                isDirectoryMarker: false,
            });
            continue;
        }

        const childEntries = await collectReadTaskEntries(itemSourcePath, itemTargetPath);
        entries.push(...childEntries);
    }

    return entries;
}

async function resolveReadTaskEntries(targetRootPath: string) {
    const entries: QueuedReadTaskEntry[] = [];
    for (const item of selectedFileItems.value) {
        const sourcePath = buildTapeItemPath(String(item.name ?? ''));
        const targetPath = buildLocalTargetPath(targetRootPath, String(item.name ?? ''));
        if (item.type === 'file') {
            entries.push({
                sourcePath,
                targetPath,
                isDirectoryMarker: false,
            });
            continue;
        }

        const childEntries = await collectReadTaskEntries(sourcePath, targetPath);
        entries.push(...childEntries);
    }

    return Array.from(
        new Map(
            entries.map(entry => [
                `${entry.isDirectoryMarker ? 'dir' : 'file'}|${entry.sourcePath}|${entry.targetPath}`,
                entry,
            ]),
        ).values(),
    );
}

async function resolveVerifyTaskEntries() {
    const entries: QueuedVerifyTaskEntry[] = [];
    for (const item of selectedFileItems.value) {
        const sourcePath = buildTapeItemPath(String(item.name ?? ''));
        if (item.type === 'file') {
            entries.push({
                sourcePath,
                isDirectoryMarker: false,
            });
            continue;
        }

        entries.push({
            sourcePath,
            isDirectoryMarker: true,
        });

        const childPaths = await collectVerifySourcePaths(sourcePath);
        entries.push(
            ...childPaths.map(path => ({
                sourcePath: path,
                isDirectoryMarker: false,
            })),
        );
    }

    return Array.from(
        new Map(
            entries.map(entry => [
                `${entry.isDirectoryMarker ? 'dir' : 'file'}|${entry.sourcePath}`,
                entry,
            ]),
        ).values(),
    );
}

function buildLocalTargetPath(basePath: string, name: string) {
    const trimmedPath = (basePath || '').trim();
    if (!trimmedPath) {
        return name;
    }

    return /[\\/]$/.test(trimmedPath) ? `${trimmedPath}${name}` : `${trimmedPath}\\${name}`;
}

async function handleAddReadTasks(targetRootPath: string) {
    if (!currentTapeName.value || selectedFileItems.value.length === 0) {
        message.warning(t('task.selectItemsFirst'));
        return;
    }

    let readEntries: QueuedReadTaskEntry[] = [];
    try {
        readEntries = await resolveReadTaskEntries(targetRootPath);
    } catch (error) {
        console.error('Failed to resolve LTFS read sources', error);
        message.error(t('task.addReadTaskFailed'));
        return;
    }

    if (readEntries.length === 0) {
        message.warning(t('task.selectItemsFirst'));
        return;
    }

    let queuedCount = 0;
    let hasFailure = false;
    for (const entry of readEntries) {
        try {
            const response = await taskApi.addReadTask(currentTapeName.value, {
                sourcePath: entry.sourcePath,
                targetPath: entry.targetPath,
                isDirectoryMarker: entry.isDirectoryMarker,
            });
            store.upsertTaskGroup(response.data);
            queuedCount += 1;
        } catch (error) {
            hasFailure = true;
            console.error('Failed to add LTFS read task', error);
        }
    }

    if (queuedCount > 0) {
        message.success(t('task.addReadTaskSuccess', { count: queuedCount }));
    }

    if (hasFailure) {
        message.error(t('task.addReadTaskFailed'));
    }
}

async function handleAddVerifyTasks() {
    if (!currentTapeName.value || selectedFileItems.value.length === 0) {
        message.warning(t('task.selectItemsFirst'));
        return;
    }

    let verifyEntries: QueuedVerifyTaskEntry[] = [];
    try {
        verifyEntries = await resolveVerifyTaskEntries();
    } catch (error) {
        console.error('Failed to resolve LTFS verify sources', error);
        message.error(t('task.addVerifyTaskFailed'));
        return;
    }

    if (verifyEntries.length === 0) {
        message.warning(t('task.selectItemsFirst'));
        return;
    }

    let queuedCount = 0;
    let hasFailure = false;
    for (const entry of verifyEntries) {
        try {
            const response = await taskApi.addVerifyTask(currentTapeName.value, {
                sourcePath: entry.sourcePath,
                isDirectoryMarker: entry.isDirectoryMarker,
            });
            store.upsertTaskGroup(response.data);
            queuedCount += 1;
        } catch (error) {
            hasFailure = true;
            console.error('Failed to add LTFS verify task', error);
        }
    }

    if (queuedCount > 0) {
        message.success(t('task.addVerifyTaskSuccess', { count: queuedCount }));
    }

    if (hasFailure) {
        message.error(t('task.addVerifyTaskFailed'));
    }
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
                        const children = dirs.map(item =>
                            createNode(
                                currentPath === '/'
                                    ? `/${item.name}`
                                    : `${currentPath}/${item.name}`,
                                item.name,
                            ),
                        );
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

        const rootKey = makeScopedPathKey(currentTapeName.value, '/');
        const nextExpandedKeys = new Set<string>([...expandedKeys.value, rootKey]);

        if (normalizedPath !== '/') {
            nextExpandedKeys.add(makeScopedPathKey(currentTapeName.value, normalizedPath));
            let current = normalizedPath;
            while (current !== '/') {
                current = current.includes('/')
                    ? current.slice(0, current.lastIndexOf('/')) || '/'
                    : '/';
                nextExpandedKeys.add(makeScopedPathKey(currentTapeName.value, current));
            }
        }

        expandedKeys.value = Array.from(nextExpandedKeys);
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

function renderLabel({ option }: { option: TreeOption }) {
    const path = normalizePath(((option as LtfsTreeNode).path as string) || '/');
    if (path === '/') {
        return h('span', { class: 'ltfs-tree-root-label' }, String(option.label ?? ''));
    }

    return String(option.label ?? '');
}

function handleExpandedKeys(keys: Array<string | number>) {
    expandedKeys.value = keys.map(key => String(key));
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
                <n-empty
                    v-if="treeLoading"
                    :description="t('ltfs.loading')"
                    style="margin-top: 20px"
                />
                <n-empty
                    v-else-if="noTapeLoaded"
                    :description="t('ltfs.notAvailable')"
                    style="margin-top: 20px"
                />
                <n-empty
                    v-else-if="unformatted"
                    :description="t('ltfs.unformatted')"
                    style="margin-top: 20px"
                />
                <n-tree
                    v-else
                    class="ltfs-tree"
                    block-line
                    expand-on-click
                    :show-line="true"
                    :indent="12"
                    :data="treeData"
                    :render-label="renderLabel"
                    :selected-keys="selectedKeys"
                    :expanded-keys="expandedKeys"
                    @update:selected-keys="handleSelect"
                    :on-load="handleLoad"
                    @update:expanded-keys="handleExpandedKeys"
                />
            </n-layout-sider>
            <n-layout>
                <div class="ltfs-content">
                    <action-bar
                        :show-tape-info-toggle="false"
                        :show-tape-info="false"
                        :show-add-button="true"
                        :show-delete-button="true"
                        :show-read-button="true"
                        :show-verify-button="true"
                        :read-disabled="!canQueueSelection"
                        :verify-disabled="!canQueueSelection"
                        @read-selected="handleAddReadTasks"
                        @verify-selected="handleAddVerifyTasks"
                    />
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

.ltfs-tree {
    min-width: 0;
}

.ltfs-tree :deep(.n-scrollbar-container) {
    overflow-x: hidden !important;
}

.ltfs-tree :deep(.n-tree-node-content) {
    width: 100%;
    min-width: 0;
    white-space: nowrap;
}

.ltfs-tree :deep(.n-tree-node-content__text) {
    overflow: hidden;
    text-overflow: ellipsis;
}

.ltfs-tree-root-label {
    font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, 'Liberation Mono', monospace;
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
