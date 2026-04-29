#!/usr/bin/env python3
"""
Generate a groomed CSV of world cities with authoritative IANA timezones
and current UTC offsets (DST-safe).

DATA SOURCES
- GeoNames allCountries.zip (https://www.geonames.org/)
- IANA Time Zone Database via Python zoneinfo

OUTPUT
- world_cities_timezones.csv

GeoNames allCountries columns (0-indexed):
  0  geonameid   1  name          2  asciiname     3  alternatenames
  4  latitude    5  longitude     6  feature_class 7  feature_code
  8  country_code 9 cc2          10 admin1code    11 admin2code
  12 admin3code  13 admin4code   14 population    15 elevation
  16 dem         17 timezone     18 modification_date

LICENSE NOTE
- GeoNames data is CC-BY 4.0 (attribution required if redistributed)

Python >= 3.9 required (zoneinfo)
"""

import csv
import zipfile
import urllib.request
from pathlib import Path
from datetime import datetime, timezone
from zoneinfo import ZoneInfo

# ---------------- CONFIG ----------------
GEONAMES_URL = "https://download.geonames.org/export/dump/allCountries.zip"
WORKDIR = Path("data")
ZIP_PATH = WORKDIR / "allCountries.zip"
TXT_PATH = WORKDIR / "allCountries.txt"
OUTPUT_CSV = "world_cities_timezones.csv"

# Only include populated places
ALLOWED_FEATURES = {
    ("P", "PPL"),
    # ("P", "PPLA"), ("P", "PPLA2"), ("P", "PPLA3"), ("P", "PPLA4"),
    # ("P", "PPLC"), ("P", "PPLG"),
}

# Minimum population to include (keeps output to ~10k rows — manageable for in-app search)
MIN_POPULATION = 50_000

# ---------------- HELPERS ----------------
def download_geonames():
    WORKDIR.mkdir(exist_ok=True)
    if ZIP_PATH.exists():
        return
    print("Downloading GeoNames allCountries.zip ...")
    urllib.request.urlretrieve(GEONAMES_URL, ZIP_PATH)


def extract_geonames():
    if TXT_PATH.exists():
        return
    print("Extracting allCountries.txt ...")
    with zipfile.ZipFile(ZIP_PATH, "r") as zf:
        zf.extract("allCountries.txt", WORKDIR)


def format_utc_offset(minutes: int) -> str:
    sign = "+" if minutes >= 0 else "-"
    minutes = abs(minutes)
    return f"UTC{sign}{minutes // 60:02d}:{minutes % 60:02d}"


# ---------------- MAIN ----------------
def main():
    download_geonames()
    extract_geonames()

    out_path = Path(OUTPUT_CSV)
    if (
        out_path.exists()
        and TXT_PATH.exists()
        and out_path.stat().st_mtime >= TXT_PATH.stat().st_mtime
    ):
        print(f"{OUTPUT_CSV} is up-to-date, skipping generation.")
        return

    now_utc = datetime.now(timezone.utc)

    # key: (country_code, ascii_name.lower()) → [population, name, asciiname, alt_names_set, lat, lon, timezone_id, offset]
    best: dict = {}

    with open(TXT_PATH, encoding="utf-8") as src:
        reader = csv.reader(src, delimiter="\t")

        for row in reader:
            # GeoNames allCountries has exactly 19 tab-separated columns.
            # Index 17 = timezone (IANA), Index 18 = modification_date.
            if len(row) < 19:
                continue

            (
                geonameid, name, asciiname, alternatenames,
                lat, lon, fclass, fcode, country_code,
                cc2, admin1, admin2, admin3, admin4,
                population, elevation, dem,
                timezone_id, modification_date
            ) = row[:19]

            if (fclass, fcode) not in ALLOWED_FEATURES:
                continue

            if not timezone_id:
                continue

            try:
                pop = int(population or 0)
                if pop < MIN_POPULATION:
                    continue
            except ValueError:
                continue

            try:
                tz = ZoneInfo(timezone_id)
                offset = int(now_utc.astimezone(tz).utcoffset().total_seconds() / 60)
            except Exception:
                continue

            # Collect alternative names, excluding the city name itself
            name_lower = name.lower()
            ascii_lower = asciiname.lower()
            new_alts = {
                n for n in alternatenames.split(",")
                if n and n.lower() not in {name_lower, ascii_lower}
            }

            dedup_key = (country_code, ascii_lower)
            if dedup_key not in best or pop > best[dedup_key][0]:
                # New best entry — preserve any previously accumulated alt names
                existing_alts = best[dedup_key][7] if dedup_key in best else set()
                best[dedup_key] = [pop, name, asciiname, lat, lon, timezone_id, offset, new_alts | existing_alts]
            else:
                # Lower-population duplicate — only merge its alt names
                best[dedup_key][7] |= new_alts

    with open(OUTPUT_CSV, "w", newline="", encoding="utf-8") as out:
        writer = csv.writer(out)
        writer.writerow([
            "country_code",
            "city_name",
            "ascii_name",
            "alt_names",
            "latitude",
            "longitude",
            "iana_timezone",
            "utc_offset_minutes",
            "utc_offset_label",
        ])

        for (country_code, _), (pop, name, asciiname, lat, lon, timezone_id, offset, alts) in best.items():
            alt_names = "|".join(sorted(alts))
            writer.writerow([
                country_code,
                name,
                asciiname,
                alt_names,
                lat,
                lon,
                timezone_id,
                offset,
                format_utc_offset(offset),
            ])

    print(f"Generated {OUTPUT_CSV} ({len(best):,} unique cities)")


if __name__ == "__main__":
    main()
