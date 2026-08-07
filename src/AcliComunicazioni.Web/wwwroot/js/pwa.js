(() => {
    let installPrompt;
    const installButtons = [...document.querySelectorAll('.pwa-install-button')];
    const connectionStatuses = [...document.querySelectorAll('[data-connection-status]')];
    const onlineRequiredButtons = [...document.querySelectorAll('[data-requires-online]')];
    const onlineRequiredForms = [...document.querySelectorAll('[data-requires-online-form]')];

    const offlineBanner = document.createElement('div');
    offlineBanner.className = 'offline-notice';
    offlineBanner.setAttribute('role', 'alert');
    offlineBanner.setAttribute('aria-live', 'assertive');
    offlineBanner.setAttribute('tabindex', '-1');
    offlineBanner.hidden = true;
    offlineBanner.innerHTML = `
        <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M2 8.8A15.5 15.5 0 0 1 12 5c3.8 0 7.3 1.4 10 3.8M5 12.5A10.7 10.7 0 0 1 12 10c2.7 0 5.2.9 7 2.5M8.5 16a5.5 5.5 0 0 1 7 0M12 20h.01M3 3l18 18"/></svg>
        <span><strong>Connessione assente</strong><small>Non è possibile salvare o modificare le percorrenze.</small></span>`;
    document.body.appendChild(offlineBanner);

    onlineRequiredButtons.forEach((button) => {
        button.dataset.initiallyDisabled = String(button.disabled);
    });

    const updateConnectionState = () => {
        const isOnline = navigator.onLine;
        document.body.classList.toggle('is-offline', !isOnline);
        offlineBanner.hidden = isOnline;

        connectionStatuses.forEach((status) => {
            status.classList.toggle('is-online', isOnline);
            status.classList.toggle('is-offline', !isOnline);
            const label = status.querySelector('[data-connection-label]');
            if (label) label.textContent = isOnline ? 'Online' : 'Offline';
        });

        onlineRequiredButtons.forEach((button) => {
            const initiallyDisabled = button.dataset.initiallyDisabled === 'true';
            button.disabled = !isOnline || initiallyDisabled;
            button.setAttribute('aria-disabled', String(button.disabled));
            if (!isOnline) {
                button.title = 'Connessione necessaria per completare l’operazione';
            } else {
                button.removeAttribute('title');
            }
        });
    };

    onlineRequiredForms.forEach((form) => {
        form.addEventListener('submit', (event) => {
            if (navigator.onLine) return;
            event.preventDefault();
            offlineBanner.hidden = false;
            offlineBanner.focus({ preventScroll: true });
        });
    });

    window.addEventListener('online', updateConnectionState);
    window.addEventListener('offline', updateConnectionState);
    updateConnectionState();

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
