export type KitchenLighting = 'normal' | 'glare' | 'oled';
export function createKitchenLighting() {
  let mode = $state<KitchenLighting>('normal');
  return {
    get mode() {
      return mode;
    },
    load() {
      try {
        const stored = localStorage.getItem('culina.kitchenLighting');
        if (stored === 'glare' || stored === 'oled' || stored === 'normal') mode = stored;
      } catch {
        /* Optional. */
      }
    },
    choose(value: string) {
      if (value !== 'normal' && value !== 'glare' && value !== 'oled') return;
      mode = value;
      try {
        localStorage.setItem('culina.kitchenLighting', value);
      } catch {
        /* Optional. */
      }
    }
  };
}
export const kitchenLighting = createKitchenLighting();
