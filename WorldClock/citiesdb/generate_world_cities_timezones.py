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
import os
import threading
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
    ("P", "PPL"),   # populated place
    ("P", "PPLA"),  # seat of a first-order administrative division (e.g. state capital)
    ("P", "PPLC"),  # capital of a country (e.g. Bogotá, Paris, Tokyo)
    ("P", "PPLG"),  # seat of government of a political entity
    # ("P", "PPLA2"), ("P", "PPLA3"), ("P", "PPLA4"),  # lower-level admin seats
}

# Minimum population to include (keeps output to ~10k rows — manageable for in-app search)
MIN_POPULATION = 50_000

# ---------------- HELPERS ----------------
DOWNLOAD_THREADS = 8  # parallel chunks; tune to your connection


def _get_content_length(url: str) -> int | None:
    req = urllib.request.Request(url, method="HEAD")
    with urllib.request.urlopen(req) as resp:
        cl = resp.headers.get("Content-Length")
        return int(cl) if cl else None


def _download_chunk(
    url: str,
    start: int,
    end: int,
    dest: Path,
    index: int,
    progress: list[int],
    lock: threading.Lock,
) -> None:
    req = urllib.request.Request(url, headers={"Range": f"bytes={start}-{end}"})
    with urllib.request.urlopen(req) as resp:
        with open(dest, "r+b") as f:
            f.seek(start)
            while True:
                chunk = resp.read(65536)
                if not chunk:
                    break
                f.write(chunk)
                with lock:
                    progress[0] += len(chunk)


def _print_progress(progress: list[int], total: int, done_event: threading.Event) -> None:
    while not done_event.wait(timeout=0.5):
        downloaded = progress[0]
        pct = min(downloaded / total * 100, 100) if total else 0
        mb_done = downloaded / 1_048_576
        mb_total = total / 1_048_576
        print(f"\r  {pct:5.1f}%  {mb_done:.1f} / {mb_total:.1f} MB  ({DOWNLOAD_THREADS} threads)", end="", flush=True)
    # final line
    print(f"\r  100.0%  {total / 1_048_576:.1f} / {total / 1_048_576:.1f} MB  done          ", flush=True)


def download_geonames():
    WORKDIR.mkdir(exist_ok=True)
    if ZIP_PATH.exists():
        return

    print("Downloading GeoNames allCountries.zip ...")
    total = _get_content_length(GEONAMES_URL)

    if total is None:
        # Server doesn't support HEAD or Content-Length — fall back to single stream
        print("  (server did not return Content-Length, downloading single-stream)")
        urllib.request.urlretrieve(GEONAMES_URL, ZIP_PATH)
        return

    # Pre-allocate the file
    with open(ZIP_PATH, "wb") as f:
        f.seek(total - 1)
        f.write(b"\0")

    chunk_size = total // DOWNLOAD_THREADS
    ranges = [
        (i * chunk_size, (i + 1) * chunk_size - 1 if i < DOWNLOAD_THREADS - 1 else total - 1)
        for i in range(DOWNLOAD_THREADS)
    ]

    progress: list[int] = [0]
    lock = threading.Lock()
    done_event = threading.Event()

    printer = threading.Thread(target=_print_progress, args=(progress, total, done_event), daemon=True)
    printer.start()

    threads = [
        threading.Thread(
            target=_download_chunk,
            args=(GEONAMES_URL, start, end, ZIP_PATH, i, progress, lock),
            daemon=True,
        )
        for i, (start, end) in enumerate(ranges)
    ]
    for t in threads:
        t.start()
    for t in threads:
        t.join()

    done_event.set()
    printer.join()


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

    # Pre-compute offset for every unique IANA timezone (~400 total).
    # Without this, ZoneInfo + utcoffset() is called once per row (~12 M times).
    tz_offset_cache: dict[str, int | None] = {}

    def get_offset(tz_id: str) -> int | None:
        if tz_id not in tz_offset_cache:
            try:
                tz = ZoneInfo(tz_id)
                tz_offset_cache[tz_id] = int(
                    now_utc.astimezone(tz).utcoffset().total_seconds() / 60
                )
            except Exception:
                tz_offset_cache[tz_id] = None
        return tz_offset_cache[tz_id]

    with open(TXT_PATH, encoding="utf-8") as src, open(
        OUTPUT_CSV, "w", newline="", encoding="utf-8"
    ) as out:
        reader = csv.reader(src, delimiter="	")
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

        for row in reader:
            # GeoNames allCountries has exactly 19 tab-separated columns.
            # Index 17 = timezone (IANA), Index 18 = modification_date.
            # The old unpacking used *_rest which captured both trailing fields,
            # leaving modification_date in timezone_id — that caused ZoneInfo to
            # fail on every row and produce an empty CSV.
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

            # Population filter — keeps output to a practical size
            try:
                if int(population or 0) < MIN_POPULATION:
                    continue
            except ValueError:
                continue

            offset = get_offset(timezone_id)
            if offset is None:
                continue

            alt_names = "|".join(
                n for n in alternatenames.split(",")
                if n and n.lower() not in {name.lower(), asciiname.lower()}
            )

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

    print(f"Generated {OUTPUT_CSV}")


if __name__ == "__main__":
    main()