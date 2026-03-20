<script setup lang="ts">
import { onMounted } from 'vue';
import { useFiles } from '@/composables/useFiles';
import { useFileStore } from '@/stores/fileStore';

//const { fetchFiles, deleteFile } = useFiles();
const store = useFileStore();

import { NButton, NTree, NIcon } from 'naive-ui';
import { FileTraySharp } from '@vicons/ionicons5';
import { h, ref } from 'vue';

const data = [
    {
        label: 'LTFS01L6',
        key: '0',
        prefix: () =>
            h(NIcon, null, {
                default: () => h(FileTraySharp),
            }),
        suffix: () =>
            h(NButton, { text: true, type: 'primary' }, { default: () => '2.2TB / 2.5TB' }),
        children: [
            {
                label: 'Directory1',
                key: '00',
                children: [
                    {
                        label: 'SubDirectory1',
                        key: '000',
                    },
                    {
                        label: 'SubDirectory2',
                        key: '001',
                    },
                ],
            },
            {
                label: 'Directory2',
                key: '01',
            },
        ],
    },
];
</script>

<template>
    <n-tree
        block-line
        :show-line="true"
        expand-on-click
        :indent="12"
        :data="data"
        :selectable="true"
    />
</template>
