import { defineStore } from 'pinia';
import type {
    TaskExecutionIncident,
    TaskExecutionLogEntry,
    TaskExecutionSnapshot,
} from '@/api/modules/tasks';

const SCSI_METRICS_PREFERENCE_KEY = 'ltfs-capybara.scsiMetricsEnabled';

function readScsiMetricsPreference() {
    if (typeof window === 'undefined') {
        return true;
    }

    const raw = window.localStorage.getItem(SCSI_METRICS_PREFERENCE_KEY);
    if (raw === null) {
        return true;
    }

    return raw !== 'false';
}

export const useExecutionStore = defineStore('execution', {
    state: () => ({
        executions: [] as TaskExecutionSnapshot[],
        incidents: [] as TaskExecutionIncident[],
        logs: [] as TaskExecutionLogEntry[],
        scsiMetricsEnabledPreference: readScsiMetricsPreference(),
    }),

    getters: {
        activeExecution: state =>
            state.executions.find(execution =>
                ['pending', 'running', 'waiting-for-confirmation'].includes(execution.status),
            ) ?? null,
    },

    actions: {
        setExecutions(executions: TaskExecutionSnapshot[]) {
            this.executions = executions;
        },

        upsertExecution(execution: TaskExecutionSnapshot) {
            const index = this.executions.findIndex(
                item => item.executionId === execution.executionId,
            );
            if (index >= 0) {
                this.executions[index] = execution;
                return;
            }

            this.executions.unshift(execution);
        },

        upsertIncident(incident: TaskExecutionIncident) {
            const index = this.incidents.findIndex(item => item.incidentId === incident.incidentId);
            if (index >= 0) {
                this.incidents[index] = incident;
                return;
            }

            this.incidents.unshift(incident);
        },

        appendLog(log: TaskExecutionLogEntry) {
            const index = this.logs.findIndex(item => item.logId === log.logId);
            if (index >= 0) {
                this.logs[index] = log;
                return;
            }

            this.logs.unshift(log);
            if (this.logs.length > 200) {
                this.logs.length = 200;
            }
        },

        setScsiMetricsEnabledPreference(enabled: boolean) {
            this.scsiMetricsEnabledPreference = enabled;
            if (typeof window !== 'undefined') {
                window.localStorage.setItem(SCSI_METRICS_PREFERENCE_KEY, String(enabled));
            }
        },
    },
});
