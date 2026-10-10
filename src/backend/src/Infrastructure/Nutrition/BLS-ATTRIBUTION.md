# Bundeslebensmittelschlüssel (BLS) 4.0

`bls.tsv` is an extract of the German federal food composition table:

> Max Rubner-Institut (2025): Bundeslebensmittelschlüssel (BLS), Version 4.0 —
> Deutsche Nährstoffdatenbank. Karlsruhe. DOI: 10.25826/Data20251217-134202-0

Licence: [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/), from
<https://blsdb.de/download>. Redistribution and modification are allowed with
attribution, which Culina gives here and wherever it shows a value from the
table (the nutrition panel on a recipe).

## What was taken, and what was changed

From `BLS_4_0_Daten_2025_DE.xlsx`, per food: the BLS code, the German and the
English name, and per 100 g edible portion (BLS documentation §7.2: the inedible
part, a shell or a stone, is already taken out) the seven values of the EU food
label: energy in kJ (`ENERCJ`) and kcal (`ENERCC`), fat (`FAT`), saturated fat
(`FASAT`), available carbohydrate (`CHO`), sugars (`SUGAR`), protein
(`PROT625`) and salt (`NACL`). Nothing else: not the other 130 components and
not the provenance columns.

Changes, all mechanical:

- Numbers are rounded to three decimal places.
- A value the BLS marks `-` (missing; "nicht als Null interpretieren") stays
  `-`, and Culina reads it as unknown, never as zero.
- A value marked as a trace (`TR`) or below the limit of detection or
  quantification (`<LOD`, `<LOQ`) is written `tr`. Culina counts it as zero:
  the nutrient is present in an amount too small to measure, and counting it as
  zero keeps every sum a true lower bound.

## Updating it

When the MRI publishes a new version, download the workbook from
<https://blsdb.de/download> and run:

```bash
python3 scripts/extract-bls.py BLS_4_0_Daten_2025_DE.xlsx > src/backend/src/Infrastructure/Nutrition/bls.tsv
```

Then raise `NutritionData.Version`, so no client keeps a nutrition answer from
before the new data, and run the nutrition tests: they pin the row count and
check that every code the name table points at still exists.
