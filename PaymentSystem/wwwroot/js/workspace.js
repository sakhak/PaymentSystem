(() => {
    const panel = document.querySelector('[data-live-workspace]');
    if (!panel) return;
    let active = false;
    let timeout;
    let stopped = false;
    const url = new URL(window.location.href);
    url.searchParams.set('handler', 'Live');
    async function refresh() {
        if (active || document.hidden || stopped) return;
        active = true;
        try {
            const response = await fetch(url, { credentials: 'same-origin', cache: 'no-store',
                headers: { Accept: 'application/json' }, signal: AbortSignal.timeout(10000) });
            if (response.redirected || response.status === 401 || response.status === 403) { stopped = true; return; }
            if (!response.ok) return;
            const data = await response.json();
            for (const key of ['total', 'pending', 'paid', 'expired']) {
                const element = document.querySelector(`[data-metric="${key}"]`);
                if (element && data[key] !== undefined) element.textContent = data[key];
            }
            for (const item of data.payments || []) {
                const element = document.querySelector(`[data-payment-status="${item.id}"]`);
                if (element) {
                    element.textContent = item.status;
                    element.className = `status-badge status-${item.status.toLowerCase()}`;
                }
            }
            document.querySelectorAll('[data-revenue]').forEach(element => {
                const amount = data.revenue?.[element.dataset.revenue];
                if (amount !== undefined) element.textContent = Number(amount).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
            });
            const updated = document.getElementById('workspace-updated');
            if (updated) updated.textContent = `Updated ${new Date().toLocaleTimeString()}`;
        } catch { /* Keep existing data visible and retry on the next interval. */ }
        finally {
            active = false;
            if (!stopped && !document.hidden) timeout = setTimeout(refresh, 5000);
        }
    }
    document.addEventListener('visibilitychange', () => {
        clearTimeout(timeout);
        if (!document.hidden) refresh();
    });
    window.addEventListener('pagehide', () => { stopped = true; clearTimeout(timeout); });
    refresh();
})();
