import { defineStore } from 'pinia';
import type { LtfsTaskGroup } from '@/api/modules/tasks';

export const useFileStore = defineStore('file', {
    state: () => ({
        files: [] as any[],
        loading: false,
        currentTapeName: '',
        currentTapeDriveId: '',
        currentPath: '/',
        localIndexTreeData: [] as any[],
        localIndexExpandedKeys: [] as Array<string | number>,
        localIndexSelectedKeys: [] as Array<string | number>,
        noLtfsTapeName: '',
        noLtfsFilesystem: false,
        taskGroups: [] as LtfsTaskGroup[],
        localTapeListRevision: 0,
    }),

    actions: {
        setFiles(files: any[]) {
            this.files = files;
        },

        setLoading(v: boolean) {
            this.loading = v;
        },

        setCurrentTapeName(name: string) {
            this.currentTapeName = name;
        },

        setCurrentPath(path: string) {
            this.currentPath = path || '/';
        },

        setCurrentTapeDriveId(tapeDriveId: string) {
            this.currentTapeDriveId = tapeDriveId;
        },

        setCurrentLocation(tapeName: string, path: string) {
            this.currentTapeName = tapeName;
            this.currentPath = path || '/';
        },

        setLocalIndexTreeData(nodes: any[]) {
            this.localIndexTreeData = nodes;
        },

        setLocalIndexExpandedKeys(keys: Array<string | number>) {
            this.localIndexExpandedKeys = keys;
        },

        setLocalIndexSelectedKeys(keys: Array<string | number>) {
            this.localIndexSelectedKeys = keys;
        },

        setNoLtfsState(tapeName: string, noLtfsFilesystem: boolean) {
            this.noLtfsTapeName = tapeName;
            this.noLtfsFilesystem = noLtfsFilesystem;
        },

        setTaskGroups(groups: LtfsTaskGroup[]) {
            this.taskGroups = groups.filter(group => (group.tasks?.length ?? 0) > 0);
        },

        upsertTaskGroup(group: LtfsTaskGroup) {
            const index = this.taskGroups.findIndex(
                g => g.tapeBarcode.toLowerCase() === group.tapeBarcode.toLowerCase(),
            );

            if ((group.tasks?.length ?? 0) === 0) {
                if (index >= 0) {
                    this.taskGroups.splice(index, 1);
                }
                return;
            }

            if (index >= 0) {
                this.taskGroups[index] = group;
                return;
            }

            this.taskGroups.push(group);
        },

        bumpLocalTapeListRevision() {
            this.localTapeListRevision += 1;
        },
    },
});
