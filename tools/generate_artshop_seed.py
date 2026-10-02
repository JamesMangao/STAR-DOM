import json
import re
import os
import sys

# Every path below is derived from this file's own location, so the repo can
# live on any drive under any folder name. Nothing here is absolute.
# tools/generate_artshop_seed.py  ->  <repo>/tools/generate_artshop_seed.py
REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
WEB = os.path.join(REPO, 'STAR-DOM-Web')

CATALOG_JSON = os.path.join(REPO, 'design', 'assets', 'extracted_products.json')

if not os.path.isfile(CATALOG_JSON):
    sys.exit(
        "Missing input: %s\n"
        "This is a one-time generator for the original Artshop catalogue import.\n"
        "That design/assets folder is not part of the repo, so the script cannot\n"
        "run from a fresh clone. The catalogue it produced is already committed as\n"
        "STAR-DOM-Web/Database/supabase_seed.sql -- use that instead.\n"
        % CATALOG_JSON
    )

with open(CATALOG_JSON, 'r', encoding='utf-8') as f:
    raw_lines = json.load(f)

# Types pattern
type_list = [
    'STICKER SHEETS',
    'STICKER',
    'ART PRINT',
    'BUTTON PINS',
    'KEYCHAINS',
    'TEMP TATTOOS',
    'CLAY DESKBUDDIES',
    'CLAY PINS'
]

cat_map = {
    'STICKER': 3,          # Stickers
    'STICKER SHEETS': 3,   # Stickers
    'ART PRINT': 2,        # Fine Art Prints
    'BUTTON PINS': 6,      # Merchandise
    'KEYCHAINS': 4,        # Keychains
    'TEMP TATTOOS': 6,     # Merchandise
    'CLAY DESKBUDDIES': 10,# Other
    'CLAY PINS': 6         # Merchandise
}

# Scan Assets folder
assets_root = os.path.join(WEB, 'Assets')
asset_files = []
for root, dirs, files in os.walk(assets_root):
    for fn in files:
        if fn.lower().endswith(('.png', '.jpg', '.jpeg', '.webp')):
            full_path = os.path.join(root, fn)
            rel_path = '/' + os.path.relpath(full_path, WEB).replace('\\', '/')
            cat_folder = os.path.basename(root).upper()
            asset_files.append({
                'filename': fn,
                'rel_path': rel_path,
                'folder': cat_folder
            })

print(f"Loaded {len(asset_files)} asset files from Assets folder.")

def normalize_text(t):
    t = os.path.splitext(t)[0].lower()
    t = re.sub(r'[\(\)\"\',_\.]', ' ', t)
    t = re.sub(r'\s+', ' ', t).strip()
    return t

pattern = r'^(?:#\d+|Item ID)\s+(.+?)\s+(' + '|'.join(type_list) + r')\s+₱([\d\.]+|xx\.xx)\s+(\d+|##)\s*(In Stock|Low Stock|Restock|Out of Stock)(.*)$'

parsed_products = []
slug_counts = {}

def slugify(s):
    s = s.lower()
    s = re.sub(r'[^a-z0-9]+', '-', s)
    s = s.strip('-')
    if not s:
        s = 'item'
    return s

def match_image_for_product(name, ptype):
    n_name = normalize_text(name)
    candidates = []
    for af in asset_files:
        if ptype == 'STICKER' and 'STICKERS' in af['folder']:
            candidates.append(af)
        elif ptype == 'STICKER SHEETS' and 'STICKER SHEETS' in af['folder']:
            candidates.append(af)
        elif ptype == 'ART PRINT' and 'ART PRINTS' in af['folder']:
            candidates.append(af)
        elif ptype == 'BUTTON PINS' and 'BUTTON PINS' in af['folder']:
            candidates.append(af)
        elif ptype == 'KEYCHAINS' and 'KEYCHAINS' in af['folder']:
            candidates.append(af)

    # Direct match
    for c in candidates:
        n_file = normalize_text(c['filename'])
        if n_name == n_file:
            return c['rel_path']

    # Specific synonyms & normalized substring matches
    for c in candidates:
        n_file = normalize_text(c['filename'])
        if n_name in n_file or n_file in n_name:
            return c['rel_path']

    # Custom aliases
    aliases = {
        ('deer', 'STICKER'): 'Philippine Deer.png',
        ('phm', 'STICKER'): 'Project Hail Mary.png',
        ('phm', 'BUTTON PINS'): 'Project Hail Mary.png',
        ('star', 'BUTTON PINS'): 'Please I am a Star.png',
        ('i luv stars', 'BUTTON PINS'): 'Please I am a Star.png',
        ('nerdz', 'BUTTON PINS'): 'I love Nerdz.png',
        ('align', 'ART PRINT'): 'Stars will align (5x7).png',
        ('bleeding fame', 'ART PRINT'): 'Bleeding Heart Pigeon (5x7).png',
        ('dont car', 'STICKER'): 'i don_t car.png',
        ('forgor', 'STICKER'): 'i forgor.png',
        ('paint tube', 'STICKER'): 'ICU Paint Tube.jpg',
        ('icu paint tube', 'STICKER'): 'ICU Paint Tube.jpg',
        ('love', 'STICKER SHEETS'): 'Love sticker sheet (3x4).png',
        ('kahit saan', 'ART PRINT'): 'Kahit saan (4x6).jpg',
        ('bread tag', 'KEYCHAINS'): 'Bread tag keychain.png',
        ('goby', 'KEYCHAINS'): 'Goby keychain.png',
        ('pigeon', 'KEYCHAINS'): 'Pigeon keychain.png',
    }

    for (alias_key, alias_type), target_f in aliases.items():
        if alias_key in n_name and ptype == alias_type:
            for c in candidates:
                if target_f.lower() in c['filename'].lower():
                    return c['rel_path']

    return None

for line in raw_lines:
    if not line.strip() or 'Item ID Item name Type Price' in line:
        continue
    m = re.match(pattern, line, re.IGNORECASE)
    if not m:
        print(f"FAILED TO MATCH: {line}")
        continue
    name, ptype, price_str, stock_str, status_str, notes = m.groups()
    name = name.strip()
    ptype = ptype.strip().upper()
    status_str = status_str.strip()
    notes = notes.strip()

    # Price handling
    if 'xx' in price_str.lower():
        base_price = 0.00
    else:
        try:
            base_price = float(price_str)
        except ValueError:
            base_price = 0.00

    # Stock handling
    if '##' in stock_str:
        stock = 0
    else:
        try:
            stock = int(stock_str)
        except ValueError:
            stock = 0

    category_id = cat_map.get(ptype, 10)
    
    idx = len(parsed_products)
    
    # Assign merchants across Mika (2), Renzo (3), Puffu (4)
    if ptype in ['STICKER', 'STICKER SHEETS']:
        merchant_id = 4 # Puffu Studio
        brand_name = 'Puffu Studio'
    elif ptype in ['ART PRINT']:
        merchant_id = 2 if (idx % 2 == 0) else 3 # Mika Visuals / Renzo Atelier
        brand_name = 'Mika Visuals' if merchant_id == 2 else 'Renzo Cruz Atelier'
    elif ptype in ['KEYCHAINS', 'CLAY DESKBUDDIES']:
        merchant_id = 3 # Renzo / RedFox
        brand_name = 'RedFox Workshop'
    else: # BUTTON PINS, TEMP TATTOOS, CLAY PINS
        merchant_id = 2
        brand_name = 'Guild Collective'

    sku = f"SKU-AS-{idx+1:04d}"
    
    base_slug = slugify(f"{name}-{ptype}")
    if base_slug in slug_counts:
        slug_counts[base_slug] += 1
        slug = f"{base_slug}-{slug_counts[base_slug]}"
    else:
        slug_counts[base_slug] = 1
        slug = base_slug

    desc = f"Authentic {ptype.lower()} by {brand_name}. {status_str}"
    if notes:
        desc += f" ({notes})"

    # Low stock threshold
    low_stock = 3

    # Badge label
    badge = ""
    if status_str.upper() == 'RESTOCK':
        badge = "RESTOCK"
    elif status_str.upper() == 'LOW STOCK':
        badge = "LOW STOCK"
    elif status_str.upper() == 'OUT OF STOCK':
        badge = "OUT OF STOCK"
    elif idx < 8:
        badge = "POPULAR"

    # Material specifications per user instructions:
    # Button pins: 1.25 in.
    # Art prints and stickers: Matte finish
    if ptype == 'BUTTON PINS':
        mat = "1.25 in. pinback button"
    elif ptype in ['STICKER', 'STICKER SHEETS']:
        mat = "Die-cut sticker, Matte finish"
    elif ptype == 'ART PRINT':
        mat = "Archival art print, Matte finish"
    elif ptype == 'KEYCHAINS':
        mat = "Durable acrylic keychain"
    elif ptype == 'TEMP TATTOOS':
        mat = "Skin-safe temporary tattoo"
    elif ptype == 'CLAY DESKBUDDIES':
        mat = "Handcrafted polymer clay desk buddy"
    elif ptype == 'CLAY PINS':
        mat = "Handcrafted polymer clay pin"
    else:
        mat = f"Original {ptype.title()}"

    img_path = match_image_for_product(name, ptype)

    parsed_products.append({
        'id': idx + 1,
        'merchant_id': merchant_id,
        'category_id': category_id,
        'name': name,
        'slug': slug,
        'description': desc,
        'base_price': base_price,
        'sale_price': None,
        'stock_quantity': stock,
        'low_stock_threshold': low_stock,
        'sku': sku,
        'brand_name': brand_name,
        'is_active': 1,
        'is_featured': 1 if idx < 12 else 0,
        'is_booth_exclusive': 1 if (idx % 7 == 0) else 0,
        'is_event_exclusive': 1 if (idx % 11 == 0) else 0,
        'badge_label': badge,
        'material_details': mat,
        'rating_avg': 5.00 if (idx % 3 == 0) else (4.50 if (idx % 2 == 0) else 0.00),
        'rating_count': 1 if (idx % 2 == 0) else 0,
        'sold_count': (idx * 3 + 5) % 40,
        'image_file': img_path
    })

print(f"Total processed products: {len(parsed_products)}")
matched_imgs = [p for p in parsed_products if p['image_file']]
print(f"Products with matched image files: {len(matched_imgs)} / {len(parsed_products)}")

# Generate SQL script
sql_lines = [
    "-- ============================================================",
    "-- STAR:DOM — Seed data from ARTSHOP DATABASE (100 items)",
    "-- Replaces previous demo catalog with official 100 Artshop items",
    "-- Includes image wiring from STAR-DOM-Web\\Assets",
    "-- ============================================================",
    "USE stardom;",
    "",
    "-- Disable foreign key checks for clean table reset",
    "SET FOREIGN_KEY_CHECKS = 0;",
    "TRUNCATE TABLE EventSales;",
    "TRUNCATE TABLE EventInventory;",
    "TRUNCATE TABLE OrderItems;",
    "TRUNCATE TABLE CartItems;",
    "TRUNCATE TABLE WishlistItems;",
    "TRUNCATE TABLE Reviews;",
    "TRUNCATE TABLE BundleItems;",
    "TRUNCATE TABLE Bundles;",
    "TRUNCATE TABLE ProductImages;",
    "TRUNCATE TABLE ProductVariants;",
    "TRUNCATE TABLE Products;",
    "SET FOREIGN_KEY_CHECKS = 1;",
    "",
    "-- ---------- Insert 100 ARTSHOP Products ----------",
    "INSERT INTO Products (Id, MerchantId, CategoryId, Name, Slug, Description, BasePrice, SalePrice, StockQuantity,",
    "                      LowStockThreshold, Sku, BrandName, IsActive, IsFeatured, IsBoothExclusive, IsEventExclusive, BadgeLabel, MaterialDetails, RatingAvg, RatingCount, SoldCount) VALUES"
]

val_rows = []
for p in parsed_products:
    escaped_name = p['name'].replace("'", "''")
    escaped_desc = p['description'].replace("'", "''")
    escaped_brand = p['brand_name'].replace("'", "''")
    escaped_badge = p['badge_label'].replace("'", "''")
    escaped_mat = p['material_details'].replace("'", "''")
    sale_p = "NULL" if p['sale_price'] is None else f"{p['sale_price']:.2f}"
    
    val_rows.append(
        f"({p['id']}, {p['merchant_id']}, {p['category_id']}, '{escaped_name}', '{p['slug']}', '{escaped_desc}', {p['base_price']:.2f}, {sale_p}, {p['stock_quantity']}, "
        f"{p['low_stock_threshold']}, '{p['sku']}', '{escaped_brand}', {p['is_active']}, {p['is_featured']}, {p['is_booth_exclusive']}, {p['is_event_exclusive']}, "
        f"'{escaped_badge}', '{escaped_mat}', {p['rating_avg']:.2f}, {p['rating_count']}, {p['sold_count']})"
    )

sql_lines.append(",\n".join(val_rows) + ";")

# ProductImages insertion
img_rows = []
for p in parsed_products:
    if p['image_file']:
        escaped_img = p['image_file'].replace("'", "''")
        img_rows.append(f"({p['id']}, '{escaped_img}', 1, 1)")

if img_rows:
    sql_lines.extend([
        "",
        "-- ---------- Product Images (From Assets) ----------",
        "INSERT INTO ProductImages (ProductId, ImageFile, IsPrimary, SortOrder) VALUES",
        ",\n".join(img_rows) + ";"
    ])

# Re-link Event Inventory (Galleria South event #1)
sql_lines.extend([
    "",
    "-- ---------- Event inventory (Galleria South tour) ----------",
    "INSERT INTO EventInventory (EventId, ProductId, StartingStock, SoldQuantity, RemainingStock, IsEventExclusive, IsActive) VALUES",
    "(1, 1, 25, 4, 21, 0, 1),",
    "(1, 2, 20, 4, 16, 0, 1),",
    "(1, 3, 15, 5, 10, 0, 1),",
    "(1, 25, 10, 6, 4, 0, 1),",
    "(1, 30, 10, 5, 5, 0, 1),",
    "(1, 46, 10, 10, 0, 1, 1),",
    "(1, 56, 12, 8, 4, 0, 1),",
    "(1, 65, 8, 5, 3, 0, 1);",
    "",
    "-- ---------- Event sales (in-person POS for active run) ----------",
    "INSERT INTO EventSales (EventId, OrderId, ProductId, Quantity, UnitPrice, TotalAmount, SaleType, PaymentMethod, SaleDate, Notes) VALUES",
    "(1, NULL, 1, 2, 30.00, 60.00, 'IN_PERSON', 'GCASH', '2026-09-04 11:20:00', 'Walk-in'),",
    "(1, NULL, 25, 1, 120.00, 120.00, 'QR',       'MAYA',  '2026-09-04 13:05:00', 'QR scan'),",
    "(1, NULL, 30, 1, 100.00, 100.00, 'IN_PERSON', 'CASH', '2026-09-04 14:40:00', 'Art Print sale'),",
    "(1, NULL, 56, 1, 150.00, 150.00, 'PREORDER', 'GCASH', '2026-09-04 16:10:00', 'Booth pre-order');",
    "",
    "-- ---------- Bundles ----------",
    "-- User specs: Stickers Bundle (4 for 100 PHP), Button pins Bundle (3 for 100 PHP)",
    "-- Standard price 4 stickers @ 30 = 120 PHP -> 100 PHP (16.67% discount)",
    "-- Standard price 3 button pins @ 35 = 105 PHP -> 100 PHP (4.76% discount)",
    "INSERT INTO Bundles (Id, Name, Description, DiscountPercent, IsActive) VALUES",
    "(1, 'Stickers Bundle (4 for 100)', 'Choose your favorite 4 stickers (Bleeding heart, Tamaraw, Tarsier, Goby) for only ₱100!', 16.67, 1),",
    "(2, 'Button Pins Bundle (3 for 100)', 'Pick 3 premium 1.25 in. button pins (Bleed, Bangus, I luv stars) for only ₱100!', 4.76, 1),",
    "(3, 'Artisan Prints & Sheet Set', 'Dinostarz sticker sheet + Trees art print set with matte finish', 15.00, 1);",
    "",
    "INSERT INTO BundleItems (BundleId, ProductId, Quantity) VALUES",
    "(1, 1, 1), (1, 2, 1), (1, 3, 1), (1, 4, 1),",
    "(2, 47, 1), (2, 48, 1), (2, 49, 1),",
    "(3, 25, 1), (3, 30, 1);"
])

full_sql = "\n".join(sql_lines)

out_sql = os.path.join(WEB, 'Database', 'seed_products_artshop.sql')
with open(out_sql, 'w', encoding='utf-8') as f:
    f.write(full_sql)

print("Generated seed_products_artshop.sql successfully!")
