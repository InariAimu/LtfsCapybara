import { apiClient } from '../client';

export interface LocalIndexItem {
    type: 'file' | 'dir';
    name: string;
    task?: string;
    size?: number | string | null;
    index?: number | string | null;
    crc64?: string | null;
    modifyTime?: string | null;
}

export interface LocalIndexDirectory {
    name: string;
    items: LocalIndexItem[];
}

export const localTapeApi = {
    list() {
        return apiClient.get('/localtapes');
    },
    async getRoot(tapeName: string): Promise<{ data: LocalIndexDirectory }> {
        try {
            return await apiClient.get<LocalIndexDirectory>(
                `/local/${encodeURIComponent(tapeName)}`,
            );
        } catch (err) {
            console.error('localTapeApi.getRoot error', err);
            throw err;
        }
    },

    async getPath(tapeName: string, path: string): Promise<{ data: LocalIndexDirectory }> {
        try {
            // Use encodeURI so path separators ('/') are preserved (not encoded to %2F)
            const encodedPath = encodeURI(path);
            return await apiClient.get<LocalIndexDirectory>(
                `/local/${encodeURIComponent(tapeName)}/${encodedPath}`,
            );
        } catch (err) {
            console.error('localTapeApi.getPath error', err);
            throw err;
        }
    },
};
