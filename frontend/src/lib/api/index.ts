import { mockApi } from './mock';
import { liveApi } from './live';
import type { ApiClient } from './types';

/** VITE_API_MODE=router for the Docker demo, mock for offline development. */
export const api: ApiClient = import.meta.env.VITE_API_MODE === 'router' ? liveApi : mockApi;
export type * from './types';
