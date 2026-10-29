# Recipe Scaling & Unit Conversion Rules

Specification for portion scaling (`src/backend/Culina.Domain/Services/QuantityFormatter.cs`, `IngredientAggregator.cs`, and `src/frontend/src/lib/utils/scaling.ts`).

---

## 1. Portion Bounds & Scale Factor

- **Default servings:** `4` (configurable per recipe from `1` to `100`).
- **Interactive scaling range:** `0.25` to `50` portions (`scaleFactor = targetServings / baseServings`).
- **Non-scalable ingredients:** If `RecipeIngredient.Scalable == false` (e.g., `"Öl zum Anbraten"`, `"1 Prise Salz zum Abschmecken"`), the base amount is preserved unchanged regardless of `scaleFactor`.
- **Ranges (`Amount` – `AmountMax`):** Both bounds scale by the same factor and format as `"{min}–{max} {unit}"` (e.g., `100–150 g` at `2×` → `200–300 g`).

---

## 2. Non-Linear Dampening for Potent Spices & Leaveners

When scaling beyond `2×` (`scaleFactor > 2.0`), potent seasonings (`Chili`, `Cayennepfeffer`, `Muskat`, `Nelken`, `Kreuzkümmel`) and chemical leaveners (`Backpulver`, `Natron`, `Trockenhefe`, `Frischhefe`) do not scale linearly in real kitchens.

- **Standard ingredients:** $\text{scaled} = \text{amount} \times s$
- **Dampened potent spices (optional cooking-mode hint):** $\text{effective} = \text{amount} \times s^{0.75}$ when $s > 2.0$, surfaced as a subtle culinary hint while keeping shopping list quantities exact.

---

## 3. Unit Auto-Upgrades & Down-Upgrades

Quantities automatically shift within their measurement family to stay readable:

| Family                   | Rule                                                                                                       | Example                                              |
| ------------------------ | ---------------------------------------------------------------------------------------------------------- | ---------------------------------------------------- |
| **Mass (`g` ↔ `kg`)**    | `>= 1000 g` converts to `kg` (up to 2 decimals); `< 1 kg` when scaled down below `1.0 kg` converts to `g`. | `750 g` × `2` → `1,5 kg`; `0,5 kg` × `0.5` → `250 g` |
| **Volume (`ml` ↔ `l`)**  | `>= 1000 ml` converts to `l`; `< 1 l` when scaled down below `1.0 l` converts to `ml`.                     | `600 ml` × `2` → `1,2 l`                             |
| **Spoons (`TL` ↔ `EL`)** | `1 EL = 3 TL = 15 ml`. Sub-quarter teaspoons (`< 0.25 TL`) display as `1 Msp.` or `1 Prise`.               | `1,5 TL` × `2` → `1 EL`                              |

---

## 4. Culinary Rounding & Vulgar Fractions (`QuantityFormatter`)

Raw floating-point results (`0.3333333`, `142.857 g`) are snapped to kitchen-friendly increments:

1. **Gram & Millilitre Precision Tiers:**
    - `< 10 g/ml`: round to nearest `0.5` (`3,5 g`).
    - `10 – 100 g/ml`: round to nearest `1` (`45 g`).
    - `100 – 500 g/ml`: round to nearest `5` (`145 g`).
    - `> 500 g/ml`: round to nearest `10` (`680 g`).
2. **Count & Spoon Vulgar Fractions (`Stück`, `TL`, `EL`, `Tasse`, `Dose`, `Bund`):**
    - Fractional parts snap within `±0.06` tolerance to Unicode fractions:
        - `0.125` → `⅛`, `0.25` → `¼`, `0.333` → `⅓`, `0.5` → `½`, `0.666` → `⅔`, `0.75` → `¾`.
    - Values like `1.5 Stück` render as `1 ½ Stück`; `0.5 TL` renders as `½ TL`.
3. **German Decimal Formatting:** Numbers that do not use vulgar fractions format with a comma decimal separator (`1,25 kg`, `0,75 l`).

---

## 5. Baking Pan & Springform Conversion

In baking recipes, scaling by pan geometry uses surface area ratios ($A_{\text{target}} / A_{\text{source}}$):

- **Round springform ($d_1 \to d_2$):** $\text{factor} = (d_2 / d_1)^2$ (e.g., $\varnothing\,26\,\text{cm} \to \varnothing\,20\,\text{cm} = 0.59\times$).
- **Rectangular sheet ($w_1 \times l_1 \to w_2 \times l_2$):** $\text{factor} = (w_2 \cdot l_2) / (w_1 \cdot l_1)$.
