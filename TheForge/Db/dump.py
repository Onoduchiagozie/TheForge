import sqlite3
import sys

# Usage: python dump_schema.py path\to\battlescenes.db
db_path = sys.argv[1] if len(sys.argv) > 1 else "battle_scenes.db"

conn = sqlite3.connect(db_path)
cur = conn.cursor()

tables = [row[0] for row in cur.execute(
    "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name;"
)]

print(f"Tables found: {tables}\n")

for table in tables:
    print(f"=== {table} ===")
    for cid, name, coltype, notnull, dflt, pk in cur.execute(f"PRAGMA table_info('{table}')"):
        print(f"  {name:<20} {coltype:<12} {'PK' if pk else ''} {'NOT NULL' if notnull else ''}")
    row_count = cur.execute(f"SELECT COUNT(*) FROM '{table}'").fetchone()[0]
    print(f"  ({row_count} rows)\n")

conn.close()