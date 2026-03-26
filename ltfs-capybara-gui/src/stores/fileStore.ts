import { defineStore } from 'pinia';

export const useFileStore = defineStore('file', {
    state: () => ({
        files: [] as any[],
        loading: false,
        currentTapeName: '',
        currentPath: '/',
        localIndexTreeData: [] as any[],
        localIndexExpandedKeys: [] as Array<string | number>,
        localIndexSelectedKeys: [] as Array<string | number>,
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
    },
});
