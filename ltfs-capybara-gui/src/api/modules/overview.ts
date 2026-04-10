import { apiClient } from '../client';

export interface OverviewCountItem {
    key: string;
    count: number;
}

export interface OverviewDrives {
    totalCount: number;
    fakeCount: number;
    loadedCount: number;
    ltfsReadyCount: number;
    stateCounts: OverviewCountItem[];
}

export interface OverviewTapes {
    totalCount: number;
    totalCapacityBytes: number;
    freeCapacityBytes: number;
    usedCapacityBytes: number;
}

export interface OverviewTasks {
    groupCount: number;
    queuedTaskCount: number;
    totalExecutionCount: number;
    activeExecutionCount: number;
    executionStatusCounts: OverviewCountItem[];
}

export interface OverviewSnapshot {
    generatedAtTicks: number;
    drives: OverviewDrives;
    tapes: OverviewTapes;
    tasks: OverviewTasks;
}

export const overviewApi = {
    get() {
        return apiClient.get<OverviewSnapshot>('/overview');
    },
};
