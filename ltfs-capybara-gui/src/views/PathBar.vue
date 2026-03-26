<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { NButton, NInput, NInputGroup, NIcon } from 'naive-ui';
import {
    ArrowUpOutline,
    RefreshOutline,
    FolderOpenOutline,
    SearchOutline,
} from '@vicons/ionicons5';
import { useI18n } from 'vue-i18n';

interface Props {
    tapeName: string;
    path: string;
}

const props = defineProps<Props>();

const emit = defineEmits<{
    navigate: [path: string];
    refresh: [];
}>();

const searchQuery = ref('');
const { t } = useI18n();

watch(
    () => props.path,
    () => {
        // Path auto-updates via computed segments
    },
    { immediate: true },
);

function handleSearch() {
    // TODO: Implement search functionality
    console.log('Search TODO:', searchQuery.value);
}

const segments = computed(() => {
    const normalized = normalizePath(props.path);
    if (normalized === '/') {
        return [] as string[];
    }
    return normalized.split('/').filter(Boolean);
});

const canGoUp = computed(() => normalizePath(props.path) !== '/');

function normalizePath(path: string): string {
    const trimmed = (path || '/').trim();
    if (!trimmed) {
        return '/';
    }

    const withSlashes = trimmed.replace(/\\/g, '/');
    const compact = withSlashes.replace(/\/{2,}/g, '/');
    return compact.startsWith('/') ? compact : `/${compact}`;
}

function navigateToIndex(index: number) {
    if (index < 0) {
        emit('navigate', '/');
        return;
    }

    const target = `/${segments.value.slice(0, index + 1).join('/')}`;
    emit('navigate', target);
}

function goUp() {
    if (!canGoUp.value) {
        return;
    }

    const next = segments.value.slice(0, -1);
    emit('navigate', next.length ? `/${next.join('/')}` : '/');
}
</script>

<template>
    <div class="path-bar">
        <n-input-group>
            <n-button secondary :size="'small'" :disabled="!canGoUp" @click="goUp">
                <template #icon>
                    <n-icon><arrow-up-outline /></n-icon>
                </template>
            </n-button>
            <n-button secondary :size="'small'" @click="emit('refresh')">
                <template #icon>
                    <n-icon><refresh-outline /></n-icon>
                </template>
            </n-button>
            <div class="path-buttons">
                <n-button text @click="navigateToIndex(-1)">
                    <n-icon><folder-open-outline /></n-icon>
                </n-button>
                <template v-for="(segment, index) in segments" :key="`${segment}-${index}`">
                    <span class="separator">></span>
                    <n-button text @click="navigateToIndex(index)" style="font-size: 12px">
                        {{ segment }}
                    </n-button>
                </template>
            </div>
            <n-input
                v-model:value="searchQuery"
                clearable
                :placeholder="t('pathBar.searchPlaceholder')"
                @keydown.enter.prevent="handleSearch"
                style="max-width: 150px; font-size: 12px"
                :size="'small'"
            />
            <n-button secondary :size="'small'" @click="handleSearch">
                <template #icon>
                    <n-icon><search-outline /></n-icon>
                </template>
            </n-button>
        </n-input-group>
    </div>
</template>

<style scoped>
.path-bar {
    padding: 8px 10px 6px;
    border-bottom: 1px solid var(--n-border-color);
}

.n-input-group {
    display: flex;
    gap: 6px;
    align-items: center;
}

.path-buttons {
    display: flex;
    align-items: center;
    gap: 2px;
    flex: 1;
    overflow: hidden hidden;
    white-space: nowrap;
    min-width: 0;
    padding: 0 4px;
}

.separator {
    font-size: 12px;
    opacity: 0.6;
    margin: 0 2px;
}
</style>
