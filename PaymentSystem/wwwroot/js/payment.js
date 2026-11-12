(() => {
    const root = document.getElementById('live-payment');
    if (!root) return;

    const badge = document.getElementById('payment-status');
    const message = document.getElementById('verification-message');
    const connection = document.getElementById('connection-status');
    const timer = document.getElementById('countdown');
    const form = document.getElementById('verify-form');
    const button = document.getElementById('verify-button');
    let paid = root.dataset.initialStatus === 'Paid';
    let active = false;
    let failures = 0;
    let timeout;
    let serverOffset = 0;
    let watching = !paid;

    const setHidden = (id, hidden) => {
        const element = document.getElementById(id);
        if (element) element.hidden = hidden;
    };
    const formatDate = value => value ? new Date(value).toLocaleString() : 'Not checked yet';

    function render(snapshot) {
        paid = snapshot.status === 'Paid';
        badge.textContent = snapshot.status;
        badge.className = `status-badge status-${snapshot.status.toLowerCase()}`;
        if (message.textContent !== snapshot.message) message.textContent = snapshot.message;
        document.getElementById('last-checked').textContent = formatDate(snapshot.lastCheckedAt);
        document.getElementById('confirmed-at').textContent = snapshot.paidAt ? formatDate(snapshot.paidAt) : 'Awaiting bank confirmation';
        document.getElementById('transaction-hash').textContent = snapshot.transactionHash || 'Available after verification';
        if (snapshot.serverTime) serverOffset = Date.parse(snapshot.serverTime) - Date.now();
        setHidden('qr-container', snapshot.status !== 'Pending');
        setHidden('expiry-panel', snapshot.status !== 'Pending');
        setHidden('payment-success', !paid);
        setHidden('payment-expired', snapshot.status !== 'Expired');
        setHidden('verify-form', paid);
        connection.textContent = paid ? 'Verified' : snapshot.autoChecking ? 'Live updates' : 'Manual check available';
        connection.classList.toggle('connection-error', !snapshot.autoChecking && !paid);
        if (paid || !snapshot.autoChecking) watching = false;
    }

    async function request(url, options = {}) {
        const response = await fetch(url, { credentials: 'same-origin', cache: 'no-store',
            ...options, headers: { Accept: 'application/json', ...options.headers }, signal: AbortSignal.timeout(22000) });
        if (response.status === 401 || response.status === 403 || response.redirected) {
            watching = false;
            throw new Error('Your session has ended. Sign in again to continue.');
        }
        if (!response.ok || !response.headers.get('content-type')?.includes('application/json'))
            throw new Error('Connection interrupted. Retrying automatically.');
        return response.json();
    }

    function schedule() {
        clearTimeout(timeout);
        if (watching && !paid && !document.hidden)
            timeout = setTimeout(poll, Math.min(30000, 3000 * 2 ** Math.min(failures, 3)));
    }

    async function poll() {
        if (active || paid || document.hidden) return;
        active = true;
        try { render(await request(root.dataset.statusUrl)); failures = 0; }
        catch (error) {
            failures++;
            connection.textContent = navigator.onLine ? 'Reconnecting' : 'Offline';
            connection.classList.add('connection-error');
            message.textContent = error.message;
        } finally { active = false; schedule(); }
    }

    form?.addEventListener('submit', async event => {
        event.preventDefault();
        if (active || paid) return;
        clearTimeout(timeout);
        active = true;
        button.disabled = true;
        button.textContent = 'Checking with Bakong...';
        try {
            render(await request(form.action, { method: 'POST', body: new FormData(form) }));
            failures = 0;
        } catch (error) { message.textContent = error.message; failures++; }
        finally {
            active = false;
            button.disabled = false;
            button.textContent = 'Check payment now';
            schedule();
        }
    });

    function updateCountdown() {
        if (!timer || paid) return;
        const seconds = Math.max(0, Math.ceil((Date.parse(timer.dataset.expires) - Date.now() - serverOffset) / 1000));
        timer.textContent = `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, '0')}`;
        if (!seconds) {
            badge.textContent = 'Expired';
            badge.className = 'status-badge status-expired';
            setHidden('qr-container', true);
            setHidden('expiry-panel', true);
            setHidden('payment-expired', false);
        }
    }
    const countdownInterval = setInterval(updateCountdown, 1000);
    document.addEventListener('visibilitychange', () => {
        clearTimeout(timeout);
        if (!document.hidden && watching) { updateCountdown(); poll(); }
    });
    window.addEventListener('online', () => { if (watching) poll(); });
    window.addEventListener('pagehide', () => { clearTimeout(timeout); clearInterval(countdownInterval); });
    updateCountdown();
    if (!paid) poll();
})();
