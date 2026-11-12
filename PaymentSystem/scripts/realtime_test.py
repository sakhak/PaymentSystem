"""Exercise automatic reconciliation and live status endpoints with a fake bank.
Run after a Release build: python PaymentSystem/scripts/realtime_test.py
"""
from contextlib import closing
from pathlib import Path
import json
import os
import socket
import sqlite3
import subprocess
import tempfile
import threading
import time
import urllib.error
from http.server import ThreadingHTTPServer
import smoke_test as smoke


def wait_until(action, condition, seconds=12):
    deadline = time.monotonic() + seconds
    while time.monotonic() < deadline:
        result = action()
        if condition(result):
            return result
        time.sleep(.25)
    raise AssertionError('Timed out waiting for automatic payment reconciliation')


def main():
    project = Path(__file__).resolve().parents[1]
    mock = ThreadingHTTPServer(('127.0.0.1', 0), smoke.FakeBakong)
    threading.Thread(target=mock.serve_forever, daemon=True).start()
    with socket.socket() as sock:
        sock.bind(('127.0.0.1', 0))
        port = sock.getsockname()[1]
    base = f'http://127.0.0.1:{port}'
    with tempfile.TemporaryDirectory(prefix='khqr-live-') as folder:
        database = Path(folder) / 'live.db'
        env = dict(os.environ, ASPNETCORE_ENVIRONMENT='Development', ASPNETCORE_URLS=base,
            ConnectionStrings__DefaultConnection=f'Data Source={database}',
            Khqr__BakongAccountId='live-test@devb', Khqr__MerchantName='Live Test',
            Bakong__BaseUrl=f'http://127.0.0.1:{mock.server_port}', Bakong__Token='fake-only',
            PaymentVerification__Enabled='true', PaymentVerification__IntervalSeconds='1')
        logfile = open(Path(folder) / 'server.log', 'w', encoding='utf-8')
        process = subprocess.Popen(['dotnet', str(project / 'bin/Release/net10.0/PaymentSystem.dll')],
            cwd=project, env=env, stdout=logfile, stderr=logfile,
            creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
        try:
            admin = smoke.Browser(base)
            def ready():
                try:
                    return admin.request('/Account/Setup')[0]
                except (urllib.error.URLError, ConnectionError):
                    return 0
            wait_until(ready, lambda status: status == 200, 30)
            smoke.check(admin.form('/Account/Setup', smoke.credentials('Live admin', 'live@example.test'))[0] == 302, 'realtime test administrator created')
            status, _, body = admin.api('/api/payments/khqr', {'orderId': 'AUTOMATIC-ORDER', 'amount': 3.75, 'currency': 'USD'})
            smoke.check(status == 201, 'automatic verification test payment created')
            payment = json.loads(body)
            path = f"/Payments/Details/{payment['id']}?handler=Status"
            def snapshot():
                status, headers, body = admin.request(path)
                smoke.check(status == 200 and 'no-store' in headers.get('Cache-Control', ''), 'live status is authorized and not cached')
                return json.loads(body)
            initial = wait_until(snapshot, lambda data: data['verificationCode'] == 'NotFound')
            smoke.check(initial['autoChecking'] and initial['lastCheckedAt'], 'worker checks without a manual verification request')
            smoke.FIXTURES[payment['md5']] = {'responseCode': 0, 'data': {
                'hash': 'b' * 64, 'toAccountId': 'live-test@devb', 'currency': 'USD',
                'amount': 3.75, 'createDateMs': int(time.time() * 1000)}}
            confirmed = wait_until(snapshot, lambda data: data['status'] == 'Paid')
            smoke.check(confirmed['verificationCode'] == 'Verified' and confirmed['paidAt'] and confirmed['transactionHash'] == 'b' * 64,
                        'worker confirms and live endpoint exposes payment automatically')
            with closing(sqlite3.connect(database)) as db:
                smoke.check(db.execute('SELECT Status FROM Payments WHERE Id=?', (payment['id'],)).fetchone()[0] == 'Paid', 'automatic confirmation persisted to SQLite')
            live = json.loads(admin.request('/?handler=Live')[2])
            smoke.check(live['paid'] == 1 and live['revenue']['USD'] == 3.75, 'live dashboard counts and collections reflect confirmation')
            history = json.loads(admin.request('/Payments?handler=Live')[2])
            smoke.check(history['payments'][0]['status'] == 'Paid', 'live payment history reflects confirmation')
            calls = smoke.REQUESTS.get(payment['md5'], 0)
            time.sleep(3)
            smoke.check(smoke.REQUESTS.get(payment['md5'], 0) == calls, 'paid orders stop automatic provider calls')
            other = smoke.Browser(base)
            other.form('/Account/Register', smoke.credentials('Other user', 'other-live@example.test'))
            smoke.check(other.request(path)[0] == 404, 'live status endpoint enforces ownership')
            smoke.check(smoke.Browser(base).request(path)[0] in (302, 401), 'anonymous visitor cannot read live payment status')
            # A second payment exercises provider failures, UI diagnostics, and the lease.
            status, _, body = admin.api('/api/payments/khqr', {'orderId': 'PROVIDER-FAILURE', 'amount': 5, 'currency': 'USD'})
            second = json.loads(body)
            smoke.FIXTURES[second['md5']] = 403
            second_path = f"/Payments/Details/{second['id']}?handler=Status"
            failure = wait_until(lambda: json.loads(admin.request(second_path)[2]), lambda data: data['verificationCode'] == 'CredentialRejected')
            smoke.check(failure['status'] == 'Pending' and 'credential' in failure['message'], 'credential rejection is visible without marking paid')
            calls = smoke.REQUESTS.get(second['md5'], 0)
            for _ in range(3):
                admin.api('/api/payments/check', {'md5': second['md5']})
            smoke.check(smoke.REQUESTS.get(second['md5'], 0) == calls, 'repeated checks respect the persisted retry window')
            page = admin.request(f"/Payments/Details/{second['id']}")[2]
            smoke.check('data-status-url=' in page and 'id="verification-message"' in page, 'payment page includes live verification controls')
            smoke.check('sidebar-nav' in page and 'KHQR CHECKOUT' in page, 'business layout and checkout render')
            smoke.check('ProviderUnavailable' not in page and 'fake-only' not in page, 'page does not expose internal credentials or raw error codes')
            print(f'\n{smoke.CHECKS} realtime integration checks passed. Provider was mocked.', flush=True)
        except Exception:
            logfile.flush()
            print((Path(folder) / 'server.log').read_text(encoding='utf-8')[-12000:])
            raise
        finally:
            process.terminate()
            try:
                process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait()
            logfile.close()
            mock.shutdown()


if __name__ == '__main__':
    main()
