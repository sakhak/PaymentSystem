"""Black-box integration checks. Uses a temporary SQLite DB and a local fake Bakong server.
Run from the solution folder: python PaymentSystem/scripts/smoke_test.py
No real payment or upstream network call is made. Requires a Release build first.
"""
import html
from contextlib import closing
import http.cookiejar
import json
import os
from pathlib import Path
import re
import socket
import sqlite3
import subprocess
import tempfile
import threading
import time
import urllib.error
import urllib.parse
import urllib.request
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

PROJECT = Path(__file__).resolve().parents[1]
FIXTURES = {}
REQUESTS = {}
CHECKS = 0
TEST_DATABASE = None


def check(condition, message):
    global CHECKS
    if not condition:
        raise AssertionError(message)
    CHECKS += 1
    print(f"PASS {message}", flush=True)


class FakeBakong(BaseHTTPRequestHandler):
    def do_POST(self):
        if self.headers.get('Transfer-Encoding', '').lower() == 'chunked':
            chunks = []
            while True:
                length = int(self.rfile.readline().strip().split(b';')[0], 16)
                if length == 0:
                    self.rfile.readline()
                    break
                chunks.append(self.rfile.read(length))
                self.rfile.read(2)
            raw = b''.join(chunks)
        else:
            raw = self.rfile.read(int(self.headers['Content-Length']))
        body = json.loads(raw)
        REQUESTS[body['md5']] = REQUESTS.get(body['md5'], 0) + 1
        response = FIXTURES.get(body['md5'], {'responseCode': 1, 'errorCode': 1, 'data': None})
        if isinstance(response, int):
            self.send_response(response)
            self.end_headers()
            return
        self.send_response(200)
        self.send_header('Content-Type', 'application/json')
        self.end_headers()
        self.wfile.write(json.dumps(response).encode())

    def log_message(self, *_):
        pass


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *_):
        return None


class Browser:
    def __init__(self, base):
        self.base = base
        self.jar = http.cookiejar.CookieJar()
        self.opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(self.jar), NoRedirect())

    def request(self, path, data=None, headers=None, method=None):
        if isinstance(data, dict):
            data = urllib.parse.urlencode(data).encode()
        request = urllib.request.Request(self.base + path, data=data, headers=headers or {}, method=method)
        try:
            response = self.opener.open(request, timeout=20)
        except urllib.error.HTTPError as error:
            response = error
        with response:
            return response.status, response.headers, response.read().decode('utf-8')

    def form(self, path, fields, post_path=None):
        status, _, body = self.request(path)
        check(status == 200, f"form available: {path}")
        token = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', body)
        if not token:
            raise AssertionError('Missing antiforgery field: ' + path)
        fields = dict(fields, __RequestVerificationToken=html.unescape(token.group(1)))
        return self.request(post_path or path, fields)

    def api(self, path, payload):
        if path == '/api/payments/check' and TEST_DATABASE is not None:
            # Independent verification fixtures advance the retry window. The
            # separate realtime suite tests throttling without resetting it.
            with closing(sqlite3.connect(TEST_DATABASE)) as db:
                db.execute('UPDATE Payments SET NextVerificationAt=NULL WHERE Md5=?', (payload.get('md5'),))
                db.commit()
        status, _, body = self.request('/api/payments/csrf')
        check(status == 200, 'authenticated CSRF endpoint')
        token = json.loads(body)['token']
        return self.request(path, json.dumps(payload).encode(),
                            {'Content-Type': 'application/json', 'X-CSRF-TOKEN': token})


def credentials(name, email):
    return {'Input.DisplayName': name, 'Input.Email': email,
            'Input.Password': 'Smoke-Only!12345', 'Input.ConfirmPassword': 'Smoke-Only!12345'}


def main():
    global TEST_DATABASE
    mock = ThreadingHTTPServer(('127.0.0.1', 0), FakeBakong)
    threading.Thread(target=mock.serve_forever, daemon=True).start()
    with socket.socket() as listener:
        listener.bind(('127.0.0.1', 0))
        port = listener.getsockname()[1]
    base = f'http://127.0.0.1:{port}'
    with tempfile.TemporaryDirectory(prefix='khqr-smoke-') as temporary:
        database = Path(temporary) / 'smoke.db'
        TEST_DATABASE = database
        env = dict(os.environ, ASPNETCORE_ENVIRONMENT='Development', ASPNETCORE_URLS=base,
                   ConnectionStrings__DefaultConnection=f'Data Source={database}',
                   Khqr__BakongAccountId='smoke@devb', Khqr__MerchantName='Smoke Test',
                   Bakong__BaseUrl=f'http://127.0.0.1:{mock.server_port}', Bakong__Token='fake-local-only')
        env['PaymentVerification__Enabled'] = 'false'
        logfile = open(Path(temporary) / 'server.log', 'w', encoding='utf-8')
        process = subprocess.Popen(['dotnet', str(PROJECT / 'bin/Release/net10.0/PaymentSystem.dll')],
                                   cwd=PROJECT, env=env, stdout=logfile, stderr=logfile,
                                   creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
        try:
            admin = Browser(base)
            deadline = time.monotonic() + 40
            while True:
                try:
                    if admin.request('/Account/Setup')[0] == 200:
                        break
                except (urllib.error.URLError, ConnectionError):
                    pass
                if process.poll() is not None or time.monotonic() > deadline:
                    raise AssertionError('Server startup failed')
                time.sleep(.2)
            check(admin.request('/')[0] == 302, 'dashboard requires login')
            check(admin.request('/api/payments')[0] == 401, 'anonymous API returns 401')
            check(admin.form('/Account/Setup', credentials('Administrator', 'admin@example.test'))[0] == 302, 'first local administrator created')
            check(admin.request('/Admin')[0] == 200, 'administrator can access admin area')
            check(admin.request('/css/site.css')[0] == 200, 'local CSS served')
            check(admin.request('/lib/bootstrap/bootstrap.min.css')[0] == 200, 'Bootstrap served locally without CDN')
            check(admin.request('/Account/Setup')[0] == 302, 'setup closed after first account')
            product = {'Input.Name': 'Integration product', 'Input.Description': 'Created in an isolated test database',
                       'Input.Price': '1.25', 'Input.Currency': 'USD', 'Input.IsActive': 'true'}
            check(admin.form('/Admin/Products/Edit', product)[0] == 302, 'admin can create a product')
            with closing(sqlite3.connect(database)) as db:
                product_id = db.execute('SELECT Id FROM Products').fetchone()[0]
            for path in ['/', '/Products', '/Payments', '/Admin/Products', '/Admin/Users', '/Account/Manage']:
                check(admin.request(path)[0] == 200, 'page renders: ' + path)
            buyer = Browser(base)
            check(buyer.form('/Account/Register', credentials('Buyer', 'buyer@example.test'))[0] == 302, 'user registration succeeds')
            check(buyer.request('/Admin')[0] in (302, 403), 'regular user cannot access admin area')
            check(buyer.request('/Admin/Products/Edit')[0] in (302, 403), 'regular user cannot edit products')
            path = f'/Payments/Create?productId={product_id}'
            status, headers, _ = buyer.form(path, {'Input.ProductId': product_id, 'Input.Quantity': 2,
                'Input.OrderId': 'PRODUCT-SMOKE', 'Input.Amount': '.01', 'Input.Currency': 'KHR'})
            check(status == 302, 'product checkout creates payment')
            details_path = headers['Location']
            status, _, body = buyer.request(details_path)
            check(status == 200 and 'data:image/png;base64,' in body, 'checkout displays scannable QR PNG')
            with closing(sqlite3.connect(database)) as db:
                row = db.execute("SELECT Id, Amount, Currency, Md5 FROM Payments WHERE OrderId='PRODUCT-SMOKE'").fetchone()
            payment_id, amount, currency, md5 = row
            check(float(amount) == 2.5 and currency == 'USD', 'server ignores tampered product price and currency')
            other = Browser(base)
            check(other.form('/Account/Register', credentials('Other user', 'other@example.test'))[0] == 302, 'second user registered')
            check(other.request(details_path)[0] == 404, 'other user cannot read payment page')
            check(other.request(f'/api/payments/{payment_id}')[0] == 404, 'other user cannot read payment API')
            status, _, body = other.request('/api/payments')
            check(status == 200 and json.loads(body) == [], 'payment list scoped to current user')
            check(buyer.request('/api/payments/khqr', json.dumps({'orderId': 'NO-CSRF', 'amount': 1, 'currency': 'USD'}).encode(), {'Content-Type': 'application/json'})[0] == 400, 'payment API rejects missing CSRF token')
            check(buyer.api('/api/payments/khqr', {'orderId': 'PRODUCT-SMOKE', 'amount': 1, 'currency': 'USD'})[0] == 409, 'duplicate order rejected')
            check(buyer.api('/api/payments/khqr', {'orderId': 'BAD-KHR', 'amount': 1.5, 'currency': 'KHR'})[0] == 409, 'fractional KHR rejected')
            fixture = {'responseCode': 0, 'data': {'hash': 'a' * 64, 'toAccountId': 'wrong@devb', 'currency': 'USD', 'amount': 2.5, 'createDateMs': int(time.time() * 1000)}}
            FIXTURES[md5] = 403
            status, _, body = buyer.api('/api/payments/check', {'md5': md5})
            check(status == 200 and json.loads(body)['status'] == 'Pending', 'upstream authorization failure does not confirm payment')
            FIXTURES[md5] = {'responseCode': 'unexpected', 'data': []}
            status, _, body = buyer.api('/api/payments/check', {'md5': md5})
            check(status == 200 and json.loads(body)['status'] == 'Pending', 'malformed provider response fails closed')
            FIXTURES[md5] = fixture
            buyer.api('/api/payments/check', {'md5': md5})
            check(json.loads(buyer.request(f'/api/payments/{payment_id}')[2])['status'] == 'Pending', 'wrong receiving account cannot confirm payment')
            fixture['data']['toAccountId'] = 'smoke@devb'
            fixture['data']['amount'] = 1
            buyer.api('/api/payments/check', {'md5': md5})
            check(json.loads(buyer.request(f'/api/payments/{payment_id}')[2])['status'] == 'Pending', 'wrong amount cannot confirm payment')
            fixture['data']['amount'] = 2.5
            fixture['data']['currency'] = 'KHR'
            buyer.api('/api/payments/check', {'md5': md5})
            check(json.loads(buyer.request(f'/api/payments/{payment_id}')[2])['status'] == 'Pending', 'wrong currency cannot confirm payment')
            fixture['data']['currency'] = 'USD'
            fixture['data']['createDateMs'] = 1
            buyer.api('/api/payments/check', {'md5': md5})
            check(json.loads(buyer.request(f'/api/payments/{payment_id}')[2])['status'] == 'Pending', 'out-of-window transaction cannot confirm payment')
            fixture['data']['createDateMs'] = int(time.time() * 1000)
            buyer.api('/api/payments/check', {'md5': md5})
            check(json.loads(buyer.request(f'/api/payments/{payment_id}')[2])['status'] == 'Paid', 'matching mocked Bakong transaction confirms payment')
            check('already' in json.loads(buyer.api('/api/payments/check', {'md5': md5})[2])['message'], 'confirmation is idempotent')
            status, _, body = buyer.api('/api/payments/khqr', {'orderId': 'REPLAY-HASH', 'amount': 2.5, 'currency': 'USD'})
            check(status == 201, 'custom payment created')
            second = json.loads(body)
            FIXTURES[second['md5']] = fixture
            buyer.api('/api/payments/check', {'md5': second['md5']})
            check(json.loads(buyer.request(f"/api/payments/{second['id']}")[2])['status'] == 'Pending', 'transaction hash cannot confirm two payments')
            # Change only the isolated test DB to exercise expiry behavior.
            with closing(sqlite3.connect(database)) as db:
                db.execute("UPDATE Payments SET ExpiresAt='2000-01-01 00:00:00' WHERE Id=?", (second['id'],))
                db.commit()
            status, _, body = buyer.request(f"/Payments/Details/{second['id']}")
            check(status == 200 and 'Expired' in body and 'data:image/png' not in body, 'expired QR is hidden')
            check(admin.form('/Admin/Products', {}, f'/Admin/Products?handler=Toggle&id={product_id}')[0] == 302, 'admin archives product')
            check('Integration product' not in buyer.request('/Products')[2], 'archived product hidden from catalog')
            check(buyer.request(path)[0] == 404, 'archived product cannot be checked out')
            with closing(sqlite3.connect(database)) as db:
                other_id = db.execute("SELECT Id FROM AspNetUsers WHERE Email='other@example.test'").fetchone()[0]
            check(admin.form('/Admin/Users', {'id': other_id, 'role': 'User'})[0] == 302, 'admin can deactivate user')
            check(other.request('/')[0] == 302, 'deactivation revokes existing session')
            check(admin.form('/Admin/Users', {'id': other_id, 'role': 'Admin', 'isActive': 'true'})[0] == 302, 'admin can promote a user')
            login_other = {'Input.Email': 'other@example.test', 'Input.Password': 'Smoke-Only!12345'}
            check(other.form('/Account/Login', login_other)[0] == 302, 'promoted user can log in')
            check(other.request('/Admin')[0] == 200, 'promoted role grants admin access')
            check(admin.form('/Admin/Users', {'id': other_id, 'role': 'User', 'isActive': 'true'})[0] == 302, 'admin can demote a user')
            check(other.request('/Admin')[0] in (302, 403), 'role change invalidates old admin session')
            for _ in range(5):
                other.form('/Account/Login', dict(login_other, **{'Input.Password': 'Wrong-Password!12345'}))
            check(other.form('/Account/Login', login_other)[0] == 200, 'five failed attempts lock the account')
            check(other.request('/')[0] == 302, 'locked account remains signed out')
            check(buyer.form('/Account/Manage', {'Input.DisplayName': 'Updated buyer'})[0] == 302, 'user can update profile')
            check(buyer.form('/Account/Manage', {'Input.DisplayName': 'Updated buyer',
                'Input.CurrentPassword': 'Smoke-Only!12345', 'Input.NewPassword': 'Changed-Only!12345',
                'Input.ConfirmPassword': 'Changed-Only!12345'})[0] == 302, 'user can change password')
            check(buyer.form('/Account/Manage', {}, '/Account/Logout')[0] == 302, 'logout succeeds with CSRF')
            check(buyer.request('/')[0] == 302, 'logout removes access')
            login = {'Input.Email': 'buyer@example.test', 'Input.Password': 'Changed-Only!12345'}
            check(buyer.form('/Account/Login?ReturnUrl=https://example.com', login)[1]['Location'] == '/', 'login rejects external return URL')
            check('Confirmed totals' in admin.request('/')[2], 'dashboard renders verified totals')
            print(f'\n{CHECKS} integration checks passed. Temporary data only; Bakong was mocked.', flush=True)
        except Exception:
            logfile.flush()
            print((Path(temporary) / 'server.log').read_text(encoding='utf-8')[-14000:])
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
