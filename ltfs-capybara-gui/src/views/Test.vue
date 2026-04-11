<script setup lang="ts">
import { onMounted, ref } from 'vue';
import { NButton, NCard, NEmpty, NSpin, useMessage } from 'naive-ui';
import { useI18n } from 'vue-i18n';
import { testApi } from '@/api/modules/test';
import type { StructMetadataDocument } from '@/api/types/structMetadata';
import StructMetadataTable from '@/components/StructMetadataTable.vue';

const { t } = useI18n();
const message = useMessage();

const isLoading = ref(false);
const payload = ref<StructMetadataDocument | null>(null);

async function loadStructMetadata() {
    isLoading.value = true;
    const result = await testApi.getStructMetadata();
    isLoading.value = false;

    if (!result) {
        message.error(t('test.loadFailed'));
        return;
    }

    payload.value = result;
}

onMounted(() => {
    void loadStructMetadata();
});
</script>

<template>
    <div class="test-page">
        <n-card :title="t('test.title')" size="small">
            <template #header-extra>
                <n-button size="small" @click="loadStructMetadata">
                    {{ t('test.reload') }}
                </n-button>
            </template>

            <p class="test-description">{{ t('test.description') }}</p>

            <n-spin :show="isLoading">
                <struct-metadata-table
                    v-if="payload"
                    :payload="payload"
                    :title="t('test.tableTitle')"
                />
                <n-empty v-else :description="t('test.empty')" />
            </n-spin>
        </n-card>
    </div>
</template>

<style scoped>
.test-page {
    padding: 10px;
}

.test-description {
    margin: 0 0 12px;
    color: rgba(0, 0, 0, 0.65);
}
</style>
