import { apiClient } from '../client';

export type TapeFsTaskType = 'write' | 'replace' | 'delete' | 'read' | 'verify' | 'format' | 'folder';
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
    isDirectoryMarker?: boolean;
}

export interface TapeFsVerifyTaskPayload {
    sourcePath: string;
    isDirectoryMarker?: boolean;
}

export interface TapeFsFormatParam {
    barcode: string;
    volumeName: string;
    mediaPool: string;
    extraPartitionCount: number;
    blockSize: number;
    immediateMode: boolean;
    capacity: number;
    p0Size: number;
    p1Size: number;
    encryptionKey: string | null;
}

export function createDefaultTapeFsFormatParam(
    barcode = '',
    volumeName = barcode,
    overrides?: Partial<TapeFsFormatParam>,
): TapeFsFormatParam {
    return {
        barcode,
        volumeName: volumeName || barcode,
        mediaPool: '',
        extraPartitionCount: 1,
        blockSize: 524288,
        immediateMode: true,
        capacity: 65535,
        p0Size: 1,
        p1Size: 65535,
        encryptionKey: null,
        ...overrides,
    };
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
    type: TapeFsTaskOperation | 'read' | 'verify' | 'format';
    tapeBarcode: string;
    pathTask?: TapeFsPathTaskPayload | null;
    readTask?: TapeFsReadTaskPayload | null;
    verifyTask?: TapeFsVerifyTaskPayload | null;
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

export interface TaskExecutionProgress {
    queueType: string;
    totalItems: number;
    completedItems: number;
    totalBytes: number;
    processedBytes: number;
    remainingBytes: number;
    currentItemPath: string | null;
    currentItemName: string | null;
    currentItemBytes: number;
    currentItemTotalBytes: number;
    currentItemPercentComplete: number;
    instantBytesPerSecond: number;
    averageBytesPerSecond: number;
    instantSpeedMBPerSecond: number;
    averageSpeedMBPerSecond: number;
    estimatedRemainingSeconds: number;
    percentComplete: number;
    statusMessage: string;
    isCompleted: boolean;
    timestampUtcTicks: number;
    tapePerformance: TaskExecutionTapePerformance | null;
    channelErrorRates: TaskExecutionChannelErrorRate[] | null;
    highestChannelErrorRate: TaskExecutionChannelErrorRate | null;
    speedHistory: TaskExecutionSpeedSample[];
    channelErrorRateHistory: TaskExecutionChannelErrorHistorySample[];
}

export interface TaskExecutionTapePerformance {
    repositionsPer100MB: number;
    dataRateIntoBufferMBPerSecond: number;
    maximumDataRateMBPerSecond: number;
    currentDataRateMBPerSecond: number;
    nativeDataRateMBPerSecond: number;
    compressionRatio: number;
}

export interface TaskExecutionChannelErrorRate {
    channelNumber: number;
    errorRateLog10: number | null;
    isNegativeInfinity: boolean;
    heatLevel: number;
    displayValue: string;
}

export interface TaskExecutionSpeedSample {
    timestampUtcTicks: number;
    speedMBPerSecond: number;
}

export interface TaskExecutionChannelErrorHistorySample {
    timestampUtcTicks: number;
    channelErrorRates: TaskExecutionChannelErrorRate[];
}

export interface TaskExecutionIncident {
    incidentId: string;
    executionId: string;
    source: string;
    severity: string;
    action: string;
    message: string;
    detail: string | null;
    requiresConfirmation: boolean;
    isResolved: boolean;
    resolution: string | null;
    createdAtTicks: number;
    resolvedAtTicks: number | null;
}

export interface TaskExecutionLogEntry {
    logId: string;
    executionId: string;
    tapeDriveId: string;
    level: string;
    message: string;
    createdAtTicks: number;
}

export interface TaskExecutionSnapshot {
    executionId: string;
    tapeBarcode: string;
    tapeDriveId: string;
    status: string;
    error: string | null;
    startedAtTicks: number;
    updatedAtTicks: number;
    completedAtTicks: number | null;
    scsiMetricsEnabled: boolean;
    progress: TaskExecutionProgress | null;
    pendingIncident: TaskExecutionIncident | null;
}

export interface TaskExecutionEventEnvelope {
    type: string;
    execution: TaskExecutionSnapshot | null;
    incident: TaskExecutionIncident | null;
    log: TaskExecutionLogEntry | null;
}

export interface TapeFsTaskCreateRequest {
    type: TapeFsTaskOperation | 'read' | 'verify' | 'format';
    tapeBarcode?: string;
    pathTask?: TapeFsPathTaskPayload;
    readTask?: TapeFsReadTaskPayload;
    verifyTask?: TapeFsVerifyTaskPayload;
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
export type LtfsVerifyTaskPayload = TapeFsVerifyTaskPayload;
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

    addReadTask(tapeBarcode: string, readTask: TapeFsReadTaskPayload) {
        return apiClient.post<TapeFsTaskGroup>(
            `/tasks/groups/${encodeURIComponent(tapeBarcode)}/tasks`,
            {
                type: 'read',
                readTask,
            },
        );
    },

    addVerifyTask(tapeBarcode: string, verifyTask: TapeFsVerifyTaskPayload) {
        return apiClient.post<TapeFsTaskGroup>(
            `/tasks/groups/${encodeURIComponent(tapeBarcode)}/tasks`,
            {
                type: 'verify',
                verifyTask,
            },
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

    listExecutions() {
        return apiClient.get<TaskExecutionSnapshot[]>('/tasks/executions');
    },

    getExecution(executionId: string) {
        return apiClient.get<TaskExecutionSnapshot>(
            `/tasks/executions/${encodeURIComponent(executionId)}`,
        );
    },

    executeGroup(tapeBarcode: string, tapeDriveId: string, scsiMetricsEnabled = true) {
        return apiClient.post<TaskExecutionSnapshot>(
            `/tasks/groups/${encodeURIComponent(tapeBarcode)}/execute`,
            { tapeDriveId, scsiMetricsEnabled },
            { timeout: 0 },
        );
    },

    updateScsiMetrics(executionId: string, enabled: boolean) {
        return apiClient.put<TaskExecutionSnapshot>(
            `/tasks/executions/${encodeURIComponent(executionId)}/metrics`,
            { enabled },
        );
    },

    resolveIncident(executionId: string, incidentId: string, resolution: 'continue' | 'abort') {
        return apiClient.post<TaskExecutionIncident>(
            `/tasks/executions/${encodeURIComponent(executionId)}/incidents/${encodeURIComponent(incidentId)}/resolve`,
            { resolution },
        );
    },
};
