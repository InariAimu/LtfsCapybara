import { apiClient } from '../client';

export interface ServerSettingsDto {
    indexOnDataPartitionId: number;
    indexOnIndexPartitionId: number;
    dataPath: string;
}

export interface ServerSettingsUpdateRequest {
    indexOnDataPartitionId: number;
    indexOnIndexPartitionId: number;
    dataPath: string;
}

export const serverSettingsApi = {
    async get(): Promise<ServerSettingsDto | null> {
        try {
            const res = await apiClient.get<ServerSettingsDto>('/settings/server');
            return res.data;
        } catch (err) {
            console.error('serverSettingsApi.get error', err);
            return null;
        }
    },

    async save(payload: ServerSettingsUpdateRequest): Promise<ServerSettingsDto | null> {
        try {
            const res = await apiClient.put<ServerSettingsDto>('/settings/server', payload);
            return res.data;
        } catch (err) {
            console.error('serverSettingsApi.save error', err);
            return null;
        }
    },
};
