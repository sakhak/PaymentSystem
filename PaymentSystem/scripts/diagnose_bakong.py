"""Read-only local provider diagnostic. Never prints tokens, QR data, or account IDs."""
import json
import os
from pathlib import Path
import sqlite3
import sys
import urllib.error
import urllib.request
import urllib.parse
from contextlib import closing
from decimal import Decimal
from datetime import datetime, timezone

project = Path(__file__).resolve().parents[1]
config = json.loads((project / 'appsettings.json').read_text(encoding='utf-8-sig'))
secret_path = Path(os.environ.get('APPDATA', str(Path.home() / 'AppData/Roaming'))) / 'Microsoft/UserSecrets/76029e8f-6163-40d4-a8c6-e4b906804d9a/secrets.json'
secrets = json.loads(secret_path.read_text(encoding='utf-8-sig')) if secret_path.exists() else {}

def setting(section, key):
    return os.environ.get(section + '__' + key) or secrets.get(section + ':' + key) or config.get(section, {}).get(key)

order = sys.argv[1] if len(sys.argv) > 1 else ''
with closing(sqlite3.connect(project / 'payments.db')) as db:
    db.row_factory = sqlite3.Row
    payment = db.execute('SELECT * FROM Payments WHERE OrderId=?', (order,)).fetchone()
    if payment is None:
        raise SystemExit('Order not found.')
    token = setting('Bakong', 'Token')
    base = setting('Bakong', 'BaseUrl')
    if '--production' in sys.argv:
        base = 'https://api-bakong.nbc.gov.kh'
    if '--production-documented' in sys.argv:
        base = 'https://api-bakong.nbc.org.kh'
    print('Payment ID:', payment['Id'], '| Stored status:', payment['Status'])
    print('Provider host:', urllib.parse.urlparse(base).hostname)
    print('Token configured:', bool(token), '| Receiver snapshot available:', bool(payment['ReceiverAccountId']))
    if not token or not payment['Md5']:
        raise SystemExit('Provider check cannot run: token or MD5 is missing.')
    request = urllib.request.Request(base.rstrip('/') + '/v1/check_transaction_by_md5',
        data=json.dumps({'md5': payment['Md5']}).encode(),
        headers={'Authorization': 'Bearer ' + token, 'Content-Type': 'application/json'})
    try:
        with urllib.request.urlopen(request, timeout=18) as response:
            result = json.load(response)
            print('Provider HTTP status:', response.status)
    except urllib.error.HTTPError as error:
        print('Provider HTTP status:', error.code)
        raise SystemExit(2)
    except urllib.error.URLError as error:
        print('Provider network connection failed:', type(error.reason).__name__)
        raise SystemExit(3)
    for key in ('responseCode', 'errorCode'):
        value = result.get(key)
        print(key + ':', value if isinstance(value, (int, type(None))) else 'non-numeric')
    data = result.get('data')
    if not isinstance(data, dict):
        raise SystemExit('No transaction data returned. Payment was not changed.')
    print('Amount matches:', Decimal(str(data.get('amount', 0))) == Decimal(str(payment['Amount'])))
    print('Currency matches:', data.get('currency') == payment['Currency'])
    print('Receiver matches:', data.get('toAccountId') == payment['ReceiverAccountId'])
    stamp = data.get('createDateMs')
    if isinstance(stamp, (float, int)):
        print('Provider transaction time UTC:', datetime.fromtimestamp(stamp / 1000, timezone.utc).isoformat())
    print('Stored payment window UTC:', payment['CreatedAt'], 'to', payment['ExpiresAt'])
    print('Read-only diagnostic; payment was not changed.')
