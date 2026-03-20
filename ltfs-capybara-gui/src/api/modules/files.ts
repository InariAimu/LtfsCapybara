import { apiClient } from '../client';

export const fileApi = {
    list(path: string) {
        return apiClient.get('/files', { params: { path } });
    },

    delete(path: string) {
        return apiClient.delete('/files', { data: { path } });
    },

    upload(file: File) {
        const form = new FormData();
        form.append('file', file);
        return apiClient.post('/upload', form);
    },
};
