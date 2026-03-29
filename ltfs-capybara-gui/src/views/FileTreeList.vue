<script setup lang="ts">
import { onMounted, h, ref, watch, computed } from 'vue';
import { NTree, TreeOption, DropdownOption, NTag, NFlex, NButton, NText, NSelect } from 'naive-ui';
import { useI18n } from 'vue-i18n';
import { useFileStore } from '@/stores/fileStore';
import { localTapeApi } from '@/api/modules/localtapes';
import formatFileSize from '@/utils/formatFileSize';
import { getLtoFormatStyle } from '@/utils/tapeFormatStyle';

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
    isLeaf: boolean;
    children?: LocalTreeNode[];
    tapeSummary?: LocalTapeSummary | null;
    suffix?: () => ReturnType<typeof h>;
}

const data = ref<LocalTreeNode[]>([]);
const store = useFileStore();
const optionsRef = ref<DropdownOption[]>([]);
const xRef = ref(0);
const yRef = ref(0);
const showDropdownRef = ref(false);
const selectedKeys = ref<Array<string | number>>([]);
const expandedKeys = ref<Array<string | number>>([]);
const nodeChainIndex = new Map<string, Array<string | number>>();
let latestSelectionRequest = 0;

const filterGeneration = ref<number | null>(null);
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

const visibleData = computed(() =>
    filterGeneration.value === null
        ? data.value
        : data.value.filter(n => n.tapeSummary?.generation === filterGeneration.value),
);

const tapeCount = computed(() => visibleData.value.length);

const totalFreeSpace = computed(() =>
    visibleData.value.reduce((sum, n) => sum + (Number(n.tapeSummary?.freeSizeBytes) || 0), 0),
);

const totalSpace = computed(() =>
    visibleData.value.reduce((sum, n) => sum + (Number(n.tapeSummary?.totalSizeBytes) || 0), 0),
);
const { t } = useI18n();

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
            //console.log('Node clicked:', option);
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
    await getData(node, false);
}

async function getData(node: TreeOption, fileOnly: boolean = false) {
    const selectionRequestId = fileOnly ? ++latestSelectionRequest : latestSelectionRequest;

    try {
        // If node has a `path`, request that path; otherwise request root for the tape
        const tapeName = (node as any).tapeName || (node.label as string);
        const currentPath = normalizePath((node as any).path || '/');
        let res: any = null;
        if (currentPath !== '/') {
            res = await localTapeApi.getPath(tapeName, currentPath);
        } else {
            // root-level node
            res = await localTapeApi.getRoot(tapeName);
        }

        store.setCurrentLocation(tapeName, currentPath);

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
                    key: item.index,
                    size: formatFileSize(Number(item.size) || 0),
                }));

            // put files into store
            try {
                store.setFiles(files);
            } catch (e) {
                console.warn('Failed to store files', e);
            }

            if (fileOnly) {
                // if only files are requested, do not update tree nodes
                return;
            }

            // only show directories in the tree; mark isLeaf always false
            node.children = dirs.map((item: any) => {
                const parentPath = normalizePath((node as any).path || '/');
                const childPath =
                    parentPath === '/' ? `/${item.name}` : `${parentPath}/${item.name}`;
                return {
                    label: item.name,
                    key: makeNodeKey(tapeName, childPath),
                    isLeaf: false,
                    tapeName: tapeName,
                    path: childPath,
                };
            });

            updatePathIndex(data.value);
            syncSelectionFromStore();
            store.setLocalIndexTreeData(data.value);
        }
    } catch (err) {
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

function handleExpandedKeys(keys: Array<string | number>) {
    expandedKeys.value = keys;
    store.setLocalIndexExpandedKeys(keys);
}

function renderLabel({ option }: { option: TreeOption }) {
    const path = normalizePath(((option as any).path as string) || '');
    if (path === '/') {
        return h('span', { class: 'local-tree-root-label' }, String(option.label ?? ''));
    }
    return option.label as string;
}
</script>

<template>
    <div style="display: flex; flex-direction: column; height: 100%">
        <n-flex
            align="center"
            :wrap="false"
            style="
                padding-bottom: 4px;
                border-bottom: 1px solid var(--n-border-color);
                flex-shrink: 0;
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
                {{ tapeCount }}
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
            <n-select
                v-model:value="filterGeneration"
                :options="generationOptions"
                size="tiny"
                clearable
                placeholder="L"
                style="width: 60px; flex-shrink: 0"
            />
        </n-flex>
        <n-tree
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
.n-tree-node-content {
    white-space: nowrap;
    width: max-content;
}

.local-tree-root-label {
    font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, 'Liberation Mono', monospace;
}
</style>
