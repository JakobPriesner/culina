import { redirect } from '@sveltejs/kit';

import { resolve } from '$app/paths';
import { native } from '$api';

/** Only the app chooses a server: the website is its server. */
export const load = () => {
  if (!native) {
    redirect(307, resolve('/(auth)/login'));
  }
};
