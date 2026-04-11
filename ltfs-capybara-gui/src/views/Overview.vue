<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { NCard, NEmpty, NSpace, NSpin, NTag, NThing, useMessage } from 'naive-ui';
import { useI18n } from 'vue-i18n';

import { overviewApi, type OverviewCountItem, type OverviewSnapshot } from '@/api/modules/overview';
import formatFileSize from '@/utils/formatFileSize';

const { t } = useI18n();
const message = useMessage();

const isLoading = ref(false);
const overview = ref<OverviewSnapshot | null>(null);

const driveStateOrder = ['loaded', 'empty', 'unknown'];
const taskStatusOrder = [
    'running',
    'waiting-for-confirmation',
    'pending',
    'completed',
    'failed',
    'cancelled',
];

const driveStateKeyMap: Record<string, string> = {
    loaded: 'loaded',
    empty: 'empty',
    unknown: 'unknown',
};

const taskStatusKeyMap: Record<string, string> = {
    running: 'running',
    'waiting-for-confirmation': 'waitingForConfirmation',
    pending: 'pending',
    completed: 'completed',
    failed: 'failed',
    cancelled: 'cancelled',
};

const driveStates = computed(() =>
    mapCounts(overview.value?.drives.stateCounts ?? [], driveStateOrder, key =>
        t(`overview.driveState.${driveStateKeyMap[key]}`),
    ),
);

const taskStatuses = computed(() =>
    mapCounts(overview.value?.tasks.executionStatusCounts ?? [], taskStatusOrder, key =>
        t(`overview.taskStatus.${taskStatusKeyMap[key]}`),
    ),
);

const generatedAt = computed(() => {
    if (!overview.value?.generatedAtTicks) {
        return '';
    }

    return new Date(
        (overview.value.generatedAtTicks - 621355968000000000) / 10000,
    ).toLocaleString();
});

function mapCounts(items: OverviewCountItem[], order: string[], getLabel: (key: string) => string) {
    return order.map(key => ({
        key,
        count: items.find(item => item.key === key)?.count ?? 0,
        label: getLabel(key),
    }));
}

function driveStateTagType(key: string) {
    switch (key) {
        case 'loaded':
            return 'success';
        case 'unknown':
            return 'warning';
        default:
            return 'default';
    }
}

function taskStatusTagType(key: string) {
    switch (key) {
        case 'running':
            return 'info';
        case 'waiting-for-confirmation':
            return 'warning';
        case 'completed':
            return 'success';
        case 'failed':
        case 'cancelled':
            return 'error';
        default:
            return 'default';
    }
}

async function loadOverview() {
    isLoading.value = true;
    try {
        const { data } = await overviewApi.get();
        overview.value = data;
    } catch (error) {
        console.error('Failed to load overview', error);
        message.error(t('overview.loadFailed'));
    } finally {
        isLoading.value = false;
    }
}

onMounted(() => {
    void loadOverview();
});
</script>

<template>
    <div class="overview-page">
        <n-card :title="t('menu.overview')" size="small">
            <n-spin :show="isLoading">
                <n-empty v-if="!overview" :description="t('overview.empty')" />
                <div v-else class="overview-grid">
                    <n-card size="small" embedded>
                        <n-thing>
                            <template #header>{{ t('overview.cards.drives.title') }}</template>
                            <template #description>
                                {{ t('overview.cards.drives.description', { time: generatedAt }) }}
                            </template>
                            <n-space vertical :size="12">
                                <div class="overview-value">{{ overview.drives.totalCount }}</div>
                                <div class="overview-metrics">
                                    <span>{{ t('overview.metrics.fake') }}</span>
                                    <strong>{{ overview.drives.fakeCount }}</strong>
                                    <span>{{ t('overview.metrics.mounted') }}</span>
                                    <strong>{{ overview.drives.loadedCount }}</strong>
                                    <span>{{ t('overview.metrics.ltfsReady') }}</span>
                                    <strong>{{ overview.drives.ltfsReadyCount }}</strong>
                                </div>
                                <n-space>
                                    <n-tag
                                        v-for="item in driveStates"
                                        :key="item.key"
                                        size="small"
                                        :type="driveStateTagType(item.key)"
                                    >
                                        {{ item.label }} {{ item.count }}
                                    </n-tag>
                                </n-space>
                            </n-space>
                        </n-thing>
                    </n-card>

                    <n-card size="small" embedded>
                        <n-thing>
                            <template #header>{{ t('overview.cards.tapes.title') }}</template>
                            <template #description>{{
                                t('overview.cards.tapes.description')
                            }}</template>
                            <n-space vertical :size="12">
                                <div class="overview-value">{{ overview.tapes.totalCount }}</div>
                                <div class="overview-metrics">
                                    <span>{{ t('overview.metrics.totalCapacity') }}</span>
                                    <strong>{{
                                        formatFileSize(overview.tapes.totalCapacityBytes)
                                    }}</strong>
                                    <span>{{ t('overview.metrics.freeCapacity') }}</span>
                                    <strong>{{
                                        formatFileSize(overview.tapes.freeCapacityBytes)
                                    }}</strong>
                                    <span>{{ t('overview.metrics.usedCapacity') }}</span>
                                    <strong>{{
                                        formatFileSize(overview.tapes.usedCapacityBytes)
                                    }}</strong>
                                </div>
                            </n-space>
                        </n-thing>
                    </n-card>

                    <n-card size="small" embedded>
                        <n-thing>
                            <template #header>{{ t('overview.cards.tasks.title') }}</template>
                            <template #description>{{
                                t('overview.cards.tasks.description')
                            }}</template>
                            <n-space vertical :size="12">
                                <div class="overview-value">
                                    {{ overview.tasks.queuedTaskCount }}
                                </div>
                                <div class="overview-metrics">
                                    <span>{{ t('overview.metrics.taskGroups') }}</span>
                                    <strong>{{ overview.tasks.groupCount }}</strong>
                                    <span>{{ t('overview.metrics.executions') }}</span>
                                    <strong>{{ overview.tasks.totalExecutionCount }}</strong>
                                    <span>{{ t('overview.metrics.activeExecutions') }}</span>
                                    <strong>{{ overview.tasks.activeExecutionCount }}</strong>
                                </div>
                                <n-space>
                                    <n-tag
                                        v-for="item in taskStatuses"
                                        :key="item.key"
                                        size="small"
                                        :type="taskStatusTagType(item.key)"
                                    >
                                        {{ item.label }} {{ item.count }}
                                    </n-tag>
                                </n-space>
                            </n-space>
                        </n-thing>
                    </n-card>
                </div>
            </n-spin>
        </n-card>
    </div>
</template>

<style scoped>
.overview-page {
    padding: 10px;
}

.overview-grid {
    display: grid;
    grid-template-columns: repeat(3, minmax(0, 1fr));
    gap: 12px;
}

.overview-value {
    font-size: 32px;
    font-weight: 700;
    line-height: 1;
}

.overview-metrics {
    display: grid;
    grid-template-columns: auto auto;
    gap: 6px 12px;
    align-items: center;
}

.overview-metrics strong {
    text-align: right;
}

@media (max-width: 960px) {
    .overview-grid {
        grid-template-columns: 1fr;
    }
}
</style>
