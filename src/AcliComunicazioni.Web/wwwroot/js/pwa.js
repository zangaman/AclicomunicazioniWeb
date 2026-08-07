(() => {
    let installPrompt;
    const installButtons = [...document.querySelectorAll('.pwa-install-button')];

    const showInstallButtons = (show) => {
        installButtons.forEach((button) => {
            button.hidden = !show;
        });
    };

    window.addEventListener('beforeinstallprompt', (event) => {
        event.preventDefault();
        installPrompt = event;
        showInstallButtons(true);
    });

    installButtons.forEach((button) => {
        button.addEventListener('click', async () => {
            if (!installPrompt) return;
            installPrompt.prompt();
            await installPrompt.userChoice;
            installPrompt = undefined;
            showInstallButtons(false);
        });
    });

    window.addEventListener('appinstalled', () => {
        installPrompt = undefined;
        showInstallButtons(false);
    });

    if ('serviceWorker' in navigator) {
        window.addEventListener('load', () => {
            navigator.serviceWorker.register('/sw.js').catch(() => {
                // L'applicazione continua a funzionare normalmente senza PWA.
            });
        });
    }
})();
