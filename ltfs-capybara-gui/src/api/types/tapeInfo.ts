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
    kBytesPerSet: number;
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

export interface PartitionInfo {
    index: number;
    wrapCount: number;
    allocatedSize: number;
    usedSize: number;
    estimatedLossSize: number;
}

export interface ApplicationSpecific {
    barCode: string;
    vendor: string;
    name: string;
    version: string;
}

export interface UsageInfo {
    drvSN: string;
    threadCount: number;
    lifeSetsWritten: number;
    lifeSetsRead: number;
    lifeWriteRetries: number;
    lifeReadRetries: number;
    lifeUnRecovWrites: number;
    lifeUnRecovReads: number;
    lifeSuspendedWrites: number;
    lifeSuspendedAppendWrites: number;
    lifeFatalSusWrites: number;
}

export interface TapeInfo {
    applicationSpecific: ApplicationSpecific;
    manufacturer: Manufacturer;
    mediaManufacturer: MediaManufacturer;
    usages: Record<string, UsageInfo>;
    eoDs: Record<string, EODs>;
    partitions: Record<string, PartitionInfo>;
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
