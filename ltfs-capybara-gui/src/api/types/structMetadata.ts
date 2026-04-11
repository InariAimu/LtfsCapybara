export interface StructMetadataLocation {
    byteIndex: number;
    endByteIndex: number;
    byteLength: number;
    bitIndex?: number | null;
    bitLength?: number | null;
}

export interface StructMetadataValueDescription {
    value: string;
    description: string;
    isCurrent: boolean;
}

export interface StructMetadataListLayout {
    lengthSource: string;
    lengthEncoding: string;
    lengthFieldMemberName?: string | null;
    lengthByteIndex: number;
    lengthEndByteIndex: number;
    lengthByteLength: number;
    isLengthMSBFirst?: boolean | null;
    valueByteIndex: number;
    valueEndByteIndex: number;
    valueByteLength: number;
}

export interface StructMetadataField {
    memberName: string;
    displayName: string;
    dataType: string;
    encoding: string;
    value: unknown;
    formattedValue: string;
    rawBytes: number[];
    rawHex: string;
    description: string;
    matchedValueDescription?: string | null;
    valueDescriptions: StructMetadataValueDescription[];
    isReserved: boolean;
    location: StructMetadataLocation;
    listLayout?: StructMetadataListLayout | null;
}

export interface StructMetadataDocument {
    typeName: string;
    description: string;
    byteLength: number;
    rawBytes: number[];
    rawHex: string;
    fields: StructMetadataField[];
}
