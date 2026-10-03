#!/usr/bin/env python
"""Regenerates Database/mall_images.sql from the files in Assets/Malls.

WHY
    App/AssetImg.aspx serves venue photos out of the AssetImages table (BYTEA)
    and falls back to the file on disk. That only protects the photos while the
    table holds them, so the table has to be loadable from something. This script
    is that something: it turns the committed .webp files into a single SQL file
    of upserts, so a fresh database (or a restore) can be given the same photos
    without anyone hand-editing hex.

WHY NOT INSIDE supabase_seed.sql
    Because the seed's TRUNCATE list deliberately omits AssetImages -- the rows
    are the durable copy of the artwork, and re-seeding the catalogue must not
    delete them. Keeping the loader separate means the seed stays the catalogue
    and this file stays the photography.

USAGE
    python tools/generate_mall_images_sql.py
    psql "$SUPABASE_DB_URL" -f STAR-DOM-Web/Database/mall_images.sql
"""
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC_DIR = os.path.join(ROOT, 'STAR-DOM-Web', 'Assets', 'Malls')
OUT = os.path.join(ROOT, 'STAR-DOM-Web', 'Database', 'mall_images.sql')


def mime_for(name):
    """Content type to store. Every file here is WebP; the switch is only so a
    future PNG import does not get served with a lying Content-Type."""
    ext = os.path.splitext(name)[1].lower()
    return {
        '.png': 'image/png',
        '.jpg': 'image/jpeg',
        '.jpeg': 'image/jpeg',
        '.gif': 'image/gif',
        '.webp': 'image/webp',
    }.get(ext, 'application/octet-stream')


def main():
    if not os.path.isdir(SRC_DIR):
        raise SystemExit('no such folder: %s' % SRC_DIR)

    files = sorted(f for f in os.listdir(SRC_DIR)
                   if os.path.splitext(f)[1].lower() in
                   ('.webp', '.png', '.jpg', '.jpeg', '.gif'))
    if not files:
        raise SystemExit('no images in %s' % SRC_DIR)

    lines = [
        '-- ============================================================',
        '-- STAR:DOM -- Mall photography, stored as BYTEA (GENERATED FILE)',
        '--',
        '-- Regenerate with:  python tools/generate_mall_images_sql.py',
        '-- Load with:        psql "$SUPABASE_DB_URL" -f mall_images.sql',
        '--',
        '-- WHY THIS EXISTS',
        '--   App\\AssetImg.aspx serves each venue photo from AssetImages and falls',
        '--   back to the file under Assets\\Malls. Files can be deleted by accident',
        '--   and are not carried by every clone or deploy; a pg_dump of the',
        '--   database is. Loading the bytes here means the itinerary grid still',
        '--   has pictures after the folder is gone.',
        '--',
        '-- IDEMPOTENT: upserts on Path, so re-running replaces a photo with the',
        '-- current file instead of failing or duplicating.',
        '--',
        '-- NOT PART OF supabase_seed.sql ON PURPOSE: the seed TRUNCATEs the demo',
        '-- tables, and these rows are meant to survive that. Deleting a row here',
        '-- is how you retire a photo.',
        '-- ============================================================',
        '',
        '-- The table itself is created by supabase_schema.sql. This file only',
        '-- fills it, so it is safe to run against an older database that has the',
        '-- table but no photos yet.',
        'INSERT INTO AssetImages (Path, Data, Mime, ByteSize, UpdatedAt) VALUES',
    ]

    parts = []
    total = 0
    for name in files:
        with open(os.path.join(SRC_DIR, name), 'rb') as fh:
            blob = fh.read()
        total += len(blob)
        # decode('<hex>','hex') is how a blob gets into a plain .sql file:
        # base64 would need pg_read_file, which only a superuser can use on
        # Supabase, and \copy cannot name a column set this way.
        parts.append("    ('/Assets/Malls/%s', decode('%s', 'hex'), '%s', %d, NOW())"
                     % (name.replace("'", "''"), blob.hex(), mime_for(name), len(blob)))

    # ON CONFLICT belongs to the INSERT statement itself, so it has to be
    # emitted before the terminating semicolon.
    lines.append(',\n'.join(parts))
    lines.append('')
    lines.append('-- ON CONFLICT (Path) DO UPDATE so re-running after re-exporting a')
    lines.append('-- photo replaces the bytes instead of hitting the primary key.')
    lines.append('ON CONFLICT (Path) DO UPDATE SET')
    lines.append('    Data = EXCLUDED.Data,')
    lines.append('    Mime = EXCLUDED.Mime,')
    lines.append('    ByteSize = EXCLUDED.ByteSize,')
    lines.append('    UpdatedAt = NOW();')
    lines.append('')

    with open(OUT, 'w', encoding='utf-8', newline='\n') as fh:
        fh.write('\n'.join(lines))

    print('wrote %s' % OUT)
    print('  %d images, %.1f KB of photo bytes' % (len(files), total / 1024.0))
    for name in files:
        print('  /Assets/Malls/%s (%d bytes)'
              % (name, os.path.getsize(os.path.join(SRC_DIR, name))))


if __name__ == '__main__':
    main()