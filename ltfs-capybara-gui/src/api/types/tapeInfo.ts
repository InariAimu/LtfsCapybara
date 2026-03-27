export interface TapePhysicInfo {
    nWraps: number;
    setsPerWrap: number;
    tapDirLength: number;
}

export interface Manufacturer {
    tapeVendor: string;
    cartridgeSN: string;
    cartridgeType: number;
    format: string;
    gen: number;
    mfgDate: string;
    tapeLength: number;
    mediaCode: number;
    particleType: number;
    isCleaningTape: boolean;
    tapePhysicInfo: TapePhysicInfo;
}

export interface MediaManufacturer {
    mfgDate: string;
    vendor: string;
}

export interface EODs {
    dataSet: number;
    wrapNumber: number;
    validity: number;
    physicalPosition: number;
}

export interface WrapInfo {
    index: number;
    startBlock: number;
    endBlock: number;
    fileMarkCount: number;
    set: number;
    type: number;
    capacity: number;
}

export interface TapeInfo {
    manufacturer: Manufacturer;
    mediaManufacturer: MediaManufacturer;
    eoDs: Record<string, EODs>;
    wraps: WrapInfo[];
}

export interface WrapTableRow {
    key: number;
    wrap: number;
    startBlock: number | string;
    endBlock: number | string;
    filemark: number | string;
    set: string;
    capacity: string;
    rawCapacity: number;
    rawType: number;
    backgroundColor?: string;
}
