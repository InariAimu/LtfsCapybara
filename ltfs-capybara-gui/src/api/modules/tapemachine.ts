import { apiClient } from '../client';
import type { TapeInfo } from '@/api/types/tapeInfo';

const DEFAULT_LONG_RUNNING_ACTION_TIMEOUT_MS = 180000;
const LONG_RUNNING_ACTIONS = new Set(['thread', 'unthread', 'eject']);

function resolveLongRunningActionTimeoutMs(): number {
    const rawValue = import.meta.env.VITE_TAPEMACHINE_ACTION_TIMEOUT_MS;
    const parsed = Number(rawValue);
    return Number.isFinite(parsed) && parsed > 0 ? parsed : DEFAULT_LONG_RUNNING_ACTION_TIMEOUT_MS;
}

const longRunningActionTimeoutMs = resolveLongRunningActionTimeoutMs();

export type TapeMachineState = 'Unknown' | 'Empty' | 'Loaded' | 'Threaded' | 'Faulted';

export type TapeMachineAction =
    | 'ThreadTape'
    | 'LoadTape'
    | 'UnthreadTape'
    | 'EjectTape'
    | 'ReadInfo';

export interface TapeMachineSnapshot {
    tapeDriveId: string;
    devicePath: string;
    state: TapeMachineState;
    allowedActions: TapeMachineAction[];
    lastError: string | null;
    isFake: boolean;
    cartridgeMemory: TapeInfo | null;
    loadedBarcode: string | null;
    hasLtfsFilesystem: boolean | null;
    ltfsVolumeName: string | null;
}

export const tapeMachineApi = {
    getState(tapeDriveId: string) {
        return apiClient.get<TapeMachineSnapshot>(
            `/tapedrives/${encodeURIComponent(tapeDriveId)}/machine`,
        );
    },
    execute(tapeDriveId: string, action: 'thread' | 'load' | 'unthread' | 'eject' | 'read-info') {
        const requestConfig = LONG_RUNNING_ACTIONS.has(action)
            ? { timeout: longRunningActionTimeoutMs }
            : undefined;

        return apiClient.post<TapeMachineSnapshot>(
            `/tapedrives/${encodeURIComponent(tapeDriveId)}/machine/${action}`,
            undefined,
            requestConfig,
        );
    },
};
