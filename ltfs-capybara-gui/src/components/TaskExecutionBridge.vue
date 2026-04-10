<script setup lang="ts">
import { onMounted, onBeforeUnmount } from 'vue';
import { useMessage } from 'naive-ui';
import { taskApi, type TaskExecutionEventEnvelope } from '@/api/modules/tasks';
import { API_BASE } from '@/api/baseurl';
import { useExecutionStore } from '@/stores/executionStore';

const message = useMessage();
const executionStore = useExecutionStore();

let eventSource: EventSource | null = null;

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
        executionStore.upsertExecution(event.execution);
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