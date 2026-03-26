<script setup lang="ts">
import { onMounted, h, ref, watch } from 'vue';
import { NTree, NIcon, TreeOption, DropdownOption } from 'naive-ui';
import { useFileStore } from '@/stores/fileStore';
import { FileTraySharp } from '@vicons/ionicons5';
import { localTapeApi } from '@/api/modules/localtapes';
import formatFileSize from '@/utils/formatFileSize';

const data = ref<Array<Record<string, any>>>([]);
const store = useFileStore();
const optionsRef = ref<DropdownOption[]>([]);
const xRef = ref(0);
const yRef = ref(0);
const showDropdownRef = ref(false);
const selectedKeys = ref<Array<string | number>>([]);
const expandedKeys = ref<Array<string | number>>([]);

onMounted(async () => {
    // Reuse cached tree state when returning from other pages.
    if (store.localIndexTreeData.length > 0) {
        data.value = store.localIndexTreeData;
        selectedKeys.value = [...store.localIndexSelectedKeys];
        expandedKeys.value = [...store.localIndexExpandedKeys];
        return;
    }

    try {
        const names = (await localTapeApi.list()).data;
        console.log('Loaded local tapes:', names);
        data.value = (names || []).map((n: string, idx: number) => ({
            label: n,
            key: `${n}-${idx}`,
            tapeName: n,
            path: '/',
            isLeaf: false,
            prefix: () => h(NIcon, null, { default: () => h(FileTraySharp) }),
        }));
        store.setLocalIndexTreeData(data.value);
    } catch (err) {
        console.error('Failed to load local tapes', err);
    }
});

function nodeProps({ option }: { option: TreeOption }) {
    return {
        async onClick() {
            console.log('Node clicked:', option);
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
            node.children = dirs.map((item: any, idx: number) => {
                const parentPath = normalizePath((node as any).path || '/');
                const childPath =
                    parentPath === '/' ? `/${item.name}` : `${parentPath}/${item.name}`;
                return {
                    label: item.name,
                    key: `${node.key}-${idx}`,
                    isLeaf: false,
                    tapeName: tapeName,
                    path: childPath,
                };
            });
            store.setLocalIndexTreeData(data.value);
        }
    } catch (err) {
        console.error('Failed to load tape files', err);
    }
}

function normalizePath(path: string): string {
    const trimmed = (path || '/').trim().replace(/\\/g, '/');
    if (!trimmed || trimmed === '/') {
        return '/';
    }

    const compact = trimmed.replace(/\/{2,}/g, '/');
    return compact.startsWith('/') ? compact : `/${compact}`;
}

function findNodeKeyChain(
    nodes: Array<Record<string, any>>,
    tapeName: string,
    targetPath: string,
    chain: Array<string | number> = [],
): Array<string | number> | null {
    for (const node of nodes) {
        const nextChain = [...chain, node.key];
        const nodeTapeName = node.tapeName || node.label;
        const nodePath = normalizePath(node.path || '/');

        if (nodeTapeName === tapeName && nodePath === targetPath) {
            return nextChain;
        }

        if (node.children && node.children.length > 0) {
            const found = findNodeKeyChain(node.children, tapeName, targetPath, nextChain);
            if (found) {
                return found;
            }
        }
    }

    return null;
}

watch(
    [() => store.currentTapeName, () => store.currentPath, data],
    ([tapeName, path]) => {
        if (!tapeName) {
            selectedKeys.value = [];
            return;
        }

        const chain = findNodeKeyChain(data.value, tapeName, normalizePath(path || '/'));
        if (!chain || chain.length === 0) {
            return;
        }

        selectedKeys.value = [chain[chain.length - 1]];
        expandedKeys.value = Array.from(new Set([...expandedKeys.value, ...chain.slice(0, -1)]));

        store.setLocalIndexSelectedKeys(selectedKeys.value);
        store.setLocalIndexExpandedKeys(expandedKeys.value);
    },
    { immediate: true, deep: true },
);

function handleExpandedKeys(keys: Array<string | number>) {
    expandedKeys.value = keys;
    store.setLocalIndexExpandedKeys(keys);
}

watch(
    data,
    nodes => {
        store.setLocalIndexTreeData(nodes);
    },
    { deep: true },
);

watch(
    selectedKeys,
    keys => {
        store.setLocalIndexSelectedKeys(keys);
    },
    { deep: true },
);
</script>

<template>
    <n-tree
        block-line
        expand-on-click
        :show-line="true"
        :indent="12"
        :data="data"
        :selected-keys="selectedKeys"
        :expanded-keys="expandedKeys"
        :on-load="handleLoad"
        :selectable="true"
        :node-props="nodeProps"
        @update:expanded-keys="handleExpandedKeys"
    />
</template>

<style>
/* 禁止换行 */
.n-tree-node-content {
    white-space: nowrap;
    width: max-content;
}
</style>
