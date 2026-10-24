import type { components } from '$api/generated/schema';

type Schemas = components['schemas'];

/** One day of a person's diary, as the server adds it up. */
export type DiaryDay = Schemas['UsersDiaryDay'];

/** One thing eaten, with the figures it was logged with. */
export type DiaryEntry = Schemas['UsersDiaryEntryDetail'];

export type DiaryFigures = Schemas['UsersDiaryFiguresDetail'];

export type DiarySettings = Schemas['UsersGetDiarySettingsResponse'];

/** What a new entry says was eaten; the server looks the figures up. */
export type DiaryEntryRequest = Schemas['UsersAddDiaryEntryRequest'];

export type DiaryEntryChange = Schemas['UsersUpdateDiaryEntryRequest'];

export const diaryMeals = ['breakfast', 'lunch', 'dinner', 'snack'] as const;

export type DiaryMeal = (typeof diaryMeals)[number];
