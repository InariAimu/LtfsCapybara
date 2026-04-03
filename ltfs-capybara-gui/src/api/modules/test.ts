import { apiClient } from '../client';
import type { StructMetadataDocument } from '@/api/types/structMetadata';

export const testApi = {
    async getStructMetadata(): Promise<StructMetadataDocument | null> {
        try {
            const res = await apiClient.get<StructMetadataDocument>('/test/struct-metadata');
            return res.data;
        } catch (err) {
            console.error('testApi.getStructMetadata error', err);
            return null;
        }
    },
};