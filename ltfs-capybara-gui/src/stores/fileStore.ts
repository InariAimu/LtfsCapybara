import { defineStore } from 'pinia';

export const useFileStore = defineStore('file', {
    state: () => ({
        files: [] as any[],
        loading: false,
        currentPath: '/',
    }),

    actions: {
        setFiles(files: any[]) {
            this.files = files;
        },

        setLoading(v: boolean) {
            this.loading = v;
        },
    },
});
