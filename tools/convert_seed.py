import re
import os
import sys

# Paths are derived from this file's own location, so the repo can live on any
# drive under any folder name. tools/convert_seed.py -> <repo>/tools/convert_seed.py
REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DB_DIR = os.path.join(REPO, 'STAR-DOM-Web', 'Database')

seed_path = os.path.join(DB_DIR, 'seed.sql')
artshop_path = os.path.join(DB_DIR, 'seed_products_artshop.sql')

for _p in (seed_path, artshop_path):
    if not os.path.isfile(_p):
        sys.exit(
            "Missing input: %s\n"
            "This is the one-time MySQL -> PostgreSQL seed converter. Its MySQL\n"
            "inputs are not part of the repo, so it cannot run from a fresh clone.\n"
            "The result it produced is already committed as\n"
            "STAR-DOM-Web/Database/supabase_seed.sql -- edit that instead.\n" % _p
        )

with open(seed_path, 'r', encoding='utf-8') as f:
    seed_sql = f.read()

with open(artshop_path, 'r', encoding='utf-8') as f:
    artshop_sql = f.read()

def clean_sql(sql):
    sql = re.sub(r'USE\s+\w+;', '', sql, flags=re.IGNORECASE)
    sql = re.sub(r'SET\s+FOREIGN_KEY_CHECKS\s*=\s*[01];', '', sql, flags=re.IGNORECASE)
    sql = re.sub(r'ON\s+DUPLICATE\s+KEY\s+UPDATE\s+Email\s*=\s*VALUES\(Email\);', 'ON CONFLICT (Id) DO UPDATE SET Email = EXCLUDED.Email;', sql, flags=re.IGNORECASE)
    sql = re.sub(r'ON\s+DUPLICATE\s+KEY\s+UPDATE\s+Name\s*=\s*VALUES\(Name\);', 'ON CONFLICT (Id) DO UPDATE SET Name = EXCLUDED.Name;', sql, flags=re.IGNORECASE)
    sql = re.sub(r'ON\s+DUPLICATE\s+KEY\s+UPDATE\s+CommissionNumber\s*=\s*VALUES\(CommissionNumber\);', 'ON CONFLICT (Id) DO UPDATE SET CommissionNumber = EXCLUDED.CommissionNumber;', sql, flags=re.IGNORECASE)
    sql = re.sub(r'ON\s+DUPLICATE\s+KEY\s+UPDATE\s+OrderNumber\s*=\s*VALUES\(OrderNumber\);', 'ON CONFLICT (Id) DO UPDATE SET OrderNumber = EXCLUDED.OrderNumber;', sql, flags=re.IGNORECASE)
    sql = re.sub(r'TRUNCATE\s+TABLE\s+(\w+);', r'TRUNCATE TABLE \1 CASCADE;', sql, flags=re.IGNORECASE)
    return sql

roles_and_users_sql = """-- ---------- Roles ----------
INSERT INTO Roles (Id, Name, Description) VALUES
  (1, 'CUSTOMER', 'Marketplace shopper & commission requester'),
  (2, 'MERCHANT', 'Artisan creator / studio merchant'),
  (3, 'ADMIN',    'System-level manager (also the artist / merchant)')
ON CONFLICT (Id) DO UPDATE SET Name = EXCLUDED.Name;

-- ---------- Users & Creator Studios ----------
INSERT INTO Users (Id, Email, Username, FullName, Phone, PasswordHash, RoleId, Status, EmailVerified,
                   CommissionSlotCapacity, CommissionStartingPrice, CommissionTurnaround, CommissionFormats, CommissionTagline) VALUES
(1,  'bella@example.com',     'bella',     'Bella Santos',       '09171234567', 'PBKDF2$10000$nI47tNzSKLVtM/0Jjm73aQ==$zzKog12teiWt0BYKvI6RvQqE1bvTqA4cgEpeIkIxlWY=', 1, 'ACTIVE', TRUE, 0, 0, '', '', ''),
(2,  'mika@stardom.ph',       'mika',      'Mika Visuals',       '09174441122', 'PBKDF2$10000$cQ5mUf/OUgrOCnt95eXS1g==$VY6w93rX9Epr3u38wvOpi7zJriNboqD5oU6TnwyXJPI=', 2, 'ACTIVE', TRUE, 4, 1200.00, '2-4 business days', 'Archival Print + Vector', 'Ethereal Flora & Fauna'),
(3,  'renzo@stardom.ph',      'renzo',     'Renzo Cruz Atelier', '09175553344', 'PBKDF2$10000$cQ5mUf/OUgrOCnt95eXS1g==$VY6w93rX9Epr3u38wvOpi7zJriNboqD5oU6TnwyXJPI=', 2, 'ACTIVE', TRUE, 3, 2000.00, '5-7 business days', 'Handmade Acrylic & Ink', 'Contemporary Urban Lore'),
(4,  'puffu@stardom.ph',      'puffu',     'Puffu Studio',       '09176665566', 'PBKDF2$10000$cQ5mUf/OUgrOCnt95eXS1g==$VY6w93rX9Epr3u38wvOpi7zJriNboqD5oU6TnwyXJPI=', 2, 'ACTIVE', TRUE, 6, 800.00,  '1-3 business days', 'Die-cut Vinyl & Hologram', 'Cute & Chaotic Sticker Guild'),
(5,  'admin@stardom.ph',      'admin',     'STAR:DOM Admin',     '09281234567', 'PBKDF2$10000$cQ5mUf/OUgrOCnt95eXS1g==$VY6w93rX9Epr3u38wvOpi7zJriNboqD5oU6TnwyXJPI=', 3, 'ACTIVE', TRUE, 5, 1500.00, '3-5 business days', 'High-Res PNG + A4 Print', 'Anime & Cyberpunk Stylist'),
(6,  'juan@example.com',      'juan',      'Juan Dela Cruz',     '09175678901', 'PBKDF2$10000$nI47tNzSKLVtM/0Jjm73aQ==$zzKog12teiWt0BYKvI6RvQqE1bvTqA4cgEpeIkIxlWY=', 1, 'ACTIVE', TRUE, 0, 0, '', '', ''),
(10, 'cloe@example.com',      'cloe',      'Cloe Valenzuela',    '09179012345', 'PBKDF2$10000$nI47tNzSKLVtM/0Jjm73aQ==$zzKog12teiWt0BYKvI6RvQqE1bvTqA4cgEpeIkIxlWY=', 1, 'ACTIVE', TRUE, 0, 0, '', '', ''),
(11, 'mark@example.com',      'mark',      'Mark Alcantara',     '09170123456', 'PBKDF2$10000$nI47tNzSKLVtM/0Jjm73aQ==$zzKog12teiWt0BYKvI6RvQqE1bvTqA4cgEpeIkIxlWY=', 1, 'ACTIVE', TRUE, 0, 0, '', '', ''),
(12, 'paolo@example.com',     'paolo',     'Paolo Reyes',        '09171239876', 'PBKDF2$10000$nI47tNzSKLVtM/0Jjm73aQ==$zzKog12teiWt0BYKvI6RvQqE1bvTqA4cgEpeIkIxlWY=', 1, 'ACTIVE', TRUE, 0, 0, '', '', '')
ON CONFLICT (Id) DO UPDATE SET Email = EXCLUDED.Email, FullName = EXCLUDED.FullName;"""

seed_sql = clean_sql(seed_sql)
seed_sql = re.sub(r'-- ---------- Roles ----------[\s\S]+?-- ---------- Categories ----------', roles_and_users_sql + '\n\n-- ---------- Categories ----------', seed_sql)

# 2. Categories: IsActive
seed_sql = re.sub(r",\s*(\d+),\s*1\s*\)", r", \1, TRUE)", seed_sql)
seed_sql = re.sub(r",\s*(\d+),\s*0\s*\)", r", \1, FALSE)", seed_sql)

# 3. Promotions: IsActive
seed_sql = re.sub(r",\s*1\s*\)\s*ON\s+CONFLICT", ", TRUE)\nON CONFLICT", seed_sql)
seed_sql = re.sub(r",\s*0\s*\)\s*ON\s+CONFLICT", ", FALSE)\nON CONFLICT", seed_sql)
seed_sql = re.sub(r",\s*1\s*\)\s*,\s*$", ", TRUE),", seed_sql, flags=re.MULTILINE)
seed_sql = re.sub(r",\s*0\s*\)\s*,\s*$", ", FALSE),", seed_sql, flags=re.MULTILINE)

# 4. StoreLocations: IsActive
seed_sql = re.sub(r",\s*1\s*\)\s*ON\s+CONFLICT", ", TRUE)\nON CONFLICT", seed_sql)
seed_sql = re.sub(r",\s*0\s*\)\s*ON\s+CONFLICT", ", FALSE)\nON CONFLICT", seed_sql)
seed_sql = re.sub(r",\s*1\s*\)\s*,\s*$", ", TRUE),", seed_sql, flags=re.MULTILINE)
seed_sql = re.sub(r",\s*0\s*\)\s*,\s*$", ", FALSE),", seed_sql, flags=re.MULTILINE)

# 5. PopUpEvents: IsCurrent
def fix_event_current(m):
    guest = m.group(1)
    is_curr = 'TRUE' if m.group(2) == '1' else 'FALSE'
    lineup = m.group(3)
    return f"{guest}, {is_curr}, {lineup}"

seed_sql = re.sub(r"('[^']*'),\s*([01]),\s*('[^']*'\))", fix_event_current, seed_sql)

# 6. Commissions: AdditionalNotes NULL -> ''
seed_sql = re.sub(r"(\d+\.\d\d,\s*)NULL,(\s*(?:NULL|\d+\.\d\d),)", r"\1'',\2", seed_sql)

# 7. Notifications: IsRead
seed_sql = re.sub(r"('[a-z\-]*'),\s*1,\s*('2026-)", r"\1, TRUE, \2", seed_sql)
seed_sql = re.sub(r"('[a-z\-]*'),\s*0,\s*('2026-)", r"\1, FALSE, \2", seed_sql)

# 8. CommissionMessages: IsRead
seed_sql = re.sub(r",\s*0,\s*('2026-)", r", FALSE, \1", seed_sql)
seed_sql = re.sub(r",\s*1,\s*('2026-)", r", TRUE, \1", seed_sql)

# 9. Products table from seed_products_artshop.sql
artshop_sql = clean_sql(artshop_sql)

def fix_product_row(line):
    if not line.strip().startswith('(') or not re.search(r'^\s*\(\d+,\s*\d+,\s*\d+,', line):
        return line
    pattern = r"('[^']*'),\s*([01]),\s*([01]),\s*([01]),\s*([01]),\s*('[^']*')"
    m = re.search(pattern, line)
    if m:
        brand = m.group(1)
        b1 = 'TRUE' if m.group(2) == '1' else 'FALSE'
        b2 = 'TRUE' if m.group(3) == '1' else 'FALSE'
        b3 = 'TRUE' if m.group(4) == '1' else 'FALSE'
        b4 = 'TRUE' if m.group(5) == '1' else 'FALSE'
        badge = m.group(6)
        replaced = f"{brand}, {b1}, {b2}, {b3}, {b4}, {badge}"
        return line[:m.start()] + replaced + line[m.end():]
    return line

artshop_lines = artshop_sql.split('\n')
artshop_sql = '\n'.join([fix_product_row(l) for l in artshop_lines])

# 10. ProductImages: IsPrimary
def fix_product_image(line):
    m = re.search(r"('(/Assets/[^']+)'|\"\"\"[^\"]+\"\"\"),\s*([01]),\s*(\d+)\s*\)", line)
    if m:
        img = m.group(1)
        is_primary = 'TRUE' if m.group(3) == '1' else 'FALSE'
        sort_order = m.group(4)
        return line[:m.start()] + f"{img}, {is_primary}, {sort_order})" + line[m.end():]
    return line

artshop_lines = artshop_sql.split('\n')
artshop_sql = '\n'.join([fix_product_image(l) for l in artshop_lines])

# 11. EventInventory: IsEventExclusive, IsActive
def fix_event_inventory(line):
    m = re.search(r"(\(\s*\d+,\s*\d+,\s*\d+,\s*\d+,\s*\d+,\s*)([01]),\s*([01])\s*\)", line)
    if m:
        prefix = m.group(1)
        excl = 'TRUE' if m.group(2) == '1' else 'FALSE'
        act = 'TRUE' if m.group(3) == '1' else 'FALSE'
        return line[:m.start()] + f"{prefix}{excl}, {act})" + line[m.end():]
    return line

artshop_lines = artshop_sql.split('\n')
artshop_sql = '\n'.join([fix_event_inventory(l) for l in artshop_lines])

# 12. Bundles: IsActive
def fix_bundle(line):
    m = re.search(r"(\(\s*\d+,\s*'[^']*',\s*'[^']*',\s*\d+(?:\.\d+)?,\s*)([01])\s*\)", line)
    if m:
        prefix = m.group(1)
        act = 'TRUE' if m.group(2) == '1' else 'FALSE'
        return line[:m.start()] + f"{prefix}{act})" + line[m.end():]
    return line

artshop_lines = artshop_sql.split('\n')
artshop_sql = '\n'.join([fix_bundle(l) for l in artshop_lines])

# 13. BundleItems: pure integers
bundle_items_sql = """INSERT INTO BundleItems (BundleId, ProductId, Quantity) VALUES
(1, 1, 1), (1, 2, 1), (1, 3, 1), (1, 4, 1),
(2, 47, 1), (2, 48, 1), (2, 49, 1),
(3, 25, 1), (3, 30, 1);"""
artshop_sql = re.sub(r"INSERT INTO BundleItems[\s\S]+?;", bundle_items_sql, artshop_sql)

seq_fix = """
-- Sync auto-increment sequences after explicit ID inserts
SELECT setval(pg_get_serial_sequence('roles', 'id'), COALESCE(MAX(Id), 1)) FROM Roles;
SELECT setval(pg_get_serial_sequence('users', 'id'), COALESCE(MAX(Id), 1)) FROM Users;
SELECT setval(pg_get_serial_sequence('categories', 'id'), COALESCE(MAX(Id), 1)) FROM Categories;
SELECT setval(pg_get_serial_sequence('promotions', 'id'), COALESCE(MAX(Id), 1)) FROM Promotions;
SELECT setval(pg_get_serial_sequence('storelocations', 'id'), COALESCE(MAX(Id), 1)) FROM StoreLocations;
SELECT setval(pg_get_serial_sequence('popupevents', 'id'), COALESCE(MAX(Id), 1)) FROM PopUpEvents;
SELECT setval(pg_get_serial_sequence('products', 'id'), COALESCE(MAX(Id), 1)) FROM Products;
SELECT setval(pg_get_serial_sequence('commissions', 'id'), COALESCE(MAX(Id), 1)) FROM Commissions;
SELECT setval(pg_get_serial_sequence('bundles', 'id'), COALESCE(MAX(Id), 1)) FROM Bundles;
SELECT setval(pg_get_serial_sequence('orders', 'id'), COALESCE(MAX(Id), 1)) FROM Orders;
"""

final_seed = seed_sql.strip() + '\n\n' + artshop_sql.strip() + '\n\n' + seq_fix.strip() + '\n'

with open(os.path.join(DB_DIR, 'supabase_seed.sql'), 'w', encoding='utf-8') as f:
    f.write(final_seed)

print('SUCCESS')
