function trimTrailingSlash(path: string): string {
    return path.length > 1 ? path.replace(/\/+$/g, '') : path;
}

export function normalizePath(path: string): string {
    const trimmed = (path || '/').trim().replace(/\\/g, '/');
    if (!trimmed || trimmed === '/') {
        return '/';
    }

    const compact = trimmed.replace(/\/{2,}/g, '/');
    return compact.startsWith('/') ? compact : `/${compact}`;
}

export function getParentPath(path: string): string {
    const normalizedPath = trimTrailingSlash(normalizePath(path));
    if (normalizedPath === '/') {
        return '/';
    }

    const separatorIndex = normalizedPath.lastIndexOf('/');
    return separatorIndex <= 0 ? '/' : normalizedPath.substring(0, separatorIndex);
}

export function getPathName(path: string): string {
    const normalizedPath = trimTrailingSlash(normalizePath(path));
    if (normalizedPath === '/') {
        return '/';
    }

    return normalizedPath.substring(normalizedPath.lastIndexOf('/') + 1);
}

export function getPathSegments(path: string): string[] {
    const normalizedPath = normalizePath(path);
    return normalizedPath === '/' ? [] : normalizedPath.split('/').filter(Boolean);
}

export function isDirectChild(parent: string, child: string): boolean {
    return getParentPath(child) === normalizePath(parent);
}

export function makeScopedPathKey(scope: string, path: string): string {
    return `${scope}::${normalizePath(path)}`;
}
