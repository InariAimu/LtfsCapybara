import { apiClient } from '../client';

export interface FsTreeNodeDto {
    id: string;
    name: string;
    path: string;
    kind: 'drive' | 'network' | 'dir';
    available: boolean;
    hasChildren: boolean;
    error?: string | null;
}

export interface FsRootsResponse {
    items: FsTreeNodeDto[];
    loadedAtUtc: string;
}

export interface FsChildrenResponse {
    parentPath: string;
    items: FsTreeNodeDto[];
    warning?: string | null;
    loadedAtUtc: string;
}

export const localFileSystemApi = {
    async getRoots(): Promise<FsRootsResponse | null> {
        try {
            const res = await apiClient.get<FsRootsResponse>('/fsroots');
            return res.data;
        } catch (err) {
            console.error('localFileSystemApi.getRoots error', err);
            return null;
        }
    },

    async getChildren(path: string): Promise<FsChildrenResponse | null> {
        try {
            const res = await apiClient.get<FsChildrenResponse>('/fschildren', {
                params: { path },
            });
            return res.data;
        } catch (err) {
            console.error('localFileSystemApi.getChildren error', err);
            return null;
        }
    },
};
