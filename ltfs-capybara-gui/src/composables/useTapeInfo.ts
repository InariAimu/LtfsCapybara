import { onBeforeUnmount, ref, watch } from 'vue';
import { localCmApi } from '@/api/modules/localcm';
import type { TapeInfo } from '@/api/types/tapeInfo';

export function useTapeInfo(getTapeName: () => string) {
    const tapeInfo = ref<TapeInfo | null>(null);
    const loading = ref(false);
    const error = ref<string | null>(null);
    const activeRequestId = ref(0);

    async function refresh() {
        const requestId = ++activeRequestId.value;
        const tapeName = getTapeName();

        if (!tapeName) {
            tapeInfo.value = null;
            error.value = null;
            loading.value = false;
            return;
        }

        loading.value = true;
        error.value = null;

        try {
            const response = await localCmApi.get(tapeName);
            if (requestId !== activeRequestId.value) {
                return;
            }

            tapeInfo.value = response?.data ?? null;
            if (!response?.data) {
                error.value = 'Failed to load tape information.';
            }
        } catch (err) {
            if (requestId !== activeRequestId.value) {
                return;
            }

            console.error('useTapeInfo.refresh error', err);
            tapeInfo.value = null;
            error.value = 'Failed to load tape information.';
        } finally {
            if (requestId === activeRequestId.value) {
                loading.value = false;
            }
        }
    }

    watch(
        getTapeName,
        () => {
            void refresh();
        },
        { immediate: true },
    );

    onBeforeUnmount(() => {
        activeRequestId.value += 1;
    });

    return {
        tapeInfo,
        loading,
        error,
        refresh,
    };
}
