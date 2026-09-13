# Culina design images

`src/frontend/static/images/culina-orzo.webp` is an original image generated for the September 2026 design revision using the built-in image-generation tool. It is an illustrative sample, not a photograph of a tested recipe. Its prototype recipe is fictional. It is used decoratively in authentication and with a descriptive label in the development preview.

The generated 1536 × 1024 PNG was encoded as WebP at quality 82 with `cwebp`; there is no third-party image hotlink or font request. Keep a usable image fallback when actual household photography is implemented.

Prompt:

> Use case: photorealistic-natural. Asset: editorial food photography for Culina, a refined household recipe app. Create one landscape 3:2 photograph, overhead near-top-down view of a handmade wide ivory ceramic bowl of lemon orzo with roasted zucchini and fresh basil, glossy tiny orzo, freshly grated parmesan, black pepper, small irregular zucchini rounds with real browned edges. Entire bowl visible centered, comfortably cropped for square and portrait responsive formats. Warm pale limestone tabletop, a cut lemon and a casually folded natural oatmeal linen napkin at the upper left edge, silver fork on the right. Natural side window light, gentle afternoon shadows, authentic home-cooked appetizing textures. Contemporary independent food magazine photography, understated, candid tactile realism, no oversaturated yellow tint, no text, no typography, no logos, no graphic elements. The food is the hero, simple background, high detail.

## Collection photographs — 13 September 2026

The development preview adds two original generated images, encoded from 1536 × 1024 PNGs with `cwebp -q 82`. The source PNGs remain in the generation output directory. These are illustrative fictional recipes, not tested cooking instructions.

- `src/frontend/static/images/culina-tomato-toast.webp` — generated source `exec-6517e653-dc13-4cfb-81b0-99dc5cd415da.png`.
- `src/frontend/static/images/culina-miso-rice.webp` — generated source `exec-9bb91f5c-c23f-46e8-b62e-3ddae063b135.png`.

Toast prompt:

> Use case: photorealistic-natural. Asset: original sample food photograph for the Culina recipe app. Landscape 3:2 editorial photograph from overhead of three slices of toasted sourdough topped with ripe chopped red and golden tomatoes and glistening olive oil, black pepper and a very few basil leaves. Handmade off-white ceramic plate, warm pale limestone table, a relaxed oatmeal linen napkin at the edge, softly lit by a window. True-to-life appetizing food textures, restrained contemporary food magazine styling, natural shadows. The entire plate centered and comfortably framed to crop to 4:3 or square. Palette warm neutral, rich natural tomato red, no orange color cast. No people, no text, no typography, no logos.

Rice prompt:

> Use case: photorealistic-natural. Asset: original sample food photograph for the Culina recipe app. Landscape 3:2 editorial photograph from overhead of a shallow handmade off-white ceramic bowl of rice with beautifully seared brown mushrooms glazed lightly with miso, a few finely cut green spring onion pieces. Bowl centered on a warm pale limestone table with a casually placed silver spoon and oatmeal linen at an edge. Quiet natural window light with soft shadows, true-to-life deeply appetizing textures, refined contemporary food magazine photograph. The whole bowl comfortably framed to crop to 4:3 or square. Restrained warm-neutral color palette, no orange color cast. No people, no text, no typography, no logos.

Collection and detail images use empty alternative text because the adjacent heading identifies the recipe. The featured photo has a descriptive label. The shared Image primitive reserves space before loading and renders a bowl illustration when loading fails; a meaningful alternative description remains accessible.
