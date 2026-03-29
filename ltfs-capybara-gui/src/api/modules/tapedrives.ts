import { apiClient } from '../client';

export interface TapeDriveDto {
    id: string;
    devicePath: string;
    displayName: string;
    isFake: boolean;
}

export const tapeDriveApi = {
    list() {
        return apiClient.get<TapeDriveDto[]>('/tapedrives');
    },
};
