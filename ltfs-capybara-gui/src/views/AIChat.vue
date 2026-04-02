<script setup lang="ts">
import { computed, nextTick, ref } from 'vue';
import { marked } from 'marked';
import { NButton, NCard, NInput, NSelect, NScrollbar, NTag, NSpin } from 'naive-ui';
import { useI18n } from 'vue-i18n';
import { API_BASE } from '../api/baseurl';

defineOptions({
    name: 'AIChat',
});

const { t } = useI18n();

type ChatRole = 'assistant' | 'user';
type DeltaType = 'reasoning' | 'answering' | 'tool' | 'selecting';

interface ChatMessage {
    id: number;
    role: ChatRole;
    content: string;
    html?: string;
    isStreaming?: boolean;
    blobType?: DeltaType;
    isCollapsed?: boolean;
}

interface OpenAiMessage {
    role: 'assistant' | 'user';
    content: string;
}

interface OpenAiStreamChunk {
    choices?: Array<{
        delta?: {
            content?: string;
            reasoning?: string;
            reasoning_content?: string;
            tool_calls?: OpenAiToolCallDelta[];
        };
    }>;
}

interface OpenAiToolCallDelta {
    index?: number;
    id?: string;
    type?: string;
    function?: {
        name?: string;
        arguments?: string;
    };
}

interface ParsedSseEvent {
    dataLines: string[];
}

type StreamRaw = OpenAiStreamChunk & {
    id: string;
    object: string;
    created: number;
    model: string;
    ltfs_stage?: string;
    system_fingerprint?: string;
    logprobs?: unknown;
    finish_reason?: string;
};

interface StreamUpdate {
    raw?: StreamRaw;
    deltaText?: string;
    deltaType?: DeltaType;
    done?: boolean;
}

const modelOptions = [
    { label: 'DeepSeek Chat', value: 'deepseek-chat' },
    { label: 'GPT-5.3-Codex', value: 'gpt-5.3-codex' },
    { label: 'GPT-4.1', value: 'gpt-4.1' },
    { label: 'o4-mini', value: 'o4-mini' },
];

const selectedModel = ref('deepseek-chat');
const prompt = ref('');
const isSending = ref(false);
const nextMessageId = ref(0);
const messages = ref<ChatMessage[]>([]);
const chatEndRef = ref<HTMLElement | null>(null);

const canSend = computed(() => prompt.value.trim().length > 0 && !isSending.value);
const canResend = computed(() => findLastUserMessage() !== null && !isSending.value);
const showPendingAssistantBubble = computed(() => {
    if (!isSending.value) {
        return false;
    }

    return !messages.value.some(item => item.role === 'assistant' && item.isStreaming);
});

function getAssistantBlobTitle(blobType?: DeltaType) {
    switch (blobType) {
        case 'selecting':
            return t('aiChat.blobs.selecting');
        case 'reasoning':
            return t('aiChat.blobs.reasoning');
        case 'tool':
            return t('aiChat.blobs.tool');
        case 'answering':
            return t('aiChat.blobs.answering');
        default:
            return t('aiChat.blobs.assistant');
    }
}

function getAssistantBlobClass(blobType?: DeltaType) {
    switch (blobType) {
        case 'selecting':
            return 'blob-selecting';
        case 'reasoning':
            return 'blob-reasoning';
        case 'tool':
            return 'blob-tool';
        case 'answering':
            return 'blob-answer';
        default:
            return 'blob-default';
    }
}

function isCollapsibleBlob(message: ChatMessage) {
    return (
        message.blobType === 'reasoning' ||
            message.blobType === 'tool' ||
            message.blobType === 'selecting'
    );
}

function toggleBlobCollapse(message: ChatMessage) {
    if (!isCollapsibleBlob(message)) {
        return;
    }

    message.isCollapsed = !message.isCollapsed;
}

function getCollapseIcon(message: ChatMessage) {
    return message.isCollapsed ? '\u25BC' : '\u25B2';
}

function scrollChatToBottom() {
    nextTick(() => {
        chatEndRef.value?.scrollIntoView({ block: 'end' });
    });
}

function buildRequestPayload(sourceMessages: OpenAiMessage[]) {
    return {
        model: selectedModel.value,
        messages: sourceMessages,
        thinking: {
            type: 'enabled',
        },
        stream: true,
    };
}

function toOpenAiMessages(chatMessages: ChatMessage[]): OpenAiMessage[] {
    return chatMessages.map(item => ({
        role: item.role,
        content: item.content,
    }));
}

function findLastUserMessage() {
    for (let i = messages.value.length - 1; i >= 0; i -= 1) {
        if (messages.value[i].role === 'user') {
            return messages.value[i];
        }
    }

    return null;
}

function parseSseEvents(buffer: string) {
    const chunks = buffer.split(/\r?\n\r?\n/);
    const pending = chunks.pop() ?? '';

    return {
        pending,
        events: chunks,
    };
}

function parseSseEvent(eventText: string): ParsedSseEvent {
    const lines = eventText.split(/\r?\n/);
    const dataLines: string[] = [];

    for (const raw of lines) {
        const line = raw.replace(/\r$/, '');
        if (!line) {
            continue;
        }

        if (line.startsWith('data:')) {
            const data = line.slice(5);
            dataLines.push(data.startsWith(' ') ? data.slice(1) : data);
        }
    }

    return { dataLines };
}

function readUpdatesFromEvent(eventText: string): StreamUpdate[] {
    const parsedEvent = parseSseEvent(eventText);
    const updates: StreamUpdate[] = [];

    for (const payload of parsedEvent.dataLines) {
        if (!payload) {
            continue;
        }

        if (payload === '[DONE]') {
            updates.push({ done: true });
            continue;
        }

        let chunk: StreamRaw | null = null;
        try {
            chunk = JSON.parse(payload) as StreamRaw;
        } catch {
            // Some providers emit plain text chunks in data lines.
            updates.push({ deltaText: payload });
            continue;
        }

        const delta = chunk.choices?.[0]?.delta;
        if (!delta) {
            continue;
        }

        const isToolSelectionStage = chunk.ltfs_stage === 'tool_selection';

        const reasoningText =
            typeof delta.reasoning_content === 'string'
                ? delta.reasoning_content
                : typeof delta.reasoning === 'string'
                  ? delta.reasoning
                  : '';
        if (reasoningText.length > 0) {
            updates.push({
                raw: chunk,
                deltaText: reasoningText,
                deltaType: isToolSelectionStage ? 'selecting' : 'reasoning',
            });
        }

        if (!isToolSelectionStage && Array.isArray(delta.tool_calls) && delta.tool_calls.length > 0) {
            updates.push({
                raw: chunk,
                deltaText: renderToolCallDelta(delta.tool_calls),
                deltaType: 'tool',
            });
        }

        if (typeof delta.content === 'string' && delta.content.length > 0) {
            updates.push({
                raw: chunk,
                deltaText: delta.content,
                deltaType: isToolSelectionStage ? 'selecting' : 'answering',
            });
        }
    }

    return updates;
}

function renderToolCallDelta(toolCalls: OpenAiToolCallDelta[]) {
    const rows: string[] = [];

    for (const item of toolCalls) {
        const nameDelta = item.function?.name ?? '';
        const argsDelta = item.function?.arguments ?? '';
        const detail = [nameDelta ?? '', argsDelta ?? ''].filter(Boolean).join();
        rows.push(`${detail ? `${detail}` : ''}`);
    }
    return rows.join();
}

function createAssistantMessage(blobType?: DeltaType) {
    const message: ChatMessage = {
        id: ++nextMessageId.value,
        role: 'assistant',
        content: '',
        html: '',
        isStreaming: true,
        blobType,
        isCollapsed: false,
    };
    messages.value.push(message);
    // Return the reactive proxy Vue created when the object was pushed,
    // not the original plain object; mutations must go through the proxy.
    return messages.value[messages.value.length - 1];
}

function parseMarkdown(content: string) {
    return marked.parse(content, { async: false }) as string;
}

function finalizeAssistantMessage(message: ChatMessage | null) {
    if (!message) {
        return;
    }

    message.isStreaming = false;
    message.html = parseMarkdown(message.content);
    if (isCollapsibleBlob(message)) {
        message.isCollapsed = true;
    }
}

async function streamChat(
    payload: ReturnType<typeof buildRequestPayload>,
    onUpdate: (update: StreamUpdate) => void,
) {
    const response = await fetch(`${API_BASE}api/ai/resend`, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify(payload),
    });

    if (!response.ok) {
        const text = await response.text();
        throw new Error(text || `Request failed (${response.status})`);
    }

    if (!response.body) {
        throw new Error(t('aiChat.errors.missingResponseBody'));
    }

    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let sseBuffer = '';

    while (true) {
        const { done, value } = await reader.read();
        if (done) {
            break;
        }

        sseBuffer += decoder.decode(value, { stream: true });
        const parsed = parseSseEvents(sseBuffer);
        sseBuffer = parsed.pending;

        for (const eventText of parsed.events) {
            const updates = readUpdatesFromEvent(eventText);
            for (const update of updates) {
                onUpdate(update);
            }
        }
    }

    sseBuffer += decoder.decode();
    if (sseBuffer.trim()) {
        const updates = readUpdatesFromEvent(sseBuffer);
        for (const update of updates) {
            onUpdate(update);
        }
    }
}

async function sendWithContext(baseContext: ChatMessage[], userInput?: string) {
    if (isSending.value) {
        return;
    }

    if (userInput) {
        messages.value.push({
            id: ++nextMessageId.value,
            role: 'user',
            content: userInput,
        });
        scrollChatToBottom();
    }

    const allBlobs: ChatMessage[] = [];
    isSending.value = true;

    try {
        const source = [...baseContext];
        if (userInput) {
            source.push({
                id: -1,
                role: 'user',
                content: userInput,
            });
        }

        // Tracks the last (type, blob) per stream ID to detect type transitions.
        const streamState = new Map<string, { type: DeltaType; blob: ChatMessage }>();

        const payload = buildRequestPayload(toOpenAiMessages(source));
        await streamChat(payload, update => {
            if (update.done) {
                for (const { blob } of streamState.values()) {
                    finalizeAssistantMessage(blob);
                }
                streamState.clear();
                return;
            }

            if (update.deltaText && update.deltaType) {
                const rawId = update.raw?.id ?? '__default__';
                const current = streamState.get(rawId);
                if (!current || current.type !== update.deltaType) {
                    if (current) {
                        finalizeAssistantMessage(current.blob);
                    }
                    const blob = createAssistantMessage(update.deltaType);
                    streamState.set(rawId, { type: update.deltaType, blob });
                    allBlobs.push(blob);
                }

                if (update.deltaType === 'tool') {
                    console.log('Received tool call delta:', update.raw);
                }

                streamState.get(rawId)!.blob.content += update.deltaText;
                scrollChatToBottom();
            }
        });

        for (const blob of allBlobs) {
            if (blob.isStreaming) {
                finalizeAssistantMessage(blob);
            }
        }

        if (allBlobs.length === 0) {
            const fallback = createAssistantMessage();
            fallback.content = t('aiChat.errors.noContentReturned');
            fallback.isStreaming = false;
            fallback.html = parseMarkdown(fallback.content);
            scrollChatToBottom();
        }
    } catch (error) {
        for (const blob of allBlobs) {
            finalizeAssistantMessage(blob);
        }
        const message = error instanceof Error ? error.message : String(error);
        const failure = createAssistantMessage();
        failure.content = t('aiChat.errors.requestFailed', { message });
        failure.isStreaming = false;
        failure.html = parseMarkdown(failure.content);
        scrollChatToBottom();
    } finally {
        isSending.value = false;
    }
}

async function handleSend() {
    const input = prompt.value.trim();
    if (!input || isSending.value) {
        return;
    }

    const context = [...messages.value];
    prompt.value = '';
    await sendWithContext(context, input);
}

async function handleResend() {
    const lastUser = findLastUserMessage();
    if (!lastUser || isSending.value) {
        return;
    }

    const lastUserIndex = messages.value.findIndex(item => item.id === lastUser.id);
    if (lastUserIndex < 0) {
        return;
    }

    const context = messages.value.slice(0, lastUserIndex + 1);
    await sendWithContext(context);
}

function handleClear() {
    messages.value = [];
    nextMessageId.value = 0;
    scrollChatToBottom();
}
</script>

<template>
    <div class="ai-chat-page">
        <n-card class="chat-card" :bordered="false" size="small">
            <div class="chat-head">
                <div class="title-wrap">
                    <span class="title-dot" />
                    <div>
                        <div class="title">{{ t('aiChat.title') }}</div>
                        <div class="subtitle">{{ t('aiChat.subtitle') }}</div>
                    </div>
                </div>
                <div class="head-actions">
                    <n-select
                        v-model:value="selectedModel"
                        :options="modelOptions"
                        size="small"
                        class="model-select"
                    />
                    <n-button size="small" quaternary :disabled="!canResend" @click="handleResend"
                        >{{ t('aiChat.actions.resend') }}</n-button
                    >
                    <n-button size="small" quaternary @click="handleClear">{{
                        t('aiChat.actions.clear')
                    }}</n-button>
                </div>
            </div>

            <div class="chat-body">
                <n-scrollbar class="chat-scroll">
                    <div class="chat-list">
                        <div v-if="messages.length === 0" class="empty-chat">
                            {{ t('aiChat.emptyState') }}
                        </div>
                        <div
                            v-for="item in messages"
                            :key="item.id"
                            class="message-row"
                            :class="item.role"
                        >
                            <div class="message-meta">
                                <n-tag size="small" round :bordered="false" class="role-tag">
                                    {{
                                        item.role === 'assistant'
                                            ? getAssistantBlobTitle(item.blobType)
                                            : t('aiChat.blobs.you')
                                    }}
                                </n-tag>
                                <button
                                    v-if="isCollapsibleBlob(item)"
                                    type="button"
                                    class="collapse-toggle"
                                    :aria-label="
                                        item.isCollapsed
                                            ? t('aiChat.actions.expandSection')
                                            : t('aiChat.actions.collapseSection')
                                    "
                                    :title="
                                        item.isCollapsed
                                            ? t('aiChat.actions.expand')
                                            : t('aiChat.actions.collapse')
                                    "
                                    @click="toggleBlobCollapse(item)"
                                >
                                    {{ getCollapseIcon(item) }}
                                </button>
                            </div>
                            <div
                                v-if="item.role === 'assistant' && item.isStreaming && !item.isCollapsed"
                                class="bubble bubble-text"
                                :class="getAssistantBlobClass(item.blobType)"
                            >
                                {{ item.content }}
                            </div>
                            <div
                                v-else-if="item.role === 'assistant' && !item.isCollapsed"
                                class="bubble"
                                :class="getAssistantBlobClass(item.blobType)"
                                v-html="item.html || item.content"
                            />
                            <div v-else-if="item.role === 'user'" class="bubble"><p>{{ item.content }}</p></div>
                            <div v-else></div>
                        </div>
                        <div v-if="showPendingAssistantBubble" class="message-row assistant">
                            <n-tag size="small" round :bordered="false" class="role-tag">
                                {{ t('aiChat.blobs.assistant') }}
                            </n-tag>
                            <div class="bubble loading-bubble">
                                <n-spin size="small" />
                                <span class="thinking-text">{{ t('aiChat.status.thinking') }}</span>
                            </div>
                        </div>
                        <div ref="chatEndRef" class="chat-end-anchor" />
                    </div>
                </n-scrollbar>
            </div>

            <div class="chat-compose">
                <n-input
                    v-model:value="prompt"
                    type="textarea"
                    :autosize="{ minRows: 3, maxRows: 6 }"
                    :placeholder="t('aiChat.promptPlaceholder')"
                    @keydown.enter.exact.prevent="handleSend"
                />
                <div class="compose-bar">
                    <div class="hint">{{ t('aiChat.sendHint') }}</div>
                    <n-button
                        type="primary"
                        :disabled="!canSend"
                        :loading="isSending"
                        @click="handleSend"
                    >
                        {{ t('aiChat.actions.send') }}
                    </n-button>
                </div>
            </div>
        </n-card>
    </div>
</template>

<style scoped>
.ai-chat-page {
    /* height: 100%; */
    min-height: 0;
}

.chat-card {
    --chat-card-color: var(--n-color, #1f2937);
    --chat-card-color-embedded: var(--n-color-embedded, #111827);
    --chat-border-color: var(--n-border-color, rgb(255 255 255 / 12%));
    --chat-text-color: var(--n-text-color, #e5e7eb);
    --chat-text-color-2: var(--n-text-color-2, #cbd5e1);
    --chat-text-color-3: var(--n-text-color-3, #94a3b8);

    height: 100%;
    background: var(--chat-card-color);
    border: 1px solid var(--chat-border-color);
    box-shadow:
        0 8px 22px rgb(2 8 18 / 24%),
        inset 0 0 0 1px rgb(255 255 255 / 4%);
    display: flex;
    flex-direction: column;
}

.chat-head {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 10px;
    margin-bottom: 12px;
}

.title-wrap {
    display: flex;
    align-items: center;
    gap: 10px;
    min-width: 0;
}

.title-dot {
    width: 9px;
    height: 9px;
    border-radius: 999px;
    background: #4ad6a7;
    box-shadow: 0 0 14px rgb(74 214 167 / 85%);
}

.title {
    color: var(--chat-text-color);
    font-size: 16px;
    font-weight: 600;
    line-height: 1.2;
}

.subtitle {
    color: var(--chat-text-color-3);
    font-size: 12px;
    line-height: 1.25;
}

.head-actions {
    display: flex;
    align-items: center;
    gap: 8px;
}

.model-select {
    width: 170px;
}

.chat-body {
    flex: 1;
    min-height: 0;
    border-radius: 12px;
    background: var(--chat-card-color-embedded);
    border: 1px solid var(--chat-border-color);
}

.chat-scroll {
    height: 100%;
}

.chat-list {
    display: flex;
    flex-direction: column;
    gap: 12px;
    padding: 12px;
}

.chat-end-anchor {
    height: 1px;
}

.empty-chat {
    color: var(--chat-text-color-3);
    font-size: 13px;
    line-height: 1.4;
}

.message-row {
    display: flex;
    flex-direction: column;
    gap: 6px;
    max-width: min(78%, 780px);
}

.message-row.user {
    margin-left: auto;
    align-items: flex-end;
}

.message-row.assistant {
    margin-right: auto;
    align-items: flex-start;
}

.role-tag {
    font-size: 11px;
}

.message-meta {
    display: flex;
    align-items: center;
    gap: 6px;
}

.collapse-toggle {
    border: none;
    background: transparent;
    color: var(--chat-text-color-3);
    font-size: 12px;
    line-height: 1;
    padding: 2px 4px;
    cursor: pointer;
    border-radius: 4px;
    min-width: 20px;
}

.collapse-toggle:hover {
    color: var(--chat-text-color);
    background: rgb(255 255 255 / 6%);
}

.bubble {
    border-radius: 8px;
    padding: 0px 10px;
    line-height: 1.45;
    color: var(--chat-text-color);
    word-break: break-word;
    box-shadow: 0 3px 14px rgb(0 0 0 / 16%);
}

.bubble-text {
    white-space: pre-wrap;
}

.bubble :deep(h1),
.bubble :deep(h2),
.bubble :deep(h3),
.bubble :deep(h4) {
    margin: 0.35em 0;
    color: var(--chat-text-color);
}

.bubble :deep(p),
.bubble :deep(ul),
.bubble :deep(ol) {
    margin: 0.45em 0;
}

.bubble :deep(code) {
    padding: 0.1em 0.35em;
    border-radius: 6px;
    background: var(--chat-card-color-embedded);
    border: 1px solid var(--chat-border-color);
}

.bubble :deep(pre) {
    margin: 0.6em 0;
    padding: 0.65em;
    overflow-x: auto;
    border-radius: 8px;
    background: var(--chat-card-color-embedded);
    border: 1px solid var(--chat-border-color);
}

.message-row.assistant .bubble {
    background: var(--chat-card-color);
    border: 1px solid var(--chat-border-color);
}

.message-row.assistant .bubble.blob-reasoning {
    background: color-mix(in srgb, #f59e0b 16%, var(--chat-card-color));
    border-color: color-mix(in srgb, #f59e0b 40%, var(--chat-border-color));
    font-size: 12px;
}

.message-row.assistant .bubble.blob-selecting {
    background: color-mix(in srgb, #7c3aed 16%, var(--chat-card-color));
    border-color: color-mix(in srgb, #7c3aed 40%, var(--chat-border-color));
    font-size: 12px;
    white-space: pre-wrap;
}

.message-row.assistant .bubble.blob-tool {
    background: color-mix(in srgb, #06b6d4 15%, var(--chat-card-color));
    border-color: color-mix(in srgb, #06b6d4 40%, var(--chat-border-color));
    font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, 'Liberation Mono',
        'Courier New', monospace;
    font-size: 12px;
}

.message-row.assistant .bubble.blob-answer {
    background: color-mix(in srgb, #22c55e 12%, var(--chat-card-color));
    border-color: color-mix(in srgb, #22c55e 34%, var(--chat-border-color));
}

.loading-bubble {
    display: flex;
    align-items: center;
    gap: 8px;
}

.thinking-text {
    color: var(--chat-text-color-3);
    font-style: italic;
}

.message-row.user .bubble {
    background: var(--chat-card-color-embedded);
    border: 1px solid var(--chat-border-color);
}

.chat-compose {
    margin-top: 12px;
    display: flex;
    flex-direction: column;
    gap: 8px;
    padding: 10px;
    border-radius: 12px;
    border: 1px solid var(--chat-border-color);
    background: var(--n-color-embedded);
    box-shadow: var(--n-box-shadow);
}

.compose-bar {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 8px;
}

.hint {
    color: var(--chat-text-color-2);
    font-size: 12px;
}

@media (max-width: 768px) {
    .ai-chat-page {
        padding: 10px;
    }

    .chat-head {
        flex-direction: column;
        align-items: flex-start;
    }

    .head-actions {
        width: 100%;
    }

    .model-select {
        flex: 1;
        width: auto;
    }

    .message-row {
        max-width: 100%;
    }
}
</style>
