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
    // color-scheme only accepts 'light', 'dark', or 'auto' — custom
    // theme names like 'mpdark' are invalid and break WebView2 rendering.
    const scheme = theme === 'light' ? 'light' : 'dark';
    document.documentElement.style.colorScheme = scheme;
}

export function isSystemDark() {
    return window.matchMedia('(prefers-color-scheme: dark)').matches;
}