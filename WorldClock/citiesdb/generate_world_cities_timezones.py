#!/usr/bin/env python3
"""
Generate a groomed CSV of world cities with authoritative IANA timezones
and current UTC offsets (DST-safe).

DATA SOURCE
- Kaggle: samvelkoch/global-airports-iata-icao-timezone-geo
  https://www.kaggle.com/datasets/samvelkoch/global-airports-iata-icao-timezone-geo

  Requires the Kaggle CLI:
      pip install kaggle
  Credentials: ~/.kaggle/kaggle.json  (or KAGGLE_USERNAME / KAGGLE_KEY env vars)
  See: https://www.kaggle.com/settings → API → Create New Token

OUTPUT
- world_cities_timezones.csv  (one row per unique city)

Output columns:
  country_code, city_name, ascii_name, alt_names,
  latitude, longitude, iana_timezone, utc_offset_minutes, utc_offset_label

Source columns used:
  City_Name, Country_CodeA2, IATA, ICAO, City_IATA,
  GeoPointLat, GeoPointLong, TimeZone

Python >= 3.9 required (zoneinfo)
"""

import csv
import json
import os
import subprocess
import unicodedata
import zipfile
from pathlib import Path
from datetime import datetime, timezone
from zoneinfo import ZoneInfo

# ---------------- CONFIG ----------------
KAGGLE_DATASET = "samvelkoch/global-airports-iata-icao-timezone-geo"
WORKDIR = Path("data")
CSV_PATH = WORKDIR / "airports.csv"
OUTPUT_CSV = "world_cities_timezones.csv"

# Set to False when a corporate SSL proxy causes certificate verification errors.
# WARNING: disabling SSL verification exposes the connection to MITM attacks.
# Prefer adding your corporate root CA to the system trust store instead.
KAGGLE_SSL_VERIFY = False

# ---------------- HELPERS ----------------

def to_ascii(text: str) -> str:
    """Normalize a unicode city name to its closest ASCII representation."""
    return unicodedata.normalize("NFKD", text).encode("ascii", "ignore").decode("ascii").strip()


def _load_kaggle_credentials() -> tuple[str, str]:
    """Return (username, key) from env vars or ~/.kaggle/kaggle.json."""
    username = os.environ.get("KAGGLE_USERNAME", "")
    key      = os.environ.get("KAGGLE_KEY", "")
    if username and key:
        return username, key
    kaggle_json = Path.home() / ".kaggle" / "kaggle.json"
    if kaggle_json.exists():
        with open(kaggle_json) as f:
            creds = json.load(f)
        return creds.get("username", ""), creds.get("key", "")
    raise SystemExit(
        "Kaggle credentials not found.\n"
        "Create your API token at https://www.kaggle.com/settings → API → Create New Token\n"
        "and place kaggle.json in ~/.kaggle/  (or set KAGGLE_USERNAME / KAGGLE_KEY env vars)."
    )


def _download_direct_no_verify() -> None:
    """Download via Kaggle REST API with SSL verification disabled (corporate proxy workaround)."""
    try:
        import requests
        import urllib3
    except ImportError:
        raise SystemExit("Install requests: pip install requests")

    urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)
    print("  WARNING: SSL verification disabled (KAGGLE_SSL_VERIFY=False).")

    username, key = _load_kaggle_credentials()
    url = f"https://www.kaggle.com/api/v1/datasets/download/{KAGGLE_DATASET}"

    resp = requests.get(url, auth=(username, key), verify=False, stream=True)
    resp.raise_for_status()

    total = int(resp.headers.get("Content-Length", 0))
    downloaded = 0
    zip_path = WORKDIR / "dataset.zip"

    with open(zip_path, "wb") as f:
        for chunk in resp.iter_content(chunk_size=65536):
            f.write(chunk)
            downloaded += len(chunk)
            if total:
                pct = min(downloaded / total * 100, 100)
                print(f"\r  {pct:5.1f}%  {downloaded/1_048_576:.1f} / {total/1_048_576:.1f} MB",
                      end="", flush=True)
    print()

    print(f"Extracting {zip_path.name} ...")
    with zipfile.ZipFile(zip_path) as zf:
        zf.extractall(WORKDIR)
    zip_path.unlink()


def download_dataset() -> None:
    WORKDIR.mkdir(exist_ok=True)
    if CSV_PATH.exists():
        return

    print("Downloading Kaggle dataset ...")

    if not KAGGLE_SSL_VERIFY:
        _download_direct_no_verify()
    else:
        try:
            subprocess.run(
                [
                    "kaggle", "datasets", "download",
                    "-d", KAGGLE_DATASET,
                    "-p", str(WORKDIR),
                ],
                check=True,
            )
        except FileNotFoundError:
            raise SystemExit(
                "\nkaggle CLI not found. Install it with:\n"
                "    pip install kaggle\n"
                "Then create your API token at https://www.kaggle.com/settings → API → Create New Token\n"
                "and place kaggle.json in ~/.kaggle/  (or set KAGGLE_USERNAME / KAGGLE_KEY env vars)."
            )
        except subprocess.CalledProcessError as exc:
            raise SystemExit(f"Download failed: {exc}")

        for zp in WORKDIR.glob("*.zip"):
            print(f"Extracting {zp.name} ...")
            with zipfile.ZipFile(zp) as zf:
                zf.extractall(WORKDIR)
            zp.unlink()

    if not CSV_PATH.exists():
        raise SystemExit(
            f"Expected '{CSV_PATH}' after extraction but it was not found. "
            "Check the contents of the data/ folder."
        )


def format_utc_offset(minutes: int) -> str:
    sign = "+" if minutes >= 0 else "-"
    minutes = abs(minutes)
    return f"UTC{sign}{minutes // 60:02d}:{minutes % 60:02d}"


# ---------------- MAIN ----------------
def main() -> None:
    download_dataset()

    now_utc = datetime.now(timezone.utc)

    # Pre-compute offset per unique IANA timezone to avoid repeated ZoneInfo construction.
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

    # key: (country_code, ascii_city_lower) → entry dict
    # Multiple airports can share a city; keep one row per city,
    # preferring the primary city airport (IATA == City_IATA).
    best: dict[tuple[str, str], dict] = {}

    with open(CSV_PATH, encoding="utf-8") as src:
        reader = csv.DictReader(src)
        for row in reader:
            country_code = (row.get("Country_CodeA2") or "").strip()
            city_name    = (row.get("City_Name")      or "").strip()
            iata         = (row.get("IATA")           or "").strip()
            icao         = (row.get("ICAO")           or "").strip()
            city_iata    = (row.get("City_IATA")      or "").strip()
            lat          = (row.get("GeoPointLat")    or "").strip()
            lon          = (row.get("GeoPointLong")   or "").strip()
            timezone_id  = (row.get("TimeZone")       or "").strip()

            if not (country_code and city_name and timezone_id and lat and lon):
                continue

            ascii_name  = to_ascii(city_name)
            # Fall back to the original name when ASCII normalization produces an
            # empty string (e.g. fully non-Latin city names like Chinese or Arabic).
            ascii_lower = (ascii_name or city_name).lower()
            dedup_key   = (country_code, ascii_lower)

            offset = get_offset(timezone_id)
            if offset is None:
                continue

            alts       = {a for a in (iata, icao) if a}
            is_primary = bool(city_iata and iata == city_iata)

            if dedup_key not in best:
                best[dedup_key] = {
                    "country_code": country_code,
                    "city_name":    city_name,
                    "ascii_name":   ascii_name or city_name,
                    "lat":          lat,
                    "lon":          lon,
                    "timezone_id":  timezone_id,
                    "offset":       offset,
                    "alts":         alts,
                    "is_primary":   is_primary,
                }
            else:
                entry = best[dedup_key]
                entry["alts"] |= alts
                # Upgrade to the primary city airport when we find it
                if not entry["is_primary"] and is_primary:
                    entry.update({
                        "city_name":   city_name,
                        "ascii_name":  ascii_name or city_name,
                        "lat":         lat,
                        "lon":         lon,
                        "timezone_id": timezone_id,
                        "offset":      offset,
                        "is_primary":  True,
                    })

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
        for entry in best.values():
            writer.writerow([
                entry["country_code"],
                entry["city_name"],
                entry["ascii_name"],
                "|".join(sorted(entry["alts"])),
                entry["lat"],
                entry["lon"],
                entry["timezone_id"],
                entry["offset"],
                format_utc_offset(entry["offset"]),
            ])

    print(f"Generated {OUTPUT_CSV} ({len(best):,} unique cities)")


if __name__ == "__main__":
    main()