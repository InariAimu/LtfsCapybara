import { apiClient } from '../client';

export const localTapeApi = {
    list() {
        return apiClient.get('/localtapes');
    },
    async getRoot(tapeName: string): Promise<any> {
        try {
            const res = await apiClient.get(`/local/${encodeURIComponent(tapeName)}`);
            return res as unknown;
        } catch (err) {
            console.error('localTapeApi.getRoot error', err);
            return null;
        }
    },

    async getPath(tapeName: string, path: string): Promise<any> {
        try {
            // Use encodeURI so path separators ('/') are preserved (not encoded to %2F)
            const encodedPath = encodeURI(path);
            const res = await apiClient.get(
                `/local/${encodeURIComponent(tapeName)}/${encodedPath}`,
            );
            return res as unknown;
        } catch (err) {
            console.error('localTapeApi.getPath error', err);
            return null;
        }
    },
};
