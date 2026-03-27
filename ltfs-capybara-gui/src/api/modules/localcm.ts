import type { AxiosResponse } from 'axios';
import type { TapeInfo } from '@/api/types/tapeInfo';
import { apiClient } from '../client';

export const localCmApi = {
    async get(tapeName: string): Promise<AxiosResponse<TapeInfo> | null> {
        try {
            const res = await apiClient.get<TapeInfo>(`/localcm/${encodeURIComponent(tapeName)}`);
            return res;
        } catch (err) {
            console.error('localCmApi.get error', err);
            return null;
        }
    },
};
