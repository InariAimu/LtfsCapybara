import { apiClient } from '../client';

export type TapeFsTaskType = 'write' | 'replace' | 'delete' | 'read' | 'format' | 'folder';
export type FolderTaskType = 'add' | 'delete';

export type TapeFsTaskOperation = 'add' | 'rename' | 'update' | 'delete';

export interface TapeFsWriteTaskPayload {
    taskType?: 'Write' | 'Replace' | 'Delete';
    localPath: string;
    targetPath: string;
}

export interface AddServerFolderTaskPayload {
    localPath: string;
    targetPath: string;
}

export interface TapeFsReadTaskPayload {
    sourcePath: string;
    targetPath: string;
}

export interface TapeFsFormatParam {
    barcode: string;
    volumeName: string;
    extraPartitionCount: number;
    blockSize: number;
    immediateMode: boolean;
    capacity: number;
    p0Size: number;
    p1Size: number;
}

export interface TapeFsFolderTaskPayload {
    taskType: FolderTaskType;
    path: string;
}

export interface TapeFsPathTaskPayload {
    isDirectory: boolean;
    operation: TapeFsTaskOperation;
    path: string;
    newPath?: string;
    localPath?: string;
}

export interface TapeFsTaskItem {
    id: string;
    type: TapeFsTaskOperation | 'read' | 'format';
    tapeBarcode: string;
    pathTask?: TapeFsPathTaskPayload | null;
    readTask?: TapeFsReadTaskPayload | null;
    formatTask?: {
        formatParam: TapeFsFormatParam;
    } | null;
    createdAtTicks: number;
}

export interface TapeFsTaskGroup {
    tapeBarcode: string;
    name: string;
    tasks: TapeFsTaskItem[];
    updatedAtTicks: number;
}

export interface TapeFsTaskCreateRequest {
    type: TapeFsTaskOperation | 'read' | 'format';
    tapeBarcode?: string;
    pathTask?: TapeFsPathTaskPayload;
    readTask?: TapeFsReadTaskPayload;
    formatTask?: {
        formatParam: TapeFsFormatParam;
    };

    // Backward-compat payload fields.
    writeTask?: TapeFsWriteTaskPayload;
    folderTask?: TapeFsFolderTaskPayload;
}

export type LtfsTaskType = TapeFsTaskType;
export type LtfsWriteTaskPayload = TapeFsWriteTaskPayload;
export type LtfsReadTaskPayload = TapeFsReadTaskPayload;
export type LtfsFormatParam = TapeFsFormatParam;
export type LtfsFolderTaskPayload = TapeFsFolderTaskPayload;
export type LtfsPathTaskPayload = TapeFsPathTaskPayload;
export type LtfsTaskItem = TapeFsTaskItem;
export type LtfsTaskGroup = TapeFsTaskGroup;
export type LtfsTaskCreateRequest = TapeFsTaskCreateRequest;

export const taskApi = {
    listGroups() {
        return apiClient.get<TapeFsTaskGroup[]>('/tasks/groups');
    },

    getOrCreateGroup(tapeBarcode: string) {
        return apiClient.get<TapeFsTaskGroup>(`/tasks/groups/${encodeURIComponent(tapeBarcode)}`);
    },

    renameGroup(tapeBarcode: string, name: string) {
        return apiClient.post<TapeFsTaskGroup>(
            `/tasks/groups/${encodeURIComponent(tapeBarcode)}/rename`,
            {
                name,
            },
        );
    },

    addTask(tapeBarcode: string, request: TapeFsTaskCreateRequest) {
        return apiClient.post<TapeFsTaskGroup>(
            `/tasks/groups/${encodeURIComponent(tapeBarcode)}/tasks`,
            request,
        );
    },

    addFormatTask(tapeBarcode: string, formatParam?: TapeFsFormatParam) {
        return apiClient.post<TapeFsTaskGroup>(
            `/tasks/groups/${encodeURIComponent(tapeBarcode)}/tasks/format`,
            {
                formatTask: formatParam ? { formatParam } : undefined,
            },
        );
    },

    addFolderTask(tapeBarcode: string, folderTask: TapeFsFolderTaskPayload) {
        return apiClient.post<TapeFsTaskGroup>(
            `/tasks/groups/${encodeURIComponent(tapeBarcode)}/tasks`,
            {
                type: folderTask.taskType,
                pathTask: {
                    isDirectory: true,
                    operation: folderTask.taskType,
                    path: folderTask.path,
                },
            },
        );
    },

    addServerFolderTask(tapeBarcode: string, request: AddServerFolderTaskPayload) {
        return apiClient.post<TapeFsTaskGroup>(
            `/tasks/groups/${encodeURIComponent(tapeBarcode)}/tasks/server-folder`,
            request,
        );
    },

    deleteTask(tapeBarcode: string, taskId: string) {
        return apiClient.delete<TapeFsTaskGroup>(
            `/tasks/groups/${encodeURIComponent(tapeBarcode)}/tasks/${encodeURIComponent(taskId)}`,
        );
    },
};
