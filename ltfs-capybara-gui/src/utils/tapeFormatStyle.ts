type LtoVendorPalette = Record<number, string>;

export type LtoFormatStyleConfig = {
    defaultPalette: LtoVendorPalette;
    vendorPalettes: Record<string, LtoVendorPalette>;
    wormCornerColor: string;
};

export type LtoFormatStyle = {
    color?: string;
    isWorm: boolean;
    wormCornerColor: string;
};

export const defaultLtoFormatStyleConfig: LtoFormatStyleConfig = {
    defaultPalette: {
        1: '#000000',
        2: '#5b2c6f',
        3: '#6b7a8f',
        4: '#2e8b57',
        5: '#800020',
        6: '#000000',
        7: '#6f2da8',
        8: '#800020',
        9: '#008080',
        10: '#000000',
    },
    vendorPalettes: {
        HPE: {
            2: '#800020',
            3: '#ffd54f',
            4: '#90ee90',
            5: '#87ceeb',
            6: '#6f2da8',
        },
    },
    wormCornerColor: '#9aa0a6',
};

function normalizeVendor(vendor: string): string {
    return vendor.trim().toUpperCase();
}

function parseLtoGeneration(format: string): number | undefined {
    const match = format.match(/LTO[-\s]?(\d+)/i);
    if (!match) {
        return undefined;
    }

    const generation = Number.parseInt(match[1], 10);
    return Number.isNaN(generation) ? undefined : generation;
}

export function getLtoFormatStyle(
    format: string | null | undefined,
    vendor: string | null | undefined,
    config: LtoFormatStyleConfig = defaultLtoFormatStyleConfig,
): LtoFormatStyle {
    const normalizedFormat = format ?? '';
    const normalizedVendor = normalizeVendor(vendor ?? '');
    const generation = parseLtoGeneration(normalizedFormat);
    const vendorPalette = config.vendorPalettes[normalizedVendor];

    return {
        color:
            generation === undefined
                ? undefined
                : (vendorPalette?.[generation] ?? config.defaultPalette[generation]),
        isWorm: /WORM/i.test(normalizedFormat),
        wormCornerColor: config.wormCornerColor,
    };
}
