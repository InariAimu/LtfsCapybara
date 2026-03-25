<script setup lang="ts">
import { onMounted, h, ref } from 'vue';
import { NButton, NTree, NIcon, TreeOption, DropdownOption } from 'naive-ui';
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

onMounted(async () => {
    try {
        const names = (await localTapeApi.list()).data;
        console.log('Loaded local tapes:', names);
        data.value = (names || []).map((n: string, idx: number) => ({
            label: n,
            key: `${n}-${idx}`,
            tapeName: n,
            isLeaf: false,
            prefix: () => h(NIcon, null, { default: () => h(FileTraySharp) }),
        }));
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
        let res: any = null;
        if ((node as any).path) {
            res = await localTapeApi.getPath(tapeName, (node as any).path);
        } else {
            // root-level node
            res = await localTapeApi.getRoot(tapeName);
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
            node.children = dirs.map((item: any, idx: number) => {
                const childPath = ((node as any).path ? (node as any).path + '/' : '') + item.name;
                return {
                    label: item.name,
                    key: `${node.key}-${idx}`,
                    isLeaf: false,
                    tapeName: tapeName,
                    path: childPath,
                };
            });
        }
    } catch (err) {
        console.error('Failed to load tape files', err);
    }
}
</script>

<template>
    <n-tree
        block-line
        expand-on-click
        :show-line="true"
        :indent="12"
        :data="data"
        :on-load="handleLoad"
        :selectable="true"
        :node-props="nodeProps"
    />
</template>

<style>
/* 禁止换行 */
.n-tree-node-content {
    white-space: nowrap;
    width: max-content;
}
</style>
