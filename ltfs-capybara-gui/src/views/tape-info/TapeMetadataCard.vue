<script setup lang="ts">
import { NCard, NSwitch, NTable, NTag } from 'naive-ui';
import type { TapeInfo } from '@/api/types/tapeInfo';
import type { LtoFormatStyle } from '@/utils/tapeFormatStyle';

interface TapeMetadata {
    particleTypeLabel: string;
    mfgAgeText: string;
    mfgAgeColor: string;
    formatStyle: LtoFormatStyle;
}

interface Props {
    tapeInfo: TapeInfo | null;
    hideSensitive: boolean;
    meta: TapeMetadata;
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
    <n-card title="Tape Info" size="small" class="tape-info-card">
        <n-table striped>
            <tbody>
                <tr>
                    <td style="width: 40%">Barcode</td>
                    <td>
                        <div class="usage-value-row">
                            <span
                                class="usage-sensitive-value"
                                :class="{ 'usage-sensitive-value-blurred': hideSensitive }"
                                >{{ props.tapeInfo?.applicationSpecific.barCode || '' }}</span
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
                    <td>Application</td>
                    <td>
                        <span>{{ props.tapeInfo?.applicationSpecific.vendor || '' }}</span
                        >&nbsp; <span>{{ props.tapeInfo?.applicationSpecific.name || '' }}</span
                        >&nbsp;
                        <span>{{ props.tapeInfo?.applicationSpecific.version || '' }}</span>
                    </td>
                </tr>
                <tr>
                    <td>Format</td>
                    <td>
                        <div class="format-cell">
                            <span>{{ props.tapeInfo?.manufacturer.format || '' }}</span>
                            <n-tag :type="'success'" :size="'tiny'">{{
                                meta.particleTypeLabel
                            }}</n-tag>
                            <span
                                v-if="meta.formatStyle.color"
                                class="format-color-swatch"
                                :class="{ 'format-color-swatch-worm': meta.formatStyle.isWorm }"
                                :style="{
                                    backgroundColor: meta.formatStyle.color,
                                    '--worm-corner-color': meta.formatStyle.wormCornerColor,
                                }"
                                aria-label="LTO format color"
                            />
                        </div>
                    </td>
                </tr>
                <tr>
                    <td>Serial Number</td>
                    <td>
                        <div class="usage-value-row">
                            <span
                                class="usage-sensitive-value"
                                :class="{ 'usage-sensitive-value-blurred': hideSensitive }"
                                >{{ props.tapeInfo?.manufacturer.cartridgeSN || '' }}
                            </span>
                            <n-switch
                                :value="hideSensitive"
                                size="small"
                                @update:value="updateHideSensitive"
                            />
                        </div>
                    </td>
                </tr>
                <tr>
                    <td>Tape Vendor</td>
                    <td>
                        <span>{{ props.tapeInfo?.manufacturer.tapeVendor || '' }}</span
                        >&nbsp;@ <span>{{ props.tapeInfo?.manufacturer.mfgDate || '' }}</span
                        ><span
                            v-if="meta.mfgAgeText"
                            :style="{ color: meta.mfgAgeColor, paddingLeft: '10px' }"
                        >
                            {{ meta.mfgAgeText }}
                        </span>
                    </td>
                </tr>
                <tr>
                    <td>Media Vendor</td>
                    <td>
                        <span>{{ props.tapeInfo?.mediaManufacturer.vendor || '' }}</span
                        >&nbsp;@ <span>{{ props.tapeInfo?.mediaManufacturer.mfgDate || '' }}</span>
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

.format-cell {
    display: inline-flex;
    align-items: center;
    gap: 8px;
}

.format-color-swatch {
    position: relative;
    width: 14px;
    height: 14px;
    border: 1px solid rgba(0, 0, 0, 0.2);
    border-radius: 2px;
    flex-shrink: 0;
}

.format-color-swatch-worm::after {
    content: '';
    position: absolute;
    right: 0;
    bottom: 0;
    width: 0;
    height: 0;
    border-style: solid;
    border-width: 0 0 7px 7px;
    border-color: transparent transparent var(--worm-corner-color, #9aa0a6) transparent;
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
</style>
