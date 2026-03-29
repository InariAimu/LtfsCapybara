<script setup lang="ts">
import { computed, h, ref, watch } from 'vue';
import { NAlert, NButton, NFlex, NInput, NModal, NSpin, NTag, NTree, TreeOption } from 'naive-ui';
import { useI18n } from 'vue-i18n';
import { FsTreeNodeDto, localFileSystemApi } from '@/api/modules/localfilesystem';

interface FsTreeNode extends TreeOption {
    key: string;
    label: string;
    path: string;
    kind: 'drive' | 'network' | 'dir';
    available: boolean;
    isLeaf: boolean;
    children?: FsTreeNode[];
    error?: string | null;
}

const props = withDefaults(
    defineProps<{
        show: boolean;
        title?: string;
        initialPath?: string;
    }>(),
    {
        title: '',
        initialPath: '',
    },
);

const emit = defineEmits<{
    (e: 'update:show', value: boolean): void;
    (e: 'confirm', path: string): void;
    (e: 'cancel'): void;
}>();

const { t } = useI18n();
const loading = ref(false);
const loadingChildren = ref(false);
const loadError = ref<string>('');
const treeData = ref<FsTreeNode[]>([]);
const expandedKeys = ref<Array<string | number>>([]);
const selectedKeys = ref<Array<string | number>>([]);
const pathInputValue = ref<string>('');
const selectedPath = ref<string>('');
const pathEditorActive = ref(false);

const modalTitle = computed(() => props.title || t('directoryChooser.title'));
const canConfirm = computed(() => selectedPath.value.length > 0);

function mapToNode(item: FsTreeNodeDto): FsTreeNode {
    const isLeaf = !item.hasChildren;
    return {
        key: item.id,
        label: item.name,
        path: item.path,
        kind: item.kind,
        available: item.available,
        isLeaf,
        error: item.error,
        children: isLeaf ? [] : undefined,
    };
}

function createPathNode(path: string, children: FsTreeNode[]): FsTreeNode {
    const isNetworkPath = path.startsWith('\\\\');
    const isDriveRoot = /^[A-Za-z]:\\?$/.test(path);

    return {
        key: `manual:${path}`,
        label: path,
        path,
        kind: isNetworkPath ? 'network' : isDriveRoot ? 'drive' : 'dir',
        available: true,
        isLeaf: children.length === 0,
        children,
        error: null,
    };
}

async function loadRoots() {
    loading.value = true;
    loadError.value = '';

    try {
        const roots = await localFileSystemApi.getRoots();
        if (!roots) {
            loadError.value = t('directoryChooser.errors.loadRootsFailed');
            treeData.value = [];
            return;
        }

        treeData.value = roots.items.map(mapToNode);

        if (props.initialPath) {
            pathInputValue.value = props.initialPath;
            selectedPath.value = props.initialPath;
        }
    } finally {
        loading.value = false;
    }
}

async function handleLoad(node: TreeOption) {
    const target = node as FsTreeNode;
    if (!target.path || target.isLeaf) {
        return;
    }

    loadingChildren.value = true;
    loadError.value = '';
    try {
        const response = await localFileSystemApi.getChildren(target.path);
        if (!response) {
            target.children = [];
            loadError.value = t('directoryChooser.errors.loadChildrenFailed', {
                path: target.path,
            });
            return;
        }

        target.children = response.items.map(mapToNode);
        if (response.warning) {
            loadError.value = response.warning;
        }
    } finally {
        loadingChildren.value = false;
    }
}

async function handleGoToPath() {
    const targetPath = pathInputValue.value.trim();

    if (!targetPath) {
        selectedPath.value = '';
        selectedKeys.value = [];
        expandedKeys.value = [];
        await loadRoots();
        return;
    }

    loadingChildren.value = true;
    loadError.value = '';

    try {
        const response = await localFileSystemApi.getChildren(targetPath);
        if (!response) {
            selectedPath.value = '';
            selectedKeys.value = [];
            expandedKeys.value = [];
            loadError.value = t('directoryChooser.errors.loadChildrenFailed', {
                path: targetPath,
            });
            return;
        }

        const resolvedPath = response.parentPath || targetPath;
        const children = response.items.map(mapToNode);
        const rootNode = createPathNode(resolvedPath, children);

        treeData.value = [rootNode];
        selectedKeys.value = [rootNode.key];
        expandedKeys.value = children.length > 0 ? [rootNode.key] : [];
        selectedPath.value = resolvedPath;
        pathInputValue.value = resolvedPath;
        loadError.value = response.warning || '';
    } finally {
        loadingChildren.value = false;
    }
}

function onUpdateSelectedKeys(
    keys: Array<string | number>,
    options: Array<TreeOption | null>,
    meta: { action: 'select' | 'unselect'; node: TreeOption | null },
) {
    selectedKeys.value = keys;

    const selectedNode = (meta.node || options.find(x => x !== null)) as FsTreeNode | null;
    if (!selectedNode || !selectedNode.available) {
        selectedPath.value = '';
        return;
    }

    selectedPath.value = selectedNode.path;
    pathInputValue.value = selectedNode.path;
}

function onUpdateExpandedKeys(keys: Array<string | number>) {
    expandedKeys.value = keys;
}

function nodeProps({ option }: { option: TreeOption }) {
    return {
        onClick() {
            const node = option as FsTreeNode;
            if (!node.available) {
                return;
            }
            selectedKeys.value = [node.key];
            selectedPath.value = node.path;
            pathInputValue.value = node.path;
        },
    };
}

function onPathEditorFocusIn() {
    pathEditorActive.value = true;
}

function onPathEditorFocusOut(event: FocusEvent) {
    const currentTarget = event.currentTarget as HTMLElement | null;
    const nextTarget = event.relatedTarget as Node | null;

    if (currentTarget && nextTarget && currentTarget.contains(nextTarget)) {
        return;
    }

    pathEditorActive.value = false;
}

function renderPrefix({ option }: { option: TreeOption }) {
    const node = option as FsTreeNode;
    const type = node.kind === 'network' ? 'warning' : node.kind === 'drive' ? 'info' : 'default';
    const label =
        node.kind === 'network'
            ? t('directoryChooser.labels.network')
            : node.kind === 'drive'
              ? t('directoryChooser.labels.drive')
              : t('directoryChooser.labels.folder');

    return h(
        NTag,
        {
            size: 'tiny',
            type,
            bordered: false,
            style: {
                marginRight: '4px',
            },
        },
        {
            default: () => label,
        },
    );
}

function closeDialog() {
    emit('update:show', false);
}

function handleCancel() {
    emit('cancel');
    closeDialog();
}

function handleConfirm() {
    if (!selectedPath.value) {
        return;
    }

    emit('confirm', selectedPath.value);
    closeDialog();
}

watch(
    () => props.show,
    async visible => {
        if (!visible) {
            return;
        }

        selectedKeys.value = [];
        expandedKeys.value = [];
        pathEditorActive.value = false;
        pathInputValue.value = props.initialPath || '';
        selectedPath.value = props.initialPath || '';
        await loadRoots();
    },
    { immediate: false },
);
</script>

<template>
    <n-modal
        :show="show"
        preset="card"
        :title="modalTitle"
        style="width: min(860px, 95vw)"
        :mask-closable="false"
        @update:show="value => emit('update:show', value)"
    >
        <n-flex vertical :size="12">
            <n-alert v-if="loadError" type="warning" :show-icon="true">
                {{ loadError }}
            </n-alert>

            <n-spin :show="loading || loadingChildren">
                <div class="directory-tree-wrap">
                    <n-tree
                        block-line
                        expand-on-click
                        :show-line="true"
                        :indent="12"
                        :data="treeData"
                        :selected-keys="selectedKeys"
                        :expanded-keys="expandedKeys"
                        :on-load="handleLoad"
                        :node-props="nodeProps"
                        :render-prefix="renderPrefix"
                        @update:selected-keys="onUpdateSelectedKeys"
                        @update:expanded-keys="onUpdateExpandedKeys"
                    />
                </div>
            </n-spin>

            <n-flex justify="space-between" align="center">
                <div
                    class="selected-path-editor"
                    @focusin="onPathEditorFocusIn"
                    @focusout="onPathEditorFocusOut"
                >
                    <n-input
                        v-model:value="pathInputValue"
                        :placeholder="t('directoryChooser.pathPlaceholder')"
                        clearable
                        @keyup.enter="handleGoToPath"
                    />
                    <n-button
                        v-if="pathEditorActive"
                        type="primary"
                        secondary
                        @click="handleGoToPath"
                    >
                        {{ t('directoryChooser.go') }}
                    </n-button>
                </div>
                <n-flex :size="8">
                    <n-button @click="handleCancel">{{ t('directoryChooser.cancel') }}</n-button>
                    <n-button type="primary" :disabled="!canConfirm" @click="handleConfirm">
                        {{ t('directoryChooser.confirm') }}
                    </n-button>
                </n-flex>
            </n-flex>
        </n-flex>
    </n-modal>
</template>

<style scoped>
.directory-tree-wrap {
    border: 1px solid var(--n-border-color);
    border-radius: 6px;
    padding: 8px;
    min-height: 320px;
    max-height: 420px;
    overflow: auto;
}

.selected-path-editor {
    display: flex;
    align-items: center;
    gap: 8px;
    flex: 1;
    min-width: 0;
}
</style>
