<script setup lang="ts">
import { onMounted, onBeforeUnmount } from 'vue';
import { useMessage } from 'naive-ui';
import { taskApi, type TaskExecutionEventEnvelope } from '@/api/modules/tasks';
import { API_BASE } from '@/api/baseurl';
import { useExecutionStore } from '@/stores/executionStore';
import { useFileStore } from '@/stores/fileStore';

const message = useMessage();
const executionStore = useExecutionStore();
const fileStore = useFileStore();

let eventSource: EventSource | null = null;
let taskGroupRefreshPromise: Promise<void> | null = null;

function isTerminalStatus(status: string | null | undefined) {
    return ['completed', 'failed', 'cancelled'].includes(String(status || '').toLowerCase());
}

function refreshTaskGroups() {
    if (taskGroupRefreshPromise) {
        return taskGroupRefreshPromise;
    }

    taskGroupRefreshPromise = taskApi
        .listGroups()
        .then(response => {
            fileStore.setTaskGroups(response.data ?? []);
        })
        .catch(error => {
            console.error('TaskExecutionBridge refreshTaskGroups error', error);
        })
        .finally(() => {
            taskGroupRefreshPromise = null;
        });

    return taskGroupRefreshPromise;
}

function notifyIncident(event: TaskExecutionEventEnvelope) {
    const incident = event.incident;
    if (!incident) {
        return;
    }

    executionStore.upsertIncident(incident);

    if (incident.action === 'NotifyOnly') {
        message.warning(incident.message);
        return;
    }

    if (incident.action === 'StopAllOperations') {
        message.error(incident.message);
        return;
    }

    if (!incident.requiresConfirmation) {
        return;
    }

    const accepted = window.confirm([incident.message, incident.detail].filter(Boolean).join('\n\n'));
    void taskApi.resolveIncident(
        incident.executionId,
        incident.incidentId,
        accepted ? 'continue' : 'abort',
    ).catch(error => {
        console.error('resolveIncident error', error);
        message.error('Failed to resolve tape incident.');
    });
}

function handleEvent(rawEvent: MessageEvent<string>) {
    const event = JSON.parse(rawEvent.data) as TaskExecutionEventEnvelope;

    if (event.execution) {
        const previous = executionStore.executions.find(
            execution => execution.executionId === event.execution?.executionId,
        );
        executionStore.upsertExecution(event.execution);

        if (
            isTerminalStatus(event.execution.status) &&
            (!previous || previous.status !== event.execution.status)
        ) {
            void refreshTaskGroups();
            if (event.execution.status === 'completed') {
                fileStore.bumpLocalTapeListRevision();
            }
        }
    }

    if (event.log) {
        executionStore.appendLog(event.log);
    }

    if (event.type === 'incident-raised' || event.type === 'incident-resolved') {
        notifyIncident(event);
    }
}

onMounted(() => {
    eventSource = new EventSource(`${API_BASE}api/tasks/events`);
    eventSource.onmessage = handleEvent;
    eventSource.onerror = error => {
        console.error('TaskExecutionBridge SSE error', error);
    };
});

onBeforeUnmount(() => {
    eventSource?.close();
    eventSource = null;
});
</script>

<template></template>