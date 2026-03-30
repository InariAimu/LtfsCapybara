import { apiClient } from '../client';

export type LtfsTaskType = 'write' | 'replace' | 'delete' | 'read' | 'format';

export interface LtfsWriteTaskPayload {
    taskType: 'Write' | 'Replace' | 'Delete';
    localPath: string;
    targetPath: string;
}

export interface LtfsReadTaskPayload {
    sourcePath: string;
    targetPath: string;
}

export interface LtfsFormatParam {
    barcode: string;
    volumeName: string;
    extraPartitionCount: number;
    blockSize: number;
    immediateMode: boolean;
    capacity: number;
    p0Size: number;
    p1Size: number;
}

export interface LtfsTaskItem {
    id: string;
    type: LtfsTaskType;
    tapeBarcode: string;
    writeTask?: LtfsWriteTaskPayload | null;
    readTask?: LtfsReadTaskPayload | null;
    formatTask?: {
        formatParam: LtfsFormatParam;
    } | null;
    createdAtTicks: number;
}

export interface LtfsTaskGroup {
    tapeBarcode: string;
    name: string;
    tasks: LtfsTaskItem[];
    updatedAtTicks: number;
}

export interface LtfsTaskCreateRequest {
    type: LtfsTaskType;
    tapeBarcode?: string;
    writeTask?: LtfsWriteTaskPayload;
    readTask?: LtfsReadTaskPayload;
    formatTask?: {
        formatParam: LtfsFormatParam;
    };
}

export const taskApi = {
    listGroups() {
        return apiClient.get<LtfsTaskGroup[]>('/tasks/groups');
    },

    getOrCreateGroup(tapeBarcode: string) {
        return apiClient.get<LtfsTaskGroup>(`/tasks/groups/${encodeURIComponent(tapeBarcode)}`);
    },

    renameGroup(tapeBarcode: string, name: string) {
        return apiClient.post<LtfsTaskGroup>(`/tasks/groups/${encodeURIComponent(tapeBarcode)}/rename`, {
            name,
        });
    },

    addTask(tapeBarcode: string, request: LtfsTaskCreateRequest) {
        return apiClient.post<LtfsTaskGroup>(
            `/tasks/groups/${encodeURIComponent(tapeBarcode)}/tasks`,
            request,
        );
    },

    addFormatTask(tapeBarcode: string, formatParam?: LtfsFormatParam) {
        return apiClient.post<LtfsTaskGroup>(
            `/tasks/groups/${encodeURIComponent(tapeBarcode)}/tasks/format`,
            {
                formatTask: formatParam ? { formatParam } : undefined,
            },
        );
    },

    deleteTask(tapeBarcode: string, taskId: string) {
        return apiClient.delete<LtfsTaskGroup>(
            `/tasks/groups/${encodeURIComponent(tapeBarcode)}/tasks/${encodeURIComponent(taskId)}`,
        );
    },
};
