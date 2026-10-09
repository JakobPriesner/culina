import { http, request, watch, type AppError, type Stream } from '$api';
import type { components } from '$api/generated/schema';
type WireJob = components['schemas']['RecipesIntakeIntakeJob'];
type WireEvent = components['schemas']['RecipesIntakeIntakeEvent'];

export type IntakeJob = Omit<WireJob, 'photoCount'> & { photoCount: number };
const normalized = (job: WireJob): IntakeJob => ({ ...job, photoCount: job.photoCount ?? 0 });
export const running = (job: IntakeJob) => !['ready', 'failed', 'reviewed'].includes(job.stage);

class Intakes {
  jobs = $state<IntakeJob[]>([]);
  error = $state<AppError | null>(null);
  submitting = $state(false);
  #owner: string | null = null;
  /** Not $state: nothing renders it. */
  #stream: Stream | null = null;

  own(userId: string | null): void {
    if (this.#owner === userId) return;
    this.stop();
    this.#owner = userId;
    this.jobs = [];
    this.error = null;
  }
  /** Keeps the list current from the server's stream; every (re)connect starts with a full snapshot. No-op while open. */
  follow(): void {
    if (this.#stream || !this.#owner) return;
    const owner = this.#owner;
    this.#stream = watch<WireEvent>('/api/v1/recipe-intakes/events', {
      message: (event) => {
        if (owner === this.#owner) this.#apply(event);
      },
      failed: (error) => {
        // Only the retries are spent: a later follow() opens it again.
        if (owner !== this.#owner) return;
        this.#stream = null;
        this.error = error;
      }
    });
  }
  stop(): void {
    this.#stream?.close();
    this.#stream = null;
  }
  #apply(event: WireEvent): void {
    this.error = null;
    if (event.snapshot) {
      this.jobs = event.jobs.map(normalized);
      return;
    }
    let jobs = this.jobs;
    for (const wire of event.jobs) {
      const job = normalized(wire);
      const known = jobs.some((one) => one.id === job.id);
      if (job.stage === 'reviewed') jobs = jobs.filter((one) => one.id !== job.id);
      else if (known) jobs = jobs.map((one) => (one.id === job.id ? job : one));
      else jobs = [job, ...jobs];
    }
    if (event.jobs.length > 0) this.jobs = jobs;
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
