<script setup lang="ts">
import { onMounted, h, ref, watch, computed } from 'vue';
import {
    NTree,
    TreeOption,
    DropdownOption,
    NTag,
    NFlex,
    NButton,
    NText,
    NSelect,
    NInput,
} from 'naive-ui';
import { useI18n } from 'vue-i18n';
import { useFileStore } from '@/stores/fileStore';
import { localTapeApi } from '@/api/modules/localtapes';
import formatFileSize from '@/utils/formatFileSize';
import { getLtoFormatStyle } from '@/utils/tapeFormatStyle';
import { useMessage } from 'naive-ui';

interface LocalTapeSummary {
    tapeName: string;
    generation: number;
    vendor: string;
    particleType: string;
    freeSizeBytes: number;
    totalSizeBytes: number;
}

type LocalTapeEntry = LocalTapeSummary | string;

interface LocalTreeNode {
    label: string;
    key: string;
    tapeName: string;
    path: string;
    taskType?: string;
    isLeaf: boolean;
    children?: LocalTreeNode[];
    tapeSummary?: LocalTapeSummary | null;
    suffix?: () => ReturnType<typeof h>;
}

const data = ref<LocalTreeNode[]>([]);
const props = withDefaults(
    defineProps<{
        showTapeInfo?: boolean;
        refreshToken?: number;
    }>(),
    {
        showTapeInfo: false,
        refreshToken: 0,
    },
);
const store = useFileStore();
const optionsRef = ref<DropdownOption[]>([]);
const xRef = ref(0);
const yRef = ref(0);
const showDropdownRef = ref(false);
const selectedKeys = ref<Array<string | number>>([]);
const expandedKeys = ref<Array<string | number>>([]);
const nodeChainIndex = new Map<string, Array<string | number>>();
const pendingPathRequests = new Map<string, Promise<any>>();
let latestSelectionRequest = 0;

const filterGeneration = ref<number | null>(null);
const filterText = ref('');
const isLoading = ref(false);

const generationOptions = computed(() =>
    Array.from(
        new Set(
            data.value.map(n => n.tapeSummary?.generation).filter((g): g is number => g != null),
        ),
    )
        .sort((a, b) => a - b)
        .map(g => ({ label: `L${g}`, value: g })),
);

const visibleData = computed(() => {
    const keyword = filterText.value.trim().toLowerCase();

    return data.value.filter(n => {
        const matchGeneration =
            filterGeneration.value === null || n.tapeSummary?.generation === filterGeneration.value;
        const matchText =
            keyword.length === 0 ||
            n.label.toLowerCase().includes(keyword) ||
            n.tapeName.toLowerCase().includes(keyword);

        return matchGeneration && matchText;
    });
});

const tapeCount = computed(() => visibleData.value.length);

const totalFreeSpace = computed(() =>
    visibleData.value.reduce((sum, n) => sum + (Number(n.tapeSummary?.freeSizeBytes) || 0), 0),
);

const totalSpace = computed(() =>
    visibleData.value.reduce((sum, n) => sum + (Number(n.tapeSummary?.totalSizeBytes) || 0), 0),
);
const { t } = useI18n();
const message = useMessage();

const totalGradient = computed(() => {
    const usedPercent =
        totalSpace.value > 0
            ? Math.min(
                  100,
                  Math.max(0, ((totalSpace.value - totalFreeSpace.value) / totalSpace.value) * 100),
              )
            : 0;
    return `linear-gradient(90deg, #18a05855 0%, #18a05855 ${usedPercent}%, #fff5 ${usedPercent}%, #fff5 100%)`;
});

function normalizePath(path: string): string {
    const trimmed = (path || '/').trim().replace(/\\/g, '/');
    if (!trimmed || trimmed === '/') {
        return '/';
    }

    const compact = trimmed.replace(/\/{2,}/g, '/');
    return compact.startsWith('/') ? compact : `/${compact}`;
}

function makeLookupKey(tapeName: string, path: string): string {
    return `${tapeName}::${normalizePath(path)}`;
}

function makeNodeKey(tapeName: string, path: string): string {
    return makeLookupKey(tapeName, path);
}

function requestTapePath(tapeName: string, path: string): Promise<any> {
    const normalizedPath = normalizePath(path);
    const requestKey = makeLookupKey(tapeName, normalizedPath);
    const pending = pendingPathRequests.get(requestKey);
    if (pending) {
        return pending;
    }

    const request =
        normalizedPath === '/'
            ? localTapeApi.getRoot(tapeName)
            : localTapeApi.getPath(tapeName, normalizedPath);

    pendingPathRequests.set(requestKey, request);
    request.finally(() => {
        if (pendingPathRequests.get(requestKey) === request) {
            pendingPathRequests.delete(requestKey);
        }
    });

    return request;
}

function updatePathIndex(nodes: LocalTreeNode[]) {
    nodeChainIndex.clear();

    const walk = (items: LocalTreeNode[], chain: Array<string | number>) => {
        for (const node of items) {
            const nextChain = [...chain, node.key];
            nodeChainIndex.set(makeLookupKey(node.tapeName, node.path), nextChain);
            if (node.children && node.children.length > 0) {
                walk(node.children, nextChain);
            }
        }
    };

    walk(nodes, []);
}

function getRootNodeKeys(): Array<string | number> {
    return data.value.filter(node => normalizePath(node.path) === '/').map(node => node.key);
}

function ensureRootNodesCollapsed() {
    if (!props.showTapeInfo) {
        return;
    }

    const rootKeys = getRootNodeKeys();
    if (rootKeys.length === 0) {
        return;
    }

    const rootKeySet = new Set(rootKeys);
    expandedKeys.value = expandedKeys.value.filter(key => !rootKeySet.has(key));
    store.setLocalIndexExpandedKeys(expandedKeys.value);
}

function syncSelectionFromStore() {
    const tapeName = store.currentTapeName;
    const targetPath = normalizePath(store.currentPath || '/');

    if (!tapeName) {
        selectedKeys.value = [];
        store.setLocalIndexSelectedKeys([]);
        return;
    }

    const chain = nodeChainIndex.get(makeLookupKey(tapeName, targetPath));
    if (!chain || chain.length === 0) {
        return;
    }

    selectedKeys.value = [chain[chain.length - 1]];
    expandedKeys.value = Array.from(new Set([...expandedKeys.value, ...chain.slice(0, -1)]));

    store.setLocalIndexSelectedKeys(selectedKeys.value);
    store.setLocalIndexExpandedKeys(expandedKeys.value);
    ensureRootNodesCollapsed();
}

function createTapeSuffix(entry: LocalTapeSummary): () => ReturnType<typeof h> {
    const ltoName = `LTO-${entry.generation}`;
    const style = getLtoFormatStyle(ltoName, entry.vendor);
    const totalSize = Number(entry.totalSizeBytes) || 0;
    const freeSize = Number(entry.freeSizeBytes) || 0;
    const usedPercent =
        totalSize > 0 ? Math.min(100, Math.max(0, ((totalSize - freeSize) / totalSize) * 100)) : 0;
    const gradient = `linear-gradient(90deg, #18a05855 0%, #18a05855 ${usedPercent}%, #fff5 ${usedPercent}%, #fff5 100%)`;

    return () =>
        h(
            NFlex,
            {
                style: { gap: '6px' },
            },
            {
                default: () => [
                    h(
                        NTag,
                        {
                            size: 'tiny',
                            style: {
                                paddingLeft: '4px',
                                paddingRight: '4px',
                                width: '80px',
                                display: 'flex',
                                fontSize: '10px',
                                justifyContent: 'right',
                                background: gradient,
                            },
                        },
                        {
                            default: () => `${formatFileSize(freeSize)} ${t('fileTree.free')}`,
                        },
                    ),
                    h(
                        NTag,
                        {
                            size: 'tiny',
                            color: { color: style.color },
                            style: {
                                width: '16px',
                                height: '16px',
                                borderRadius: '3px',
                            },
                        },
                        { default: () => ' ' },
                    ),
                ],
            },
        );
}

async function loadTapes(useCache = true) {
    if (useCache && store.localIndexTreeData.length > 0) {
        data.value = store.localIndexTreeData as LocalTreeNode[];
        selectedKeys.value = [...store.localIndexSelectedKeys];
        expandedKeys.value = [...store.localIndexExpandedKeys];
        updatePathIndex(data.value);
        syncSelectionFromStore();
        return;
    }

    isLoading.value = true;
    try {
        const tapes = (await localTapeApi.list()).data as Array<LocalTapeEntry>;
        data.value = (tapes || [])
            .map(entry => {
                const summary = typeof entry === 'string' ? null : entry;
                const tapeName = typeof entry === 'string' ? entry : (entry.tapeName ?? '');
                if (!tapeName) {
                    return null;
                }

                const path = '/';

                return {
                    label: tapeName,
                    key: makeNodeKey(tapeName, path),
                    tapeName,
                    tapeSummary: summary,
                    path,
                    isLeaf: false,
                    suffix: summary ? createTapeSuffix(summary) : undefined,
                } as LocalTreeNode;
            })
            .filter((node): node is LocalTreeNode => node !== null);

        updatePathIndex(data.value);
        store.setLocalIndexTreeData(data.value);
        ensureRootNodesCollapsed();
        syncSelectionFromStore();
    } catch (err) {
        console.error('Failed to load local tapes', err);
    } finally {
        isLoading.value = false;
    }
}

async function handleRefresh() {
    await loadTapes(false);
}

onMounted(async () => {
    await loadTapes();
});

function nodeProps({ option }: { option: TreeOption }) {
    return {
        async onClick() {
            const tapeName = String((option as any).tapeName || (option.label as string) || '');
            const path = normalizePath((option as any).path || '/');
            const key = (option as any).key as string | number;

            // When clicking an already-expanded tape root, treat it as a collapse intent
            // and avoid re-fetching root payload that can immediately rehydrate descendants.
            if (path === '/' && expandedKeys.value.includes(key)) {
                store.setCurrentLocation(tapeName, path);
                syncSelectionFromStore();
                return;
            }

            await getData(option, true);
        },
        onContextmenu(e: MouseEvent): void {
            optionsRef.value = [option];
            showDropdownRef.value = true;
            xRef.value = e.clientX;
            yRef.value = e.clientY;
            console.log(e.clientX, e.clientY);
            e.preventDefault();
        },
    };
}

async function handleLoad(node: TreeOption) {
    const path = normalizePath((node as any).path || '/');
    if (props.showTapeInfo && path === '/') {
        return;
    }
    await getData(node, false);
}

async function getData(node: TreeOption, fileOnly: boolean = false) {
    const selectionRequestId = fileOnly ? ++latestSelectionRequest : latestSelectionRequest;
    const treeNode = node as TreeOption & { children?: LocalTreeNode[] };
    const tapeName = String((node as any).tapeName || (node.label as string) || '');
    const currentPath = normalizePath((node as any).path || '/');
    const shouldClearFilesOnRootFailure = !props.showTapeInfo && currentPath === '/';

    // Persist clicked node context immediately so root selection can still drive UI mode
    // even when subsequent directory request fails (e.g. 404).
    store.setCurrentLocation(tapeName, currentPath);

    // In Tape Info mode, selecting a tape root should not probe LTFS filesystem endpoints.
    // This avoids 404-based no-LTFS state overriding the Tape Info panel.
    if (props.showTapeInfo && fileOnly && currentPath === '/') {
        syncSelectionFromStore();
        return;
    }

    const mapDirectoryChildren = (items: any[]) => {
        const parentPath = normalizePath((node as any).path || '/');
        return items.map((item: any) => {
            const childPath = parentPath === '/' ? `/${item.name}` : `${parentPath}/${item.name}`;
            return {
                label: item.name,
                key: makeNodeKey(tapeName, childPath),
                isLeaf: false,
                tapeName: tapeName,
                path: childPath,
                taskType: item.taskType,
            };
        });
    };

    try {
        const res = await requestTapePath(tapeName, currentPath);
        if (currentPath === '/') {
            store.setNoLtfsState(tapeName, false);
        }

        const isStaleSelection =
            fileOnly && selectionRequestId > 0 && selectionRequestId !== latestSelectionRequest;
        if (isStaleSelection) {
            return;
        }

        if (res && (res as any).data) {
            const d = (res as any).data;
            // split items into directories and files
            const dirs = (d.items || []).filter((item: any) => item.type !== 'file');
            const files = (d.items || [])
                .filter((item: any) => item.type === 'file')
                .map((item: any) => ({
                    ...item,
                    key: item.index ?? `file:${currentPath}:${item.name}`,
                    size: formatFileSize(Number(item.size) || 0),
                }));
            const directories = dirs.map((item: any) => ({
                ...item,
                key: item.index ?? `dir:${currentPath}:${item.name}`,
                // For folders, show child item count in the table size column.
                size: item.count ?? '-',
            }));

            // Keep both directories and files for the table view.
            try {
                store.setFiles([...directories, ...files]);
            } catch (e) {
                console.warn('Failed to store files', e);
            }

            if (fileOnly) {
                // Root selection can be file-only, but still hydrate tree nodes when
                // there are folders/tasks to show under an empty or previously failed root.
                if (currentPath === '/') {
                    const rootChildren = mapDirectoryChildren(dirs);
                    treeNode.children = rootChildren;
                    if ((treeNode as any).isLeaf && rootChildren.length > 0) {
                        (treeNode as any).isLeaf = false;
                    }
                    updatePathIndex(data.value);
                    syncSelectionFromStore();
                    ensureRootNodesCollapsed();
                    store.setLocalIndexTreeData(data.value);
                }
                return;
            }

            // only show directories in the tree; mark isLeaf always false
            treeNode.children = mapDirectoryChildren(dirs);

            updatePathIndex(data.value);
            syncSelectionFromStore();
            ensureRootNodesCollapsed();
            store.setLocalIndexTreeData(data.value);
        } else if (!fileOnly) {
            // Mark node as loaded even when API returned no payload to avoid repeated load retries.
            treeNode.children = [];
            store.setLocalIndexTreeData(data.value);
        }
    } catch (err) {
        const isStaleSelection =
            fileOnly && selectionRequestId > 0 && selectionRequestId !== latestSelectionRequest;
        if (!isStaleSelection) {
            if (shouldClearFilesOnRootFailure) {
                store.setFiles([]);
            }
            const status = (err as any)?.response?.status;
            const error = (err as any)?.response?.data?.error;
            const isNoLtfsFilesystemError =
                currentPath === '/' && status === 404 && error === 'No index files found for tape';

            if (isNoLtfsFilesystemError) {
                store.setNoLtfsState(tapeName, true);
                message.warning(t('messages.noLtfsFilesystem'));
            } else {
                message.error(
                    status
                        ? `Failed to load ${tapeName}:${currentPath} (HTTP ${status})`
                        : `Failed to load ${tapeName}:${currentPath}`,
                );
            }
        }
        if (!fileOnly) {
            // Mark node as loaded on failure (e.g. 404) so async tree does not keep requesting it.
            treeNode.children = [];
            store.setLocalIndexTreeData(data.value);
        }
        console.error('Failed to load tape files', err);
    }
}

watch(
    [() => store.currentTapeName, () => store.currentPath],
    () => {
        syncSelectionFromStore();
    },
    { immediate: true },
);

watch(
    () => props.showTapeInfo,
    enabled => {
        if (enabled) {
            ensureRootNodesCollapsed();
        }
    },
    { immediate: true },
);

watch(
    () => props.refreshToken,
    async () => {
        await loadTapes(false);
    },
);

function handleExpandedKeys(keys: Array<string | number>) {
    const previousExpanded = new Set(expandedKeys.value);
    const nextExpanded = new Set(keys);
    const collapsedRootKeys = getRootNodeKeys().filter(
        key => previousExpanded.has(key) && !nextExpanded.has(key),
    );

    const descendantKeySet = new Set<string | number>();
    if (collapsedRootKeys.length > 0) {
        const collectDescendantKeys = (nodes: LocalTreeNode[]) => {
            for (const node of nodes) {
                descendantKeySet.add(node.key);
                if (node.children && node.children.length > 0) {
                    collectDescendantKeys(node.children);
                }
            }
        };

        for (const rootKey of collapsedRootKeys) {
            const rootNode = data.value.find(node => node.key === rootKey);
            if (rootNode?.children && rootNode.children.length > 0) {
                collectDescendantKeys(rootNode.children);
            }
        }
    }

    const sanitizedKeys =
        descendantKeySet.size > 0 ? keys.filter(key => !descendantKeySet.has(key)) : keys;

    if (props.showTapeInfo) {
        const rootKeySet = new Set(getRootNodeKeys());
        const filtered = sanitizedKeys.filter(key => !rootKeySet.has(key));
        expandedKeys.value = filtered;
        store.setLocalIndexExpandedKeys(filtered);
        return;
    }

    expandedKeys.value = sanitizedKeys;
    store.setLocalIndexExpandedKeys(sanitizedKeys);
}

function renderLabel({ option }: { option: TreeOption }) {
    const path = normalizePath(((option as any).path as string) || '');
    if (path === '/') {
        return h('span', { class: 'local-tree-root-label' }, String(option.label ?? ''));
    }
    
    const taskType = String((option as any).taskType || '').toLowerCase();
    if (!taskType) {
        return option.label as string;
    }

    return h(
        NFlex,
        { align: 'center', style: { gap: '6px', minWidth: 0 } },
        {
            default: () => [
                h(
                    NTag,
                    {
                        size: 'tiny',
                        type: taskType === 'delete' ? 'warning' : 'success',
                    },
                    { default: () => getShortTaskType(taskType) },
                ),
                h('span', String(option.label ?? '')),
            ],
        },
    );
}

function getShortTaskType(taskType: string): string {
    const lower = taskType.toLowerCase();
    if (lower === 'delete') {
        return 'D';
    }
    if (lower === 'add') {
        return 'A';
    }
    return taskType;
}
</script>

<template>
    <div>
        <div
            style="
                display: flex;
                flex-direction: column;
                position: fixed;
                background: var(--n-color);
                z-index: 2;
                padding-top: 3px;
                min-width: 242px;
                gap: 4px;
            "
        >
            <n-flex
                align="center"
                :wrap="false"
                style="
                    padding-bottom: 4px;
                    border-bottom: 1px solid var(--n-border-color);
                    flex-shrink: 0;
                    gap: 0px 10px;
                "
            >
                <n-button
                    size="tiny"
                    tertiary
                    type="success"
                    :loading="isLoading"
                    style="padding: 0 4px; font-size: 14px"
                    @click="handleRefresh"
                    >↻</n-button
                >
                <n-text depth="3" style="font-size: 13px; white-space: nowrap">
                    {{ tapeCount }} Tapes
                </n-text>
                <n-tag
                    v-if="totalSpace > 0"
                    size="tiny"
                    :style="{
                        whiteSpace: 'nowrap',
                        flex: '1',
                        textAlign: 'right',
                        height: '21px',
                        fontSize: '10px',
                        background: totalGradient,
                    }"
                >
                    {{ formatFileSize(totalSpace) }} | {{ formatFileSize(totalFreeSpace) }}
                    {{ t('fileTree.free') }}
                </n-tag>
            </n-flex>
            <n-flex
                align="center"
                :wrap="false"
                style="
                    padding-bottom: 4px;
                    border-bottom: 1px solid var(--n-border-color);
                    flex-shrink: 0;
                    gap: 0px 10px;
                "
            >
                <n-input
                    v-model:value="filterText"
                    size="tiny"
                    :placeholder="'barcode'"
                    clearable
                />
                <n-select
                    v-model:value="filterGeneration"
                    :options="generationOptions"
                    size="tiny"
                    clearable
                    placeholder="L"
                    style="width: 65px; flex-shrink: 0"
                />
            </n-flex>
        </div>
        <n-tree
            class="local-tree"
            block-line
            expand-on-click
            :show-line="true"
            :indent="12"
            :data="visibleData"
            :render-label="renderLabel"
            :selected-keys="selectedKeys"
            :expanded-keys="expandedKeys"
            :on-load="handleLoad"
            :selectable="true"
            :node-props="nodeProps"
            @update:expanded-keys="handleExpandedKeys"
        />
    </div>
</template>

<style>
.local-tree {
    min-width: 0;
    position: relative;
    top: 64px;
}

.local-tree .n-scrollbar-container {
    overflow-x: hidden !important;
}

.local-tree .n-tree-node-content {
    white-space: nowrap;
    width: 100%;
    min-width: 0;
}

.local-tree .n-tree-node-content__text {
    overflow: hidden;
    text-overflow: ellipsis;
}

.local-tree-root-label {
    font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, 'Liberation Mono', monospace;
}
</style>
