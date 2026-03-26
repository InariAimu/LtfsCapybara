<script setup lang="ts">
import { computed } from 'vue';
import { NCard, NForm, NFormItem, NSelect } from 'naive-ui';
import { useI18n } from 'vue-i18n';
import { setAppLocale, type AppLocale } from '@/i18n';

const { t, locale } = useI18n();

const localeOptions = computed(() => [
    { label: t('locale.enUS'), value: 'en-US' },
    { label: t('locale.zhCN'), value: 'zh-CN' },
]);

const currentLocale = computed<AppLocale>({
    get: () => locale.value as AppLocale,
    set: value => setAppLocale(value),
});
</script>

<template>
    <div class="settings-page">
        <n-card :title="t('settings.title')" size="small">
            <n-form label-placement="left" label-width="110">
                <n-form-item :label="t('app.language')">
                    <n-select v-model:value="currentLocale" :options="localeOptions" />
                </n-form-item>
            </n-form>
        </n-card>
    </div>
</template>

<style scoped>
.settings-page {
    padding: 10px;
}
</style>
