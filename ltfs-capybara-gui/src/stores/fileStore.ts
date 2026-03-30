import { defineStore } from 'pinia';
import type { LtfsTaskGroup } from '@/api/modules/tasks';

export const useFileStore = defineStore('file', {
    state: () => ({
        files: [] as any[],
        loading: false,
        currentTapeName: '',
        currentPath: '/',
        localIndexTreeData: [] as any[],
        localIndexExpandedKeys: [] as Array<string | number>,
        localIndexSelectedKeys: [] as Array<string | number>,
        noLtfsTapeName: '',
        noLtfsFilesystem: false,
        taskGroups: [] as LtfsTaskGroup[],
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
            this.taskGroups = groups;
        },

        upsertTaskGroup(group: LtfsTaskGroup) {
            const index = this.taskGroups.findIndex(
                g => g.tapeBarcode.toLowerCase() === group.tapeBarcode.toLowerCase(),
            );
            if (index >= 0) {
                this.taskGroups[index] = group;
                return;
            }

            this.taskGroups.push(group);
        },
    },
});
