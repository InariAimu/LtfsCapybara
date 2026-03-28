<script setup lang="ts">
import { Warning } from '@vicons/ionicons5';
import { NCard, NIcon, NSwitch, NTable, NTag } from 'naive-ui';
import type { UsageInfo } from '@/api/types/tapeInfo';
import { formatFileSize } from '@/utils/formatFileSize';

interface UsageMetrics {
    usageInfo: UsageInfo | null;
    totalWrite: number;
    totalRead: number;
    fveText: string;
    hasFatalSuspendedWrites: boolean;
}

interface Props {
    hideSensitive: boolean;
    usage: UsageMetrics;
}

const props = defineProps<Props>();
const emit = defineEmits<{
    'update:hideSensitive': [value: boolean];
}>();

function updateHideSensitive(value: boolean) {
    emit('update:hideSensitive', value);
}
</script>

<template>
    <n-card title="Medium Usage" size="small" class="tape-info-card">
        <n-table striped>
            <tbody>
                <tr>
                    <td style="width: 40%">Last Drive SN</td>
                    <td>
                        <div class="usage-value-row">
                            <span
                                class="usage-sensitive-value"
                                :class="{ 'usage-sensitive-value-blurred': hideSensitive }"
                                >{{ props.usage.usageInfo?.drvSN || '' }}</span
                            >
                            <n-switch
                                :value="hideSensitive"
                                size="small"
                                @update:value="updateHideSensitive"
                            />
                        </div>
                    </td>
                </tr>
                <tr>
                    <td>Load Count</td>
                    <td>
                        {{ props.usage.usageInfo?.threadCount || '' }}
                    </td>
                </tr>
                <tr>
                    <td>Total</td>
                    <td>
                        <n-tag :type="'success'" :size="'tiny'"> Read</n-tag
                        ><span style="margin: 0 16px 0 8px">
                            {{ formatFileSize(props.usage.totalRead) }}</span
                        >
                        <n-tag :type="'error'" :size="'tiny'"> Write</n-tag>
                        <span style="margin: 0 16px 0 8px">{{
                            formatFileSize(props.usage.totalWrite)
                        }}</span>
                        <n-tag :type="'info'" :size="'tiny'">FVE</n-tag>
                        <span style="margin: 0 16px 0 8px">{{ props.usage.fveText }}</span>
                    </td>
                </tr>
                <tr>
                    <td>RW Retries</td>
                    <td>
                        <n-tag :type="'success'" :size="'tiny'"> Read</n-tag
                        ><span style="margin: 0 16px 0 8px">
                            {{ props.usage.usageInfo?.lifeReadRetries }}</span
                        >
                        <n-tag :type="'error'" :size="'tiny'"> Write</n-tag>
                        <span style="margin: 0 16px 0 8px">{{
                            props.usage.usageInfo?.lifeWriteRetries
                        }}</span>
                    </td>
                </tr>
                <tr>
                    <td>RW Unrecovered</td>
                    <td>
                        <n-tag :type="'success'" :size="'tiny'"> Read</n-tag
                        ><span style="margin: 0 16px 0 8px">
                            {{ props.usage.usageInfo?.lifeUnRecovReads }}</span
                        >
                        <n-tag :type="'error'" :size="'tiny'"> Write</n-tag>
                        <span style="margin: 0 16px 0 8px">{{
                            props.usage.usageInfo?.lifeUnRecovWrites
                        }}</span>
                    </td>
                </tr>
                <tr>
                    <td>Suspended Writes / Append</td>
                    <td>
                        {{ props.usage.usageInfo?.lifeSuspendedWrites }} /
                        {{ props.usage.usageInfo?.lifeSuspendedAppendWrites }}
                    </td>
                </tr>
                <tr>
                    <td>Fatal Suspended Writes</td>
                    <td>
                        <span class="fatal-warning-value">
                            <span>{{ props.usage.usageInfo?.lifeFatalSusWrites }}</span>
                            <n-icon
                                v-if="props.usage.hasFatalSuspendedWrites"
                                class="fatal-warning-icon"
                                :size="15"
                            >
                                <warning />
                            </n-icon>
                        </span>
                    </td>
                </tr>
            </tbody>
        </n-table>
    </n-card>
</template>

<style scoped>
.tape-info-card {
    margin-bottom: 0;
}

.usage-value-row {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 8px;
}

.usage-sensitive-value {
    transition: filter 0.15s ease;
}

.usage-sensitive-value-blurred {
    filter: blur(4px);
}

.fatal-warning-value {
    display: inline-flex;
    align-items: center;
    gap: 6px;
}

.fatal-warning-icon {
    color: #f0ad00;
}
</style>
