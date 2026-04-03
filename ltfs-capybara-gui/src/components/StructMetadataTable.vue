<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { NAlert, NCard, NCode, NEmpty, NTag } from 'naive-ui';
import { useI18n } from 'vue-i18n';
import type { StructMetadataDocument, StructMetadataField } from '@/api/types/structMetadata';

interface Props {
    payload: StructMetadataDocument | string | null;
    title?: string;
}

const props = defineProps<Props>();
const { t } = useI18n();
const selectedMemberName = ref<string | null>(null);

const bitLabels = [7, 6, 5, 4, 3, 2, 1, 0] as const;

type RenderFieldCell = {
    kind: 'field';
    key: string;
    field: StructMetadataField;
    rowSpan: number;
    colSpan: number;
    colorIndex: number;
};

type RenderEmptyCell = {
    kind: 'empty';
    key: string;
};

type RenderCell = RenderFieldCell | RenderEmptyCell;

type GridRow = {
    byteIndex: number;
    cells: RenderCell[];
};

const parsedState = computed(() => {
    if (!props.payload) {
        return {
            document: null as StructMetadataDocument | null,
            error: '',
        };
    }

    if (typeof props.payload !== 'string') {
        return {
            document: props.payload,
            error: '',
        };
    }

    try {
        return {
            document: JSON.parse(props.payload) as StructMetadataDocument,
            error: '',
        };
    } catch (error) {
        return {
            document: null,
            error: error instanceof Error ? error.message : String(error),
        };
    }
});

const rows = computed(() => parsedState.value.document?.fields ?? []);

const byteIndexes = computed(() =>
    Array.from({ length: parsedState.value.document?.byteLength ?? 0 }, (_, index) => index),
);

const sortedFields = computed(() =>
    [...rows.value].sort((left, right) => {
        if (left.location.byteIndex !== right.location.byteIndex) {
            return left.location.byteIndex - right.location.byteIndex;
        }

        const leftColumn = getStartColumn(left);
        const rightColumn = getStartColumn(right);
        if (leftColumn !== rightColumn) {
            return leftColumn - rightColumn;
        }

        if (left.location.byteLength !== right.location.byteLength) {
            return right.location.byteLength - left.location.byteLength;
        }

        return left.memberName.localeCompare(right.memberName);
    }),
);

const gridRows = computed<GridRow[]>(() => {
    const rowCount = parsedState.value.document?.byteLength ?? 0;
    const occupancy = Array.from({ length: rowCount }, () => Array.from({ length: 8 }, () => false));
    const startCells = new Map<string, RenderFieldCell>();

    sortedFields.value.forEach((field, index) => {
        const startRow = field.location.byteIndex;
        const rowSpan = field.location.byteLength;
        const startColumn = getStartColumn(field);
        const colSpan = getColumnSpan(field);

        for (let rowIndex = startRow; rowIndex < startRow + rowSpan; rowIndex += 1) {
            for (let columnIndex = startColumn; columnIndex < startColumn + colSpan; columnIndex += 1) {
                if (rowIndex >= 0 && rowIndex < rowCount && columnIndex >= 0 && columnIndex < 8) {
                    occupancy[rowIndex][columnIndex] = true;
                }
            }
        }

        startCells.set(`${startRow}:${startColumn}`, {
            kind: 'field',
            key: `${field.memberName}-${startRow}-${startColumn}`,
            field,
            rowSpan,
            colSpan,
            colorIndex: index,
        });
    });

    return byteIndexes.value.map(byteIndex => {
        const cells: RenderCell[] = [];

        for (let columnIndex = 0; columnIndex < 8; columnIndex += 1) {
            const startCell = startCells.get(`${byteIndex}:${columnIndex}`);
            if (startCell) {
                cells.push(startCell);
                continue;
            }

            if (occupancy[byteIndex][columnIndex]) {
                continue;
            }

            cells.push({
                kind: 'empty',
                key: `empty-${byteIndex}-${columnIndex}`,
            });
        }

        return { byteIndex, cells };
    });
});

const selectedField = computed(() => {
    if (!selectedMemberName.value) {
        return null;
    }

    return rows.value.find(field => field.memberName === selectedMemberName.value) ?? null;
});

watch(
    rows,
    fields => {
        if (fields.length === 0) {
            selectedMemberName.value = null;
            return;
        }

        if (!fields.some(field => field.memberName === selectedMemberName.value)) {
            selectedMemberName.value = (fields.find(field => !field.isReserved) ?? fields[0]).memberName;
        }
    },
    { immediate: true },
);

function formatLocation(row: StructMetadataField): string {
    const location = row.location;
    const byteLabel = `${t('structInspector.location.byte')} ${location.byteIndex}`;
    const endByteLabel =
        location.endByteIndex !== location.byteIndex
            ? `-${location.endByteIndex}`
            : '';
    const bitLabel =
        location.bitIndex == null || location.bitLength == null
            ? ''
            : `, ${t('structInspector.location.bit')} ${location.bitIndex}, ${t('structInspector.location.length')} ${location.bitLength}`;

    return `${byteLabel}${endByteLabel}, ${t('structInspector.location.bytes')} ${location.byteLength}${bitLabel}`;
}

function getColumnSpan(field: StructMetadataField): number {
    return field.location.bitLength ?? 8;
}

function getStartColumn(field: StructMetadataField): number {
    if (field.location.bitIndex == null || field.location.bitLength == null) {
        return 0;
    }

    const topBit = field.location.bitIndex + field.location.bitLength - 1;
    return 7 - topBit;
}

function selectField(field: StructMetadataField): void {
    selectedMemberName.value = field.memberName;
}

function isFieldCell(cell: RenderCell): cell is RenderFieldCell {
    return cell.kind === 'field';
}

function getFieldStyle(colorIndex: number): Record<string, string> {
    const field = sortedFields.value[colorIndex];
    if (field?.isReserved) {
        return {
            '--field-bg': 'rgba(226, 232, 240, 0.92)',
            '--field-border': 'rgba(100, 116, 139, 0.72)',
            '--field-accent': 'rgba(51, 65, 85, 0.9)',
        };
    }

    const hue = (colorIndex * 47) % 360;
    return {
        '--field-bg': `hsla(${hue}, 78%, 86%, 0.96)`,
        '--field-border': `hsl(${hue}, 55%, 45%)`,
        '--field-accent': `hsl(${hue}, 48%, 24%)`,
    };
}
</script>

<template>
    <n-card :title="props.title || t('structInspector.title')" size="small" class="struct-card">
        <template #header-extra>
            <div v-if="parsedState.document" class="struct-summary">
                <span>{{ parsedState.document.typeName }}</span>
                <n-tag size="small" :bordered="false" type="info">
                    {{ parsedState.document.byteLength }} {{ t('structInspector.location.bytes') }}
                </n-tag>
            </div>
        </template>

        <n-alert v-if="parsedState.error" type="error" :title="t('structInspector.invalidJsonTitle')">
            {{ parsedState.error }}
        </n-alert>

        <template v-else-if="parsedState.document">
            <div class="struct-raw-block">
                <span class="struct-raw-label">{{ t('structInspector.rawBytes') }}</span>
                <n-code :code="parsedState.document.rawHex || '-'" word-wrap />
                <span v-if="parsedState.document.description" class="struct-raw-label">
                    {{ parsedState.document.description }}
                </span>
            </div>

            <div class="struct-split-layout">
                <section class="struct-pane struct-visual-pane">
                    <div class="struct-pane-title">{{ t('structInspector.visualTitle') }}</div>
                    <div class="struct-grid-scroller">
                        <table class="struct-grid-table">
                            <thead>
                                <tr>
                                    <th class="struct-grid-corner">
                                        {{ t('structInspector.location.byte') }}
                                    </th>
                                    <th
                                        v-for="bit in bitLabels"
                                        :key="`bit-${bit}`"
                                        class="struct-grid-header"
                                    >
                                        {{ t('structInspector.bitHeader') }} {{ bit }}
                                    </th>
                                </tr>
                            </thead>
                            <tbody>
                                <tr v-for="row in gridRows" :key="`byte-row-${row.byteIndex}`">
                                    <th class="struct-grid-bit-header">
                                        {{ t('structInspector.byteHeader') }} {{ row.byteIndex }}
                                    </th>
                                    <template v-for="cell in row.cells" :key="cell.key">
                                        <td
                                            v-if="isFieldCell(cell)"
                                            class="struct-grid-field"
                                            :class="{
                                                'struct-grid-field-selected':
                                                    selectedField?.memberName === cell.field.memberName,
                                            }"
                                            :rowspan="cell.rowSpan"
                                            :colspan="cell.colSpan"
                                            :style="getFieldStyle(cell.colorIndex)"
                                            @click="selectField(cell.field)"
                                        >
                                            <div class="struct-grid-field-name">
                                                {{ cell.field.displayName }}
                                            </div>
                                            <div class="struct-grid-field-value">
                                                {{ cell.field.formattedValue }}
                                            </div>
                                        </td>
                                        <td v-else class="struct-grid-empty" />
                                    </template>
                                </tr>
                            </tbody>
                        </table>
                    </div>
                </section>

                <section class="struct-pane struct-detail-pane">
                    <div class="struct-pane-title">{{ t('structInspector.detailTitle') }}</div>

                    <template v-if="selectedField">
                        <div class="struct-detail-header">
                            <strong>{{ selectedField.displayName }}</strong>
                            <div
                                v-if="selectedField.displayName !== selectedField.memberName"
                                class="struct-field-subtitle"
                            >
                                {{ selectedField.memberName }}
                            </div>
                        </div>

                        <div class="struct-detail-tags">
                            <n-tag size="small" type="info" :bordered="false">
                                {{ selectedField.encoding }}
                            </n-tag>
                            <n-tag v-if="selectedField.isReserved" size="small" type="warning" :bordered="false">
                                {{ t('structInspector.reservedTag') }}
                            </n-tag>
                            <n-tag size="small" :bordered="false">
                                {{ selectedField.dataType }}
                            </n-tag>
                        </div>

                        <div class="struct-detail-grid">
                            <div class="struct-detail-item">
                                <span class="struct-detail-label">{{ t('structInspector.fields.value') }}</span>
                                <n-code :code="selectedField.formattedValue || '-'" word-wrap />
                            </div>

                            <div class="struct-detail-item">
                                <span class="struct-detail-label">{{ t('structInspector.fields.location') }}</span>
                                <span>{{ formatLocation(selectedField) }}</span>
                            </div>

                            <div class="struct-detail-item">
                                <span class="struct-detail-label">{{ t('structInspector.fields.raw') }}</span>
                                <n-code :code="selectedField.rawHex || '-'" word-wrap />
                            </div>

                            <div class="struct-detail-item">
                                <span class="struct-detail-label">{{ t('structInspector.fields.description') }}</span>
                                <span v-if="selectedField.description || selectedField.matchedValueDescription">
                                    {{ selectedField.description || t('structInspector.noDescription') }}
                                </span>
                                <span v-else>{{ t('structInspector.noDescription') }}</span>
                            </div>

                            <div v-if="selectedField.matchedValueDescription" class="struct-detail-item">
                                <span class="struct-detail-label">{{ t('structInspector.currentValueLabel') }}</span>
                                <span class="struct-description-current">
                                    {{ selectedField.matchedValueDescription }}
                                </span>
                            </div>

                            <div
                                v-if="selectedField.valueDescriptions.length > 0"
                                class="struct-detail-item"
                            >
                                <span class="struct-detail-label">{{ t('structInspector.fields.valueDescriptions') }}</span>
                                <div class="struct-value-descriptions">
                                    <span v-for="item in selectedField.valueDescriptions" class="struct-description-current"
                                    :class="item.isCurrent ? 'struct-tag-description-current':'struct-tag-description'">
                                        {{ item.description ? `${item.value}: ${item.description}` : item.value }}
                                    </span>
                                    <!-- <n-tag
                                        v-for="item in selectedField.valueDescriptions"
                                        :key="`${selectedField.memberName}-${item.value}`"
                                        size="small"
                                        :bordered="false"
                                        :type="item.isCurrent ? 'success' : 'default'"
                                    >
                                        {{ item.description ? `${item.value}: ${item.description}` : item.value }}
                                    </n-tag> -->
                                </div>
                            </div>
                        </div>
                    </template>

                    <n-empty v-else :description="t('structInspector.selectHint')" />
                </section>
            </div>
        </template>

        <n-empty v-else :description="t('structInspector.empty')" />
    </n-card>
</template>

<style scoped>
.struct-card {
    margin-bottom: 0;
}

.struct-summary {
    display: inline-flex;
    align-items: center;
    gap: 8px;
}

.struct-raw-block {
    display: grid;
    gap: 6px;
    margin-bottom: 12px;
}

.struct-raw-label {
    font-size: 12px;
    color: rgba(0, 0, 0, 0.55);
}

.struct-split-layout {
    display: grid;
    grid-template-columns: minmax(0, 1.7fr) minmax(260px, 1fr);
    align-items: start;
    gap: 12px;
}

.struct-pane {
    min-width: 0;
    border: 1px solid rgba(15, 23, 42, 0.08);
    border-radius: 10px;
    padding: 12px;
    background: linear-gradient(180deg, rgba(248, 250, 252, 0.9), rgba(255, 255, 255, 0.96));
}

.struct-pane-title {
    margin-bottom: 10px;
    font-size: 12px;
    font-weight: 700;
    letter-spacing: 0.04em;
    text-transform: uppercase;
    color: rgba(15, 23, 42, 0.6);
}

.struct-grid-scroller {
    overflow: auto;
}

.struct-grid-table {
    width: 100%;
    border-collapse: collapse;
    table-layout: fixed;
    min-width: max-content;
}

.struct-grid-table th,
.struct-grid-table td {
    border: 1px solid rgba(148, 163, 184, 0.35);
}

.struct-grid-corner,
.struct-grid-header,
.struct-grid-bit-header {
    background: rgba(241, 245, 249, 0.95);
    color: rgba(15, 23, 42, 0.72);
    font-size: 12px;
    font-weight: 700;
    text-align: center;
    padding: 8px 6px;
}

.struct-grid-header,
.struct-grid-empty,
.struct-grid-field {
    min-width: 98px;
}

.struct-grid-empty {
    height: 58px;
    background:
        linear-gradient(135deg, rgba(248, 250, 252, 0.9), rgba(255, 255, 255, 1)),
        repeating-linear-gradient(
            45deg,
            rgba(148, 163, 184, 0.08),
            rgba(148, 163, 184, 0.08) 6px,
            transparent 6px,
            transparent 12px
        );
}

.struct-grid-field {
    padding: 8px;
    vertical-align: top;
    background: var(--field-bg);
    box-shadow: inset 0 0 0 1px var(--field-border);
    cursor: pointer;
    transition:
        transform 0.12s ease,
        box-shadow 0.12s ease,
        filter 0.12s ease;
}

.struct-grid-field:hover {
    filter: saturate(1.08);
}

.struct-grid-field-selected {
    box-shadow:
        inset 0 0 0 2px var(--field-border),
        0 0 0 2px color-mix(in srgb, var(--field-border) 32%, transparent);
}

.struct-grid-field-name {
    font-size: 12px;
    font-weight: 700;
    color: var(--field-accent);
    line-height: 1.25;
}

.struct-grid-field-value {
    margin-top: 4px;
    font-size: 11px;
    line-height: 1.35;
    color: rgba(15, 23, 42, 0.72);
    word-break: break-word;
    white-space: pre-wrap;
}

.struct-field-name {
    display: grid;
    gap: 2px;
}

.struct-field-subtitle {
    font-size: 12px;
    opacity: 0.66;
}

.struct-location {
    display: inline-flex;
    align-items: center;
    flex-wrap: wrap;
    gap: 8px;
}

.struct-detail-pane {
    display: grid;
    align-content: start;
    gap: 12px;
    position: sticky;
    top: 12px;
    align-self: start;
    max-height: calc(100vh - 96px);
    overflow: auto;
}

.struct-detail-header {
    display: grid;
    gap: 4px;
}

.struct-detail-tags {
    display: flex;
    flex-wrap: wrap;
    gap: 6px;
}

.struct-detail-grid {
    display: grid;
    gap: 12px;
}

.struct-detail-item {
    display: grid;
    gap: 6px;
    white-space: pre-wrap;
    min-width: 0;
}

.struct-detail-label {
    font-size: 12px;
    font-weight: 700;
    color: rgba(15, 23, 42, 0.62);
}

.struct-detail-item > span,
.struct-detail-header > strong,
.struct-field-subtitle,
.struct-description-current {
    min-width: 0;
    overflow-wrap: anywhere;
    word-break: break-word;
}

.struct-description {
    display: grid;
    gap: 8px;
    white-space: pre-wrap;
}

.struct-description-current {
    color: #0b7285;
}

.struct-value-descriptions {
    display: flex;
    flex-wrap: wrap;
    gap: 6px;
}

.struct-tag-description-current {
    color: #0b7285;
    font-weight: 500;
    border: 2px solid rgba(11, 114, 133, 0.12);
    padding: 3px 6px;
}

.struct-tag-description {
    color: rgba(15, 23, 42, 0.72);
    padding: 3px 6px;
}

@media (max-width: 960px) {
    .struct-split-layout {
        grid-template-columns: 1fr;
    }

    .struct-detail-pane {
        position: static;
        max-height: none;
        overflow: visible;
    }
}
</style>