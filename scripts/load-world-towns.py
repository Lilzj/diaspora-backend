"""Write the SQL that loads the world list of towns (GeoNames cities1000) into location.world_towns.

Owner, 5 Nov 2026: "3 bedroom flat in Grand Baie", "where to braid my hair in Port Louis". The app
reads the place out of a search and the API looks it up in this list (GET
/api/v1/location/towns/match), without any AI. The platform is used everywhere, so the list is the
whole world's: every populated place of 1,000 people or more, about 150,000 towns. A place the list
does not have falls back to the app's Google town search.

GeoNames data is CC BY 4.0 (https://www.geonames.org/), credited on the app's About screen.

Usage (standard library only):
  python -I scripts/load-world-towns.py --download <an empty folder>
      fetches https://download.geonames.org/export/dump/cities1000.zip into it
  python -I scripts/load-world-towns.py --source <cities1000.zip or .txt> --out world-towns.sql
  psql "<connection string>" -v ON_ERROR_STOP=1 -f world-towns.sql
  python -I scripts/load-world-towns.py --self-test

The SQL runs in one transaction. A town already there (same GeoNames id) is updated and a new one is
added. Nothing is deleted, so it can be run again after GeoNames refreshes its list.

The names are written and kept by the same rules as TownNames and WorldTown.Create
(src/Diaspora.Modules.Location/Domain/Towns). Change both together; --self-test checks the same
examples as tests/Diaspora.UnitTests/Location/TownNamesTests.cs.
"""

import argparse
import datetime
import io
import os
import sys
import unicodedata
import urllib.request
import zipfile

SOURCE_URL = "https://download.geonames.org/export/dump/cities1000.zip"
SOURCE_MEMBER = "cities1000.txt"

# TownNames
MAX_NAMES_PER_TOWN = 8
BIG_CITY_POPULATION = 100_000
MAX_NAMES_PER_BIG_CITY = 32
MAX_NAME_LENGTH = 60
MAX_SEARCH_NAMES_LENGTH = 1000

# LocationPersistenceConstants
TOWN_NAME_LENGTH = 200

# char.IsLetterOrDigit
LETTER_OR_DIGIT = {"Lu", "Ll", "Lt", "Lm", "Lo", "Nd"}

# The GeoNames columns used (https://download.geonames.org/export/dump/readme.txt)
GEONAME_ID, NAME, ASCII_NAME, ALTERNATE_NAMES, LATITUDE, LONGITUDE = 0, 1, 2, 3, 4, 5
COUNTRY_CODE, POPULATION = 8, 14
COLUMNS = 19


def normalize(text):
    """TownNames.Normalize: accents off, lower case, anything else one space."""
    if not text or not text.strip():
        return ""

    out = []
    last_was_space = True

    for character in unicodedata.normalize("NFD", text):
        category = unicodedata.category(character)

        if category == "Mn":
            continue

        if category in LETTER_OR_DIGIT:
            out.append(character.lower())
            last_was_space = False
        elif not last_was_space:
            out.append(" ")
            last_was_space = True

    return "".join(out).strip()


def is_searchable(normalized):
    """TownNames.IsSearchable: plain letters a member can type, and short."""
    return 2 <= len(normalized) <= MAX_NAME_LENGTH and all(
        "a" <= character <= "z" or "0" <= character <= "9" or character == " " for character in normalized
    )


def is_code(spelling):
    """TownNames.IsCode: an airport or postal code, four capital letters or fewer."""
    trimmed = (spelling or "").strip()
    return 0 < len(trimmed) <= 4 and all("A" <= character <= "Z" for character in trimmed)


def max_names_for(population):
    """TownNames.MaxNamesFor."""
    return MAX_NAMES_PER_BIG_CITY if population >= BIG_CITY_POPULATION else MAX_NAMES_PER_TOWN


def search_names(name, other_names, population):
    """WorldTown.Create: the town's names between bars, its own first, no repeats."""
    names = []
    seen = set()
    length = 1
    room = max_names_for(population)

    for spelling in [name] + [other for other in other_names if not is_code(other)]:
        normalized = normalize(spelling)

        if not is_searchable(normalized) or normalized in seen:
            continue

        seen.add(normalized)

        if len(names) == room or length + len(normalized) + 1 > MAX_SEARCH_NAMES_LENGTH:
            break

        names.append(normalized)
        length += len(normalized) + 1

    return "|" + "|".join(names) + "|"


def read_towns(lines):
    """One row per town: (geoname id, name, country, latitude, longitude, population, search names)."""
    seen = set()

    for line in lines:
        columns = line.rstrip("\r\n").split("\t")

        if len(columns) != COLUMNS:
            continue

        country = columns[COUNTRY_CODE].strip().upper()

        if len(country) != 2 or not country.isascii() or not country.isalpha():
            continue

        try:
            geoname_id = int(columns[GEONAME_ID])
            latitude = float(columns[LATITUDE])
            longitude = float(columns[LONGITUDE])
            population = max(0, int(columns[POPULATION] or 0))
        except ValueError:
            continue

        name = columns[NAME].strip()[:TOWN_NAME_LENGTH]

        if not name or geoname_id in seen:
            continue

        # Its plain-letter spelling comes right after its own name, then the other spellings.
        other_names = [columns[ASCII_NAME]] + [other for other in columns[ALTERNATE_NAMES].split(",") if other]
        names = search_names(name, other_names, population)

        if names == "||":
            continue

        seen.add(geoname_id)
        yield geoname_id, name, country, latitude, longitude, population, names


def copy_text(value):
    """A value in COPY's text format."""
    return (
        str(value)
        .replace("\\", "\\\\")
        .replace("\t", "\\t")
        .replace("\n", "\\n")
        .replace("\r", "\\r")
    )


def write_sql(towns, out):
    columns = '"GeoNameId", "Name", "CountryCode", "Latitude", "Longitude", "Population", "SearchNames"'
    written = 0

    out.write(
        "-- The world list of towns, from GeoNames cities1000 (CC BY 4.0, https://www.geonames.org/).\n"
        f"-- Written by scripts/load-world-towns.py on {datetime.date.today().isoformat()}.\n"
        '-- Apply with: psql "<connection string>" -v ON_ERROR_STOP=1 -f <this file>\n'
        "\\set ON_ERROR_STOP on\n"
        "BEGIN;\n"
        "CREATE TEMP TABLE world_towns_load (\n"
        '    "GeoNameId" integer NOT NULL,\n'
        '    "Name" character varying(200) NOT NULL,\n'
        '    "CountryCode" character varying(2) NOT NULL,\n'
        '    "Latitude" double precision NOT NULL,\n'
        '    "Longitude" double precision NOT NULL,\n'
        '    "Population" bigint NOT NULL,\n'
        '    "SearchNames" character varying(1000) NOT NULL\n'
        ") ON COMMIT DROP;\n"
        f"COPY world_towns_load ({columns}) FROM STDIN;\n"
    )

    for town in towns:
        out.write("\t".join(copy_text(value) for value in town) + "\n")
        written += 1

    out.write(
        "\\.\n"
        f'INSERT INTO location.world_towns ("Id", {columns})\n'
        f"SELECT gen_random_uuid(), {columns}\n"
        "FROM world_towns_load\n"
        'ON CONFLICT ("GeoNameId") DO UPDATE SET\n'
        '    "Name" = EXCLUDED."Name",\n'
        '    "CountryCode" = EXCLUDED."CountryCode",\n'
        '    "Latitude" = EXCLUDED."Latitude",\n'
        '    "Longitude" = EXCLUDED."Longitude",\n'
        '    "Population" = EXCLUDED."Population",\n'
        '    "SearchNames" = EXCLUDED."SearchNames";\n'
        "ANALYZE location.world_towns;\n"
        "COMMIT;\n"
    )

    return written


def open_source(path):
    if path.lower().endswith(".zip"):
        archive = zipfile.ZipFile(path)
        return io.TextIOWrapper(archive.open(SOURCE_MEMBER), encoding="utf-8", newline="")

    return open(path, encoding="utf-8", newline="")


def download(folder):
    os.makedirs(folder, exist_ok=True)

    if os.listdir(folder):
        sys.exit(f"{folder} is not empty; give an empty folder for the download.")

    target = os.path.join(folder, "cities1000.zip")
    request = urllib.request.Request(SOURCE_URL, headers={"User-Agent": "diaspora-load-world-towns"})

    with urllib.request.urlopen(request, timeout=120) as response, open(target, "wb") as file:
        while chunk := response.read(1 << 16):
            file.write(chunk)

    print(f"{target}: {os.path.getsize(target):,} bytes")


def self_test():
    """The examples of TownNamesTests, so the two sides keep to one rule."""
    assert normalize("Mahébourg") == "mahebourg"
    assert normalize("Grand-Baie") == "grand baie"
    assert normalize("  PORT   Louis ") == "port louis"
    assert normalize("Saint-Pierre (Réunion)") == "saint pierre reunion"
    assert normalize(None) == ""

    assert is_searchable("grand baie")
    assert not is_searchable("порт луи")
    assert not is_searchable("x")

    assert search_names("Grand Baie", ["Grand Bay", "grand-baie", "Гран-Бе"], 11_000) == "|grand baie|grand bay|"
    assert search_names("Port Louis", ["PLU", "MRU", "Port-Louis", "Port Lewis"], 150_000) == "|port louis|port lewis|"

    spellings = [
        "Lakana", "Landan", "Landen", "Ljondan", "Llundain", "Lodoni", "Londain", "Londan", "Londar",
        "Londe", "Londen", "Londinium", "Londino", "Londis", "Londona", "Londonas", "Londoni", "Londra", "Londres",
    ]
    assert "|londres|" in search_names("London", spellings, 8_900_000)
    assert search_names("Londontown", spellings, 8_000).count("|") == 8 + 1

    long_names = [f"{'a' * 50} {index:02d}" for index in range(40)]
    capped = search_names("Bigtown", long_names, 5_000_000)
    assert len(capped) <= MAX_SEARCH_NAMES_LENGTH and capped.startswith("|bigtown|") and capped.endswith("|")

    line = "\t".join([
        "934154", "Grand Baie", "Grand Baie", "Grand Bay,Grand-Baie,GBA", "-20.01639", "57.58028", "P", "PPL",
        "MU", "", "15", "", "", "", "11000", "", "6", "Indian/Mauritius", "2019-01-15",
    ])
    [town] = list(read_towns([line + "\n", "too\tshort\n"]))
    assert town == (934154, "Grand Baie", "MU", -20.01639, 57.58028, 11000, "|grand baie|grand bay|"), town

    out = io.StringIO()
    assert write_sql([town, (1, "Tab\tTown", "XX", 0.0, 0.0, 0, "|tab town|")], out) == 2
    sql = out.getvalue()
    assert "934154\tGrand Baie\tMU\t-20.01639\t57.58028\t11000\t|grand baie|grand bay|\n" in sql
    assert "Tab\\tTown" in sql and sql.rstrip().endswith("COMMIT;")

    print("self-test passed")


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--download", metavar="FOLDER", help="fetch cities1000.zip into an empty folder")
    parser.add_argument("--source", help="cities1000.zip or cities1000.txt")
    parser.add_argument("--out", help="the SQL file to write")
    parser.add_argument("--self-test", action="store_true")
    arguments = parser.parse_args()

    if arguments.self_test:
        self_test()
    elif arguments.download:
        download(arguments.download)
    elif arguments.source and arguments.out:
        with open_source(arguments.source) as source, open(arguments.out, "w", encoding="utf-8", newline="\n") as out:
            written = write_sql(read_towns(source), out)
        print(f"{arguments.out}: {written:,} towns")
    else:
        parser.error("give --self-test, --download FOLDER, or --source and --out")


if __name__ == "__main__":
    main()
