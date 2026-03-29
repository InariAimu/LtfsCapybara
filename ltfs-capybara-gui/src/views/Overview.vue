<script setup lang="ts">
import { NCard, NEmpty } from 'naive-ui';
import { useI18n } from 'vue-i18n';

const { t } = useI18n();
</script>

<template>
    <div class="placeholder-page">
        <n-card :title="t('menu.overview')" size="small">
            <n-empty :description="t('pages.comingSoon')" />
        </n-card>
    </div>
</template>

<style scoped>
.placeholder-page {
    padding: 10px;
}
</style>
