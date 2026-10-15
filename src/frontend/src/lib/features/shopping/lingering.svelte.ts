/**
 * Lines just ticked, left where they were for a moment, struck through: in a shop the next line would
 * otherwise slide under the thumb that ticked this one, and a quick second tap ticks the wrong thing.
 */
export function createLingering(ms = 1200) {
  let ids = $state<readonly string[]>([]);

  return {
    get ids(): readonly string[] {
      return ids;
    },

    hold(id: string): void {
      ids = [...ids, id];
      setTimeout(() => {
        ids = ids.filter((held) => held !== id);
      }, ms);
    }
  };
}
