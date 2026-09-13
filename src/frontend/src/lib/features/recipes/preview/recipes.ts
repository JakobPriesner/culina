import { base } from '$app/paths';
import { m } from '$shell/i18n';

export interface PreviewRecipe {
  id: string;
  title: string;
  description: string;
  minutes: number;
  tag: string;
  image?: string;
  ingredients: { name: string; quantity: number; unit: string }[];
  steps: string[];
}

export interface PreviewProgress {
  servings: number;
  currentStep: number;
  cooking: boolean;
  checked: Record<string, boolean>;
}

/** Fictional examples for visual review, never persisted or fetched as household data. */
export function sampleRecipes(): PreviewRecipe[] {
  return [
    {
      id: 'orzo',
      title: m['preview.orzo.title'](),
      description: m['preview.orzo.body'](),
      minutes: 25,
      tag: m['preview.tag.weeknight'](),
      image: `${base}/images/culina-orzo.webp`,
      ingredients: [
        { name: m['preview.ingredient.orzo'](), quantity: 200, unit: 'g' },
        { name: m['preview.ingredient.zucchini'](), quantity: 1, unit: '' },
        { name: m['preview.ingredient.lemon'](), quantity: 1, unit: '' },
        { name: m['preview.ingredient.stock'](), quantity: 400, unit: 'ml' },
        { name: m['preview.ingredient.parmesan'](), quantity: 30, unit: 'g' }
      ],
      steps: [m['preview.orzo.step1'](), m['preview.orzo.step2'](), m['preview.orzo.step3']()]
    },
    {
      id: 'toast',
      title: m['preview.toast.title'](),
      description: m['preview.toast.body'](),
      minutes: 15,
      tag: m['preview.tag.simple'](),
      image: `${base}/images/culina-tomato-toast.webp`,
      ingredients: [
        { name: m['preview.ingredient.bread'](), quantity: 4, unit: '' },
        { name: m['preview.ingredient.tomatoes'](), quantity: 400, unit: 'g' },
        { name: m['preview.ingredient.oil'](), quantity: 20, unit: 'ml' }
      ],
      steps: [m['preview.toast.step1'](), m['preview.toast.step2']()]
    },
    {
      id: 'rice',
      title: m['preview.rice.title'](),
      description: m['preview.rice.body'](),
      minutes: 40,
      tag: m['preview.tag.slow'](),
      image: `${base}/images/culina-miso-rice.webp`,
      ingredients: [
        { name: m['preview.ingredient.rice'](), quantity: 200, unit: 'g' },
        { name: m['preview.ingredient.mushrooms'](), quantity: 250, unit: 'g' },
        { name: m['preview.ingredient.miso'](), quantity: 20, unit: 'g' }
      ],
      steps: [m['preview.rice.step1'](), m['preview.rice.step2']()]
    }
  ];
}
