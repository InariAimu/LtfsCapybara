const LIGHT_GREEN = '#18a058';
const LIGHT_YELLOW = '#f0ad00';
const LIGHT_RED = '#ff4332';

function parseHexColor(hex: string): [number, number, number] {
    const normalized = hex.replace('#', '');
    const value = Number.parseInt(normalized, 16);
    return [(value >> 16) & 255, (value >> 8) & 255, value & 255];
}

function toHex(value: number): string {
    return Math.round(value).toString(16).padStart(2, '0');
}

function interpolateColor(from: string, to: string, t: number): string {
    const clamped = Math.max(0, Math.min(1, t));
    const [r1, g1, b1] = parseHexColor(from);
    const [r2, g2, b2] = parseHexColor(to);

    const r = r1 + (r2 - r1) * clamped;
    const g = g1 + (g2 - g1) * clamped;
    const b = b1 + (b2 - b1) * clamped;

    return `#${toHex(r)}${toHex(g)}${toHex(b)}`;
}

export function parseCompactDate(value?: string | null): Date | null {
    if (!value || !/^\d{8}$/.test(value)) {
        return null;
    }

    const year = Number(value.slice(0, 4));
    const month = Number(value.slice(4, 6));
    const day = Number(value.slice(6, 8));

    const parsed = new Date(Date.UTC(year, month - 1, day));
    if (
        Number.isNaN(parsed.getTime()) ||
        parsed.getUTCFullYear() !== year ||
        parsed.getUTCMonth() !== month - 1 ||
        parsed.getUTCDate() !== day
    ) {
        return null;
    }

    return new Date(year, month - 1, day);
}

function getAgePartsFromNow(date: Date): { years: number; months: number } {
    const now = new Date();
    if (date.getTime() > now.getTime()) {
        return { years: 0, months: 0 };
    }

    let years = now.getFullYear() - date.getFullYear();
    let months = now.getMonth() - date.getMonth();
    if (now.getDate() < date.getDate()) {
        months -= 1;
    }
    if (months < 0) {
        years -= 1;
        months += 12;
    }

    return { years: Math.max(0, years), months: Math.max(0, months) };
}

export function formatRelativeAgeFromNow(value?: string | null): string {
    const date = parseCompactDate(value);
    if (!date) {
        return '';
    }

    const now = new Date();
    if (date.getTime() > now.getTime()) {
        return '';
    }

    const { years, months } = getAgePartsFromNow(date);
    const parts: string[] = [];
    if (years > 0) {
        parts.push(`${years} year${years === 1 ? '' : 's'}`);
    }
    if (months > 0) {
        parts.push(`${months} month${months === 1 ? '' : 's'}`);
    }

    if (!parts.length) {
        return 'less than 1 month ago';
    }

    return `${parts.join(' ')} ago`;
}

export function getRelativeAgeColor(value?: string | null): string {
    const date = parseCompactDate(value);
    if (!date) {
        return '';
    }

    const now = new Date();
    if (date.getTime() > now.getTime()) {
        return '';
    }

    const { years, months } = getAgePartsFromNow(date);
    const ageYears = years + months / 12;

    if (ageYears <= 10) {
        return interpolateColor(LIGHT_GREEN, LIGHT_YELLOW, ageYears / 10);
    }

    if (ageYears <= 20) {
        return interpolateColor(LIGHT_YELLOW, LIGHT_RED, (ageYears - 10) / 10);
    }

    return LIGHT_RED;
}
