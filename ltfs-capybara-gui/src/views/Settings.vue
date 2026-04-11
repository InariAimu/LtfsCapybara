<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue';
import { NButton, NCard, NForm, NFormItem, NInput, NSelect, useMessage } from 'naive-ui';
import { useI18n } from 'vue-i18n';
import { serverSettingsApi, type ServerSettingsUpdateRequest } from '@/api/modules/serversettings';
import { setAppLocale, type AppLocale } from '@/i18n';
import { setAppThemeMode, type AppThemeMode, useAppTheme } from '@/theme';

const { t, locale } = useI18n();
const { appThemeMode } = useAppTheme();
const message = useMessage();

const isServerSettingsLoading = ref(false);
const isServerSettingsSaving = ref(false);

const serverSettings = reactive<ServerSettingsUpdateRequest>({
    indexOnDataPartitionId: 1,
    indexOnIndexPartitionId: 1,
    dataPath: '',
});

const localeOptions = computed(() => [
    { label: t('locale.enUS'), value: 'en-US' },
    { label: t('locale.zhCN'), value: 'zh-CN' },
]);

const themeModeOptions = computed(() => [
    { label: t('theme.auto'), value: 'auto' },
    { label: t('theme.light'), value: 'light' },
    { label: t('theme.dark'), value: 'dark' },
]);

const currentLocale = computed<AppLocale>({
    get: () => locale.value as AppLocale,
    set: value => setAppLocale(value),
});

const currentThemeMode = computed<AppThemeMode>({
    get: () => appThemeMode.value,
    set: value => setAppThemeMode(value),
});

const indexOnDataPartitionOptions = [
    { label: 'SNIA Ltfs 2.4.0', value: 1 },
    { label: 'SNIA Ltfs 2.5.1', value: 2 },
];

const indexOnIndexPartitionOptions = [
    { label: 'SNIA Ltfs', value: 1 },
    { label: 'lcg', value: 2 },
];

async function loadServerSettings() {
    isServerSettingsLoading.value = true;
    const loaded = await serverSettingsApi.get();
    isServerSettingsLoading.value = false;

    if (!loaded) {
        message.error(t('settings.serverSettingsLoadFailed'));
        return;
    }

    serverSettings.indexOnDataPartitionId = loaded.indexOnDataPartitionId;
    serverSettings.indexOnIndexPartitionId = loaded.indexOnIndexPartitionId;
    serverSettings.dataPath = loaded.dataPath ?? '';
}

async function saveServerSettings() {
    isServerSettingsSaving.value = true;
    const saved = await serverSettingsApi.save({
        indexOnDataPartitionId: serverSettings.indexOnDataPartitionId,
        indexOnIndexPartitionId: serverSettings.indexOnIndexPartitionId,
        dataPath: serverSettings.dataPath,
    });
    isServerSettingsSaving.value = false;

    if (!saved) {
        message.error(t('settings.serverSettingsSaveFailed'));
        return;
    }

    serverSettings.indexOnDataPartitionId = saved.indexOnDataPartitionId;
    serverSettings.indexOnIndexPartitionId = saved.indexOnIndexPartitionId;
    serverSettings.dataPath = saved.dataPath ?? '';

    message.success(t('settings.serverSettingsSaveSuccess'));
}

onMounted(() => {
    void loadServerSettings();
});
</script>

<template>
    <div class="settings-page">
        <n-card :title="t('settings.appearance')" size="small">
            <n-form label-placement="left" label-width="110">
                <n-form-item :label="t('app.language')">
                    <n-select v-model:value="currentLocale" :options="localeOptions" />
                </n-form-item>
                <n-form-item :label="t('settings.themeMode')">
                    <n-select v-model:value="currentThemeMode" :options="themeModeOptions" />
                </n-form-item>
            </n-form>
        </n-card>

        <n-card :title="t('settings.serverSettings')" size="small">
            <n-form label-placement="left" label-width="180">

                <n-form-item :label="t('settings.indexOnIndexPartition')">
                    <n-select
                        v-model:value="serverSettings.indexOnIndexPartitionId"
                        :options="indexOnIndexPartitionOptions"
                        :loading="isServerSettingsLoading"
                    />
                </n-form-item>
                
                <n-form-item :label="t('settings.indexOnDataPartition')">
                    <n-select
                        v-model:value="serverSettings.indexOnDataPartitionId"
                        :options="indexOnDataPartitionOptions"
                        :loading="isServerSettingsLoading"
                    />
                </n-form-item>

                <n-form-item :label="t('settings.dataPath')">
                    <n-input
                        v-model:value="serverSettings.dataPath"
                        :placeholder="t('settings.dataPathPlaceholder')"
                    />
                </n-form-item>

                <n-form-item>
                    <n-button
                        type="primary"
                        :loading="isServerSettingsSaving"
                        @click="saveServerSettings"
                    >
                        {{ t('settings.saveServerSettings') }}
                    </n-button>
                </n-form-item>
            </n-form>
        </n-card>
    </div>
</template>

<style scoped>
.settings-page {
    padding: 10px;
    display: grid;
    gap: 10px;
}
</style>
