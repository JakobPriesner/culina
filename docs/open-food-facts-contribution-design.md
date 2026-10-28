# Contributing products to Open Food Facts (design)

Status: decided, not built, 28 October 2026 (decisions in §3). Follows the lookup built in
`barcode-scanning-design.md` §5.2. Bead: `culina-v2-eawy.12`.

**The idea.** When someone scans a package that Open Food Facts (OFF) does not
know, they type its label into Culina once. That label could also go to OFF, so
the next person anywhere finds the product. Culina would then give something
back for what it takes from OFF.

## 1. What OFF expects (checked 28 October 2026)

| Topic | What OFF says |
| --- | --- |
| Endpoint | `POST /cgi/product_jqm2.pl` as a form. A v3 write API is in progress and not documented for products yet. |
| Fields | `code`, `lc`/`cc`, `product_name_de`, `add_brands`, `quantity`, `nutrition_data_per=100g`, `nutriment_<name>` + `nutriment_<name>_unit`, `no_nutrition_data=on` |
| Overwriting | A plain field replaces what is there. `add_` appends. Only send plain fields after showing the person what OFF already holds. |
| Authentication | `user_id` + `password` per write, or a session cookie (same IP, at most 10 sessions). No OAuth yet: it is planned with Keycloak. |
| Apps writing for their users | One app account. Every write carries `app_name`, `app_version` and `app_uuid`, a salted random id per person, so moderators can ban one person and not the app. `comment` may carry a non-personal id. |
| Limits | No limit on writes. Queue them, drop one only after OFF confirms it, and retry network errors, 429 and 5xx with backoff and jitter. Never retry another 4xx. |
| Test server | `https://world.openfoodfacts.net`: basic auth `off:off` and its own accounts. OFF asks for all testing there. |
| Terms | Contributions are irrevocably under DbCL 1.0 (photos CC BY-SA) and public, as is who made them. Data must come "directly from the label and packaging". Nothing personal. |
| Wanted most | Photos of the front, the nutrition table and the ingredients. Name, category and quantity help OFF compute scores. |

Sources: [OFF API introduction](https://openfoodfacts.github.io/openfoodfacts-server/api/),
[Adding missing products](https://openfoodfacts.github.io/documentation/docs/Product-Opener/api/tutorials/adding-missing-products/),
[API cheatsheet](https://openfoodfacts.github.io/openfoodfacts-server/api/ref-cheatsheet/),
[Terms of use](https://world.openfoodfacts.org/terms-of-use).
Prior art: OpenNutriTracker PR #113 (merged 19 October 2026). It uses each
user's own OFF account, opts in per upload with a preview, defaults to the test
server, and skips products that already have values.

**Not verified:** OFF documents the energy field as `nutriment_energy` with
`_unit=kJ`. It does not document `nutriment_energy-kcal` for writing. Find out
which works on the test server before building on it.

## 2. Proposal

### 2.1 What is sent, and when

- **Only products OFF did not know**: the diary's "Open Food Facts kennt diesen
  Strichcode nicht" path (`DiaryProduct` label phase with no `offered`).
  Corrections to an existing OFF product (`openFoodFactsEdited`) are **not**
  sent in the first version. A plain field would overwrite somebody else's
  values, and the person typed a correction, not necessarily the whole label.
- **Fields:** `code`, `lc=de`, `cc=de`, `product_name_de`, `add_brands`
  (optional), `quantity` (optional), `nutrition_data_per=100g`, and each value
  the person typed with its unit (kcal and kJ, fat, saturated fat,
  carbohydrate, sugars, protein, salt, all in g). A value left empty is
  **not** sent. Empty means unknown, as everywhere in Culina.
- The label form gains two optional fields for an unknown product only:
  **Marke** and **Packungsgröße**. Both help OFF and the household's product
  memory, and neither is needed to log the food.
- **Photos in the first version** (decision 4): the label form for an unknown
  product offers two photos, **front** and **nutrition table**. The viewfinder
  already runs, so a still is taken from it, or a picture is chosen on a
  laptop. They are uploaded with the product (`imagefield=front_de` /
  `nutrition_de`; take the exact upload endpoint and fields from OFF's image
  tutorial on the test server). Both photos are optional: values without
  photos are still sent. Photos go under CC BY-SA and must be the person's own,
  which a photo taken in the sheet is. Culina keeps no copy once OFF has
  accepted them.

### 2.2 Whose name it goes in

**Recommended: one OFF account per Culina instance**, entered by the
administrator next to the lookup switch. Each write carries
`app_name=Culina`, `app_version`, and `app_uuid = HMAC(instance secret,
user id)`: stable per person, meaningless outside the instance, and nothing
personal. The password is stored with `ISecretProtector`, like the AI
providers' keys.

- *Alternative:* each person's own OFF account, as OpenNutriTracker does. The
  contribution is then credited to them, but every person needs an OFF account
  and Culina has to keep their OFF password. Worth adding later as an option
  ("Unter meinem eigenen Konto beitragen").
- When OFF ships OAuth (Keycloak), the per-person account becomes the clean
  path, and Culina never sees the password.

### 2.3 Consent

Contributing is public and cannot be taken back, so:

1. **The administrator turns contributing on** (off by default) and enters the
   app account. Without both, Culina sends nothing and offers nothing. The
   setting says plainly that the whole instance then contributes every new
   product typed from a label.
2. **Then every unknown product is sent, without asking each time**
   (decision 2). There is no checkbox. The label form for an unknown product
   still says it, in one line above the save button: "Wird auch an Open Food
   Facts geschickt: öffentlich, dauerhaft und unter freier Lizenz. Trag nur ein,
   was so auf der Packung steht." Nobody should learn afterwards that what they
   typed is public.
3. Values the person changed after a doubt (`energyMismatch`, …) are the
   person's own reading of the label, so they are sent like any other.

### 2.4 Delivery

- The diary entry and the household food are saved **first**, as today.
  Contributing never delays or fails a log.
- The contribution goes into a server-side outbox (`product_contributions`:
  gtin, fields, user, state, attempts, next attempt, OFF's answer). A worker
  sends it, retries 429/5xx and network errors with backoff and jitter up to a
  day, and stops on any other 4xx.
- **Before sending, the worker reads the product at OFF again.** If somebody
  added it meanwhile, it sends nothing (state `alreadyThere`). It never
  overwrites.
- On success, the server's lookup cache drops its "not known" row for the code,
  so the next lookup asks OFF and finds the product.
- The person sees the outcome where the food is: on the household foods list,
  "an Open Food Facts geschickt", with a link to the product page, or "nicht
  angenommen" with OFF's reason.

### 2.5 Test server first

The administrator setting has a target: **Test server** (`.net`, the default
until someone switches it) or **Open Food Facts**. The test server needs its
own account and basic auth `off:off`. Integration tests use a scripted
handler, as for the lookup, and never reach either server.

## 3. Decisions (Jakob, 28 October 2026)

1. **Account:** one OFF app account per instance, entered by the
   administrator, with `app_name` and a salted `app_uuid` per person.
2. **Consent:** always send once the administrator enabled contributing, with
   no per-product checkbox. The form says so in one line (§2.3).
3. **Corrections:** new products only. Corrections of products OFF already
   has are not sent.
4. **Photos:** in the first version, front and nutrition table, both optional
   (§2.1).

## 4. Build outline (after the decisions)

1. On the test server by hand: post one product with kcal and kJ, and settle
   the energy field names (§1, not verified).
2. Settings: `contribute`, `target`, app account user and secret password
   (instance settings group `products`). Admin UI next to the lookup switch.
3. Domain: `ProductContribution` from a household food and the optional brand
   and quantity. A pure mapping to OFF's form fields, unit-tested.
4. Infrastructure: migration for `product_contributions`, the outbox worker, and
   the OFF client's write with basic auth on the test server. Scripted-handler
   integration tests: sent once, retried on 503, given up on 400, skipped when
   already there, cache row dropped.
5. Photos: a still from the viewfinder (or a chosen file), kept in the outbox
   until OFF accepts it, then deleted. Upload after the product write
   succeeded, with the same retry rules.
6. Frontend: brand, pack size and the two photos plus the one-line notice in
   the label form (unknown products only, and only when contributing is on),
   and the state on the household foods list.
7. Docs: README ("Culina contributes only when the administrator and the
   person agree"), operations, and this file's "As built".
