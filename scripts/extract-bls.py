#!/usr/bin/env python3
"""Extract the seven EU label values from the Bundeslebensmittelschlüssel (BLS) workbook.

Usage:
    python3 scripts/extract-bls.py BLS_4_0_Daten_2025_DE.xlsx \
        > src/backend/src/Infrastructure/Nutrition/bls.tsv

The workbook comes from https://blsdb.de/download (Max Rubner-Institut, CC BY 4.0). It is
14 MB and is not committed; this script is the repeatable step from it to the committed
extract, run when the MRI publishes a new version. Standard library only, so it adds no
dependency to the repository.

Per food it keeps the BLS code, the German and English name, and per 100 g edible portion:
energy (kJ, kcal), fat, saturated fat, carbohydrate, sugars, protein and salt. A value the
BLS marks '-' (missing, "nicht als Null interpretieren") stays '-'. A value it marks as a
trace ('TR') or below the limit of detection or quantification ('<LOD', '<LOQ') is
written 'tr': present, too little to measure.
"""

import re
import sys
import zipfile
import xml.etree.ElementTree as ET

NS = '{http://schemas.openxmlformats.org/spreadsheetml/2006/main}'

# Output column, and the component code its header starts with.
COLUMNS = [
    ('energy_kj', 'ENERCJ'),
    ('energy_kcal', 'ENERCC'),
    ('fat', 'FAT'),
    ('saturated_fat', 'FASAT'),
    ('carbohydrate', 'CHO'),
    ('sugars', 'SUGAR'),
    ('protein', 'PROT625'),
    ('salt', 'NACL'),
]

TRACE = re.compile(r'^(TR|<LOD|<LOQ|<LOD or <LOQ)$')


def column_index(reference):
    number = 0
    for letter in re.match(r'[A-Z]+', reference).group():
        number = number * 26 + ord(letter) - 64
    return number - 1


def rows(path):
    workbook = zipfile.ZipFile(path)
    shared = [
        ''.join(text.text or '' for text in item.iter(NS + 't'))
        for item in ET.fromstring(workbook.read('xl/sharedStrings.xml')).findall(NS + 'si')
    ]
    for _, element in ET.iterparse(workbook.open('xl/worksheets/sheet1.xml')):
        if not element.tag.endswith('}row'):
            continue
        values = {}
        for cell in element.findall(NS + 'c'):
            value = cell.find(NS + 'v')
            if value is not None:
                values[column_index(cell.get('r'))] = (
                    shared[int(value.text)] if cell.get('t') == 's' else value.text
                )
        yield values
        element.clear()


def number(raw):
    raw = raw.strip()
    if raw == '-':
        return '-'
    if TRACE.match(raw):
        return 'tr'
    text = f'{float(raw):.3f}'.rstrip('0').rstrip('.')
    return '0' if text == '-0' else text


def main(path):
    found = rows(path)
    header = next(found)

    def find(code):
        matches = [
            index for index, title in header.items()
            if title.startswith(code + ' ') and '[' in title
        ]
        if len(matches) != 1:
            sys.exit(f'Expected one value column for {code}, found {len(matches)}.')
        return matches[0]

    indexes = [find(code) for _, code in COLUMNS]
    out = sys.stdout
    out.write('\t'.join(['code', 'name_de', 'name_en'] + [name for name, _ in COLUMNS]) + '\n')

    for values in found:
        code = values.get(0, '').strip()
        if not code:
            continue
        names = [values.get(1, '').strip(), values.get(2, '').strip()]
        if any('\t' in name or '\n' in name for name in names):
            sys.exit(f'{code}: a name contains a tab or a line break.')
        out.write('\t'.join([code] + names + [number(values.get(index, '-')) for index in indexes]) + '\n')


if __name__ == '__main__':
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    main(sys.argv[1])
