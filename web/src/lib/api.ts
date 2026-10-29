/** RFC 7807 problem returned by the YeaFlow API. SAP error codes arrive in `code`. */
export interface Problem {
  title: string;
  status: number;
  detail?: string;
  code?: string;
  correlationId?: string;
  errors?: Record<string, string[]>;
}

export class ApiError extends Error {
  constructor(public readonly problem: Problem) {
    super(problem.detail ?? problem.title);
    this.name = 'ApiError';
  }
}

export async function api<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`/api/v1${path}`, {
    ...init,
    credentials: 'include',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json', ...init?.headers },
  });

  if (!response.ok) {
    const isProblem = response.headers.get('content-type')?.includes('json');
    const problem: Problem = isProblem
      ? await response.json()
      : { title: response.statusText, status: response.status };
    throw new ApiError(problem);
  }

  return response.status === 204 ? (undefined as T) : ((await response.json()) as T);
}

/** New app transaction ID for a document form; reused on every retry so a double click cannot post twice. */
export function newTransactionId(): string {
  return `YF-${crypto.randomUUID()}`;
}
