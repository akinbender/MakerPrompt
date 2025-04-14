export function watchSystemTheme(dotNetHelper) {
    const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');

    const handler = (e) => {
        dotNetHelper.invokeMethodAsync('HandleSystemThemeChange', e.matches);
    };

    mediaQuery.addEventListener('change', handler);
    return {
        dispose: () => mediaQuery.removeEventListener('change', handler)
    };
}

export function setTheme(theme) {
    document.documentElement.setAttribute('data-bs-theme', theme);
    document.documentElement.style.colorScheme = theme;
}

export function isSystemDark() {
    return window.matchMedia('(prefers-color-scheme: dark)').matches;
}