import { http, request, type AppError } from '$api';
import type { components } from '$api/generated/schema';
type WireJob = components['schemas']['RecipesIntakeIntakeJob'];

export type IntakeJob = Omit<WireJob, 'photoCount'> & { photoCount: number };
const normalized = (job: WireJob): IntakeJob => ({ ...job, photoCount: job.photoCount ?? 0 });
export const running = (job: IntakeJob) => !['ready', 'failed', 'reviewed'].includes(job.stage);

class Intakes {
  jobs = $state<IntakeJob[]>([]);
  error = $state<AppError | null>(null);
  submitting = $state(false);
  #loading = false;
  #owner: string | null = null;

  own(userId: string | null): void {
    if (this.#owner === userId) return;
    this.#owner = userId;
    this.jobs = [];
    this.error = null;
  }
  async refresh(): Promise<void> {
    if (this.#loading || !this.#owner) return;
    const owner = this.#owner;
    this.#loading = true;
    try {
      const result = await request(() => http.GET('/api/v1/recipe-intakes'));
      if (owner !== this.#owner) return;
      if (result.ok) {
        this.jobs = result.value.map(normalized);
        this.error = null;
      } else this.error = result.error;
    } finally {
      this.#loading = false;
    }
  }
  async get(id: string): Promise<IntakeJob | null> {
    const owner = this.#owner;
    const result = await request(() =>
      http.GET('/api/v1/recipe-intakes/{id}', { params: { path: { id } } })
    );
    if (owner !== this.#owner) return null;
    if (!result.ok) {
      this.error = result.error;
      return null;
    }
    this.jobs = [normalized(result.value), ...this.jobs.filter((job) => job.id !== id)];
    return normalized(result.value);
  }
  async start(input: {
    id: string;
    householdId: string;
    language: string;
    material: string;
    transcript: string;
    sourceUrl?: string;
    photos: File[];
    fetchSource?: boolean;
  }): Promise<IntakeJob | null> {
    if (this.submitting) return null;
    const owner = this.#owner;
    this.submitting = true;
    this.error = null;
    const body = new FormData();
    body.set('fetchSource', String(input.fetchSource ?? false));
    body.set('material', input.material);
    body.set('transcript', input.transcript);
    if (input.sourceUrl) body.set('sourceUrl', input.sourceUrl);
    input.photos.forEach((photo) => body.append('photos', photo));
    try {
      const result = await request(() =>
        http.POST('/api/v1/recipe-intakes', {
          params: {
            query: { id: input.id, householdId: input.householdId, language: input.language }
          },
          body: body as never,
          bodySerializer: (value) => value as unknown as FormData
        })
      );
      if (owner !== this.#owner) return null;
      if (!result.ok) {
        this.error = result.error;
        return null;
      }
      this.jobs = [
        normalized(result.value),
        ...this.jobs.filter((job) => job.id !== result.value.id)
      ];
      return normalized(result.value);
    } finally {
      this.submitting = false;
    }
  }
  async retry(id: string, nextId: string): Promise<IntakeJob | null> {
    const owner = this.#owner;
    const result = await request(() =>
      http.POST('/api/v1/recipe-intakes/{id}/retry', {
        params: { path: { id }, query: { nextId } }
      })
    );
    if (owner !== this.#owner) return null;
    if (!result.ok) {
      this.error = result.error;
      return null;
    }
    this.jobs = [normalized(result.value), ...this.jobs.filter((job) => job.id !== id)];
    return normalized(result.value);
  }
  async reviewed(id: string): Promise<boolean> {
    const owner = this.#owner;
    const result = await request(() =>
      http.POST('/api/v1/recipe-intakes/{id}/reviewed', { params: { path: { id } } })
    );
    if (owner !== this.#owner) return false;
    if (!result.ok) {
      this.error = result.error;
      return false;
    }
    this.jobs = this.jobs.filter((job) => job.id !== id);
    return true;
  }
}
export const intakes = new Intakes();
