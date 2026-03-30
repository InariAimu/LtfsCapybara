const WRAP_TYPE_GUARD = 3;

const ERROR_CAPACITY = 50;
const FATAL_CAPACITY = 60;
const CRITICAL_CAPACITY = 90;
const WARNING_CAPACITY = 95;
const MAX_CAPACITY = 100;

const COLORS = {
    guard: '#9fc5ff',
    error: '#ff4d4d',
    fatal: '#ff8888',
    critical: '#ffa27d',
    warningStart: '#fff6cf',
    warningEnd: '#ffdfb9',
    healthyStart: '#fff2ad',
    healthyEnd: '#c3e8c3',
    overProvisioned: '#aeedae',
} as const;

const clamp = (value: number, min: number, max: number): number =>
    Math.min(max, Math.max(min, value));
const mix = (from: number, to: number, progress: number): number =>
    Math.round(from + (to - from) * progress);

const toRgb = (hex: string) => {
    const value = hex.replace('#', '');
    return {
        r: Number.parseInt(value.slice(0, 2), 16),
        g: Number.parseInt(value.slice(2, 4), 16),
        b: Number.parseInt(value.slice(4, 6), 16),
    };
};

const interpolate = (startHex: string, endHex: string, progress: number): string => {
    const start = toRgb(startHex);
    const end = toRgb(endHex);
    const p = clamp(progress, 0, 1);
    return `rgb(${mix(start.r, end.r, p)}, ${mix(start.g, end.g, p)}, ${mix(start.b, end.b, p)})`;
};

export function getCapacityCellBackground(capacity: unknown, type?: unknown): string | undefined {
    if (type === WRAP_TYPE_GUARD) {
        return COLORS.guard;
    }

    if (typeof capacity !== 'number' || Number.isNaN(capacity) || capacity <= 0) {
        return undefined;
    }

    if (capacity <= ERROR_CAPACITY) {
        return COLORS.error;
    }

    if (capacity <= FATAL_CAPACITY) {
        return COLORS.fatal;
    }

    if (capacity <= CRITICAL_CAPACITY) {
        return COLORS.critical;
    }

    if (capacity <= WARNING_CAPACITY) {
        return interpolate(
            COLORS.warningStart,
            COLORS.warningEnd,
            (capacity - CRITICAL_CAPACITY) / (WARNING_CAPACITY - CRITICAL_CAPACITY),
        );
    }

    if (capacity <= MAX_CAPACITY) {
        return interpolate(
            COLORS.healthyStart,
            COLORS.healthyEnd,
            (capacity - WARNING_CAPACITY) / (MAX_CAPACITY - WARNING_CAPACITY),
        );
    }

    return COLORS.overProvisioned;
}
