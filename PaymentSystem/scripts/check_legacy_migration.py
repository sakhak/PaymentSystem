"""Back up the existing local database and verify migration on a separate copy.
Does not migrate or change the original database. Run from the solution directory.
"""
from contextlib import closing
from datetime import datetime, timezone
from pathlib import Path
import sqlite3
import subprocess
import tempfile

project = Path(__file__).resolve().parents[1]
source = project / 'payments.db'
backup_folder = project / 'backups'
backup_folder.mkdir(exist_ok=True)
backup = backup_folder / ('payments-before-accounts-' + datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ') + '.db')
with closing(sqlite3.connect(source.as_uri() + '?mode=ro', uri=True)) as original:
    columns = [row[1] for row in original.execute('PRAGMA table_info(Payments)')]
    if not columns:
        raise RuntimeError('The database has no Payments table. Check the configured database path.')
    selection = ','.join('"' + column + '"' for column in columns)
    before = original.execute(f'SELECT {selection} FROM Payments ORDER BY Id').fetchall()
    existing_tables = {row[0] for row in original.execute("SELECT name FROM sqlite_master WHERE type='table'")}
    preserved = {table: original.execute(f'SELECT * FROM [{table}] ORDER BY Id').fetchall()
                 for table in ('AspNetUsers', 'AspNetRoles', 'Products') if table in existing_tables}
    with closing(sqlite3.connect(backup)) as destination:
        original.backup(destination)

with tempfile.TemporaryDirectory(prefix='khqr-migrate-') as folder:
    test_database = Path(folder) / 'copy.db'
    with closing(sqlite3.connect(backup)) as snapshot, closing(sqlite3.connect(test_database)) as destination:
        snapshot.backup(destination)
    subprocess.run(['dotnet', 'ef', 'database', 'update', '--project', str(project / 'PaymentSystem.csproj'),
                    '--configuration', 'Release', '--no-build', '--connection', f'Data Source={test_database}'],
                   cwd=project.parent, check=True)
    with closing(sqlite3.connect(test_database)) as migrated:
        after = migrated.execute(f'SELECT {selection} FROM Payments ORDER BY Id').fetchall()
        assert before == after, 'Existing payment data changed!'
        assert migrated.execute('PRAGMA foreign_key_check').fetchall() == []
        for table, rows in preserved.items():
            assert migrated.execute(f'SELECT * FROM [{table}] ORDER BY Id').fetchall() == rows, f'{table} data changed!'
print(f'PASS: all {len(before)} existing payments preserved by migration on the copy.')
print(f'Backup: {backup}')
print('The original database is unchanged. Development startup applies the migration automatically.')
