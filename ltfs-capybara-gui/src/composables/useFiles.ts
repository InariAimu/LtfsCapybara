import { fileApi } from '@/api/modules/files';
import { useFileStore } from '@/stores/fileStore';

export function useFiles() {
    const store = useFileStore();

    async function fetchFiles(path: string) {
        store.setLoading(true);
        try {
            const response = await fileApi.list(path);
            store.setFiles(response.data);
            store.currentPath = path;
        } finally {
            store.setLoading(false);
        }
    }

    async function deleteFile(path: string) {
        await fileApi.delete(path);
        await fetchFiles(store.currentPath);
    }

    return {
        fetchFiles,
        deleteFile,
    };
}
