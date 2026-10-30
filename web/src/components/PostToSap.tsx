import { useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { ApiError, newTransactionId } from '../lib/api';
import { getPosting, isSettling, reconcilePosting, submitPosting, type PostingIntent } from '../lib/postings';
import { StatusChip } from './StatusChip';

/**
 * Sends the document to SAP as a posting intent and follows it to a final state (scope section 8). One transaction ID
 * per document: a retry or double click reuses it, so SAP cannot get the same document twice.
 */
export function PostToSap({
  companyId,
  sapObject,
  buildPayload,
  canReconcile,
}: {
  companyId: string;
  sapObject: string;
  buildPayload: () => string;
  canReconcile: boolean;
}) {
  const [transactionId, setTransactionId] = useState(newTransactionId);
  const [postings, setPostings] = useState<PostingIntent[]>([]);
  const [formError, setFormError] = useState<string | null>(null);

  const post = useMutation({
    mutationFn: (payload: string) => submitPosting(companyId, transactionId, sapObject, payload),
    onSuccess: (intent) => {
      setPostings((list) => [intent, ...list.filter((p) => p.id !== intent.id)]);
      // The API now owns this transaction; the next document gets a fresh ID.
      setTransactionId(newTransactionId());
    },
  });

  const onPost = () => {
    setFormError(null);
    let payload: string;
    try {
      payload = buildPayload();
    } catch (e) {
      setFormError((e as Error).message);
      return;
    }
    post.mutate(payload);
  };

  const requestError = post.error instanceof ApiError ? post.error.problem.detail ?? post.error.problem.title : post.error?.message;

  return (
    <section className="yf-card yf-section" aria-label="Post to SAP">
      <h2>Post to SAP</h2>
      <button type="button" className="yf-btn-primary" onClick={onPost} disabled={post.isPending}>
        {post.isPending ? 'Posting…' : 'Post sales order'}
      </button>
      {(formError ?? requestError) && (
        <p role="alert" className="yf-error">
          {formError ?? requestError}
        </p>
      )}
      {postings.length > 0 && (
        <ul className="yf-postings" aria-label="Postings">
          {postings.map((p) => (
            <PostingRow key={p.id} initial={p} canReconcile={canReconcile} />
          ))}
        </ul>
      )}
    </section>
  );
}

function PostingRow({ initial, canReconcile }: { initial: PostingIntent; canReconcile: boolean }) {
  const { data, refetch } = useQuery({
    queryKey: ['posting', initial.id],
    queryFn: () => getPosting(initial.id),
    initialData: initial,
    refetchInterval: (q) => (q.state.data && isSettling(q.state.data.state) ? 5000 : false),
  });
  const check = useMutation({ mutationFn: () => reconcilePosting(initial.id), onSettled: () => void refetch() });
  const p = data ?? initial;

  return (
    <li>
      <div className="yf-postings__head">
        <StatusChip status={p.state} />
        {p.sapDocNum != null && <strong>SAP order {p.sapDocNum}</strong>}
      </div>
      {p.errorMessage && p.state !== 'Posted' && <span className="yf-muted">{p.errorMessage}</span>}
      {p.state === 'OutcomeUnknown' &&
        (canReconcile ? (
          <button type="button" className="yf-btn-secondary" onClick={() => check.mutate()} disabled={check.isPending}>
            {check.isPending ? 'Checking SAP…' : 'Check SAP now'}
          </button>
        ) : (
          <span className="yf-muted">YeaFlow checks SAP for this document automatically within a few minutes. Nothing will be posted twice.</span>
        ))}
      {check.error && (
        <span role="alert" className="yf-error">
          {check.error instanceof ApiError ? (check.error.problem.detail ?? check.error.problem.title) : check.error.message}
        </span>
      )}
      <span className="yf-muted">
        <small>{p.transactionId}</small>
      </span>
    </li>
  );
}
