// composables/useFileSSE.ts
import { useFileStore } from '@/stores/fileStore';

export function useFileSSE() {
    const store = useFileStore();

    const es = new EventSource('/api/events');

    es.onmessage = event => {
        const data = JSON.parse(event.data);

        if (data.type === 'file_update') {
            store.setFiles(data.files);
        }
    };

    return { es };
}
