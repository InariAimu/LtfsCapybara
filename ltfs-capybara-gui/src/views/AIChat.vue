<script setup lang="ts">
import { computed, ref } from 'vue';
import { marked } from 'marked';
import { NButton, NCard, NInput, NSelect, NScrollbar, NTag } from 'naive-ui';

type ChatRole = 'assistant' | 'user';

interface ChatMessage {
    id: number;
    role: ChatRole;
    content: string;
    html?: string;
}

const modelOptions = [
    { label: 'GPT-5.3-Codex', value: 'gpt-5.3-codex' },
    { label: 'GPT-4.1', value: 'gpt-4.1' },
    { label: 'o4-mini', value: 'o4-mini' },
];

const selectedModel = ref('gpt-5.3-codex');
const prompt = ref('');
const isSending = ref(false);
const nextMessageId = ref(2);
const messages = ref<ChatMessage[]>([
    {
        id: 1,
        role: 'assistant',
        content: 'AI Chat is ready. Request and result parsing are currently placeholder logic.',
    },
]);

const canSend = computed(() => prompt.value.trim().length > 0 && !isSending.value);

function buildRequestPayload(input: string) {
    return {
        model: selectedModel.value,
        input,
        context: messages.value,
    };
}

const markdown = ref('');

function parseAssistantResult(rawResponse: unknown): string {
    void rawResponse;
    markdown.value = "placeholder";
    return marked.parse(markdown.value, { async: false }) as string;
}

async function handleSend() {
    const input = prompt.value.trim();
    if (!input || isSending.value) {
        return;
    }

    messages.value.push({
        id: ++nextMessageId.value,
        role: 'user',
        content: input,
    });

    prompt.value = '';
    isSending.value = true;

    try {
        const payload = buildRequestPayload(input);
        void payload;

        await new Promise(resolve => setTimeout(resolve, 450));

        const placeholderRaw = { ok: true, data: null };
        const parsed = parseAssistantResult(placeholderRaw);

        messages.value.push({
            id: ++nextMessageId.value,
            role: 'assistant',
            content: markdown.value,
            html: parsed,
        });
    } finally {
        isSending.value = false;
    }
}

function handleClear() {
    messages.value = [
        {
            id: 1,
            role: 'assistant',
            content: 'Conversation cleared. Request and parser are still placeholders.',
        },
    ];
    nextMessageId.value = 1;
}
</script>

<template>
    <div class="ai-chat-page">
        <n-card class="chat-card" :bordered="false" size="small">
            <div class="chat-head">
                <div class="title-wrap">
                    <span class="title-dot" />
                    <div>
                        <div class="title">AI Chat</div>
                        <div class="subtitle">Design placeholder with request and parser stubs</div>
                    </div>
                </div>
                <div class="head-actions">
                    <n-select
                        v-model:value="selectedModel"
                        :options="modelOptions"
                        size="small"
                        class="model-select"
                    />
                    <n-button size="small" quaternary @click="handleClear">Clear</n-button>
                </div>
            </div>

            <div class="chat-body">
                <n-scrollbar class="chat-scroll">
                    <div class="chat-list">
                        <div
                            v-for="item in messages"
                            :key="item.id"
                            class="message-row"
                            :class="item.role"
                        >
                            <n-tag size="small" round :bordered="false" class="role-tag">
                                {{ item.role === 'assistant' ? 'Assistant' : 'You' }}
                            </n-tag>
                            <div v-if="item.role === 'assistant'" class="bubble" v-html="item.html || item.content" />
                            <div v-else class="bubble">{{ item.content }}</div>
                        </div>
                    </div>
                </n-scrollbar>
            </div>

            <div class="chat-compose">
                <n-input
                    v-model:value="prompt"
                    type="textarea"
                    :autosize="{ minRows: 3, maxRows: 6 }"
                    placeholder="Describe what to build"
                    @keydown.enter.exact.prevent="handleSend"
                />
                <div class="compose-bar">
                    <div class="hint">Enter to send, Shift+Enter for new line</div>
                    <n-button type="primary" :disabled="!canSend" :loading="isSending" @click="handleSend">
                        Send
                    </n-button>
                </div>
            </div>
        </n-card>
    </div>
</template>

<style scoped>
.chat-card {
    --chat-card-color: var(--n-color, #1f2937);
    --chat-card-color-embedded: var(--n-color-embedded, #111827);
    --chat-border-color: var(--n-border-color, rgb(255 255 255 / 12%));
    --chat-text-color: var(--n-text-color, #e5e7eb);
    --chat-text-color-2: var(--n-text-color-2, #cbd5e1);
    --chat-text-color-3: var(--n-text-color-3, #94a3b8);

    height: 100%;
    border-radius: 16px;
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

.bubble {
    border-radius: 12px;
    padding: 10px 12px;
    line-height: 1.45;
    color: var(--chat-text-color);
    word-break: break-word;
    box-shadow: 0 3px 14px rgb(0 0 0 / 16%);
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

.message-row.user .bubble {
    background: var(--chat-card-color-embedded);
    border: 1px solid var(--chat-border-color);
}

.chat-compose {
    margin-top: 12px;
    display: flex;
    flex-direction: column;
    gap: 8px;
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
