import { useEffect, useRef, useState, type FormEvent } from 'react';
import { useMutation } from '@tanstack/react-query';
import { MailCheck } from 'lucide-react';
import { Dialog } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { studentTopicApi } from '../services/studentTopicApi';
import { getTopicErrorMessage } from '../services/topicApi';
import type { ParticipationResult } from '../types/studentTopic.types';

export function StudentParticipationDialog({
  onClose,
  onConfirmed,
  onRefresh,
}: {
  onClose: () => void;
  onConfirmed: (result: ParticipationResult) => void;
  onRefresh: () => void;
}) {
  const [token, setToken] = useState('');
  const [error, setError] = useState('');
  const submitting = useRef(false);
  const controller = useRef<AbortController | null>(null);
  useEffect(() => () => controller.current?.abort(), []);
  const mutation = useMutation({
    mutationFn: (value: string) =>
      studentTopicApi.confirmParticipation(value, controller.current?.signal),
    retry: false,
    onSuccess: (result) => {
      setToken('');
      onConfirmed(result);
    },
    onError: () => onRefresh(),
    onSettled: () => {
      submitting.current = false;
      controller.current = null;
    },
  });
  function submit(event: FormEvent) {
    event.preventDefault();
    if (submitting.current || mutation.isPending) return;
    if (!token.trim()) {
      setError('Vui lòng nhập mã xác nhận trong email.');
      return;
    }
    submitting.current = true;
    controller.current = new AbortController();
    setError('');
    mutation.mutate(token.trim());
  }
  return (
    <Dialog
      title="Xác nhận tham gia đề tài"
      busy={mutation.isPending}
      onClose={() => {
        if (!mutation.isPending) onClose();
      }}
    >
      <form onSubmit={submit} className="space-y-5 p-5 sm:p-6" noValidate>
        <div className="flex items-start gap-3">
          <span className="rounded-xl bg-blue-50 p-3 text-primary">
            <MailCheck className="size-5" />
          </span>
          <div>
            <h2 className="font-semibold text-slate-900">Xác nhận lời mời của bạn</h2>
            <p className="mt-1 text-sm leading-6 text-slate-500">
              Nhập mã trong email mời tham gia nhóm. Mã có hiệu lực 24 giờ.
            </p>
          </div>
        </div>
        <div className="space-y-2">
          <label htmlFor="participation-token" className="text-sm font-medium">
            Mã xác nhận
          </label>
          <Input
            id="participation-token"
            value={token}
            disabled={mutation.isPending}
            autoComplete="off"
            spellCheck={false}
            placeholder="Dán mã xác nhận từ email"
            error={error || undefined}
            maxLength={2048}
            onChange={(event) => {
              setToken(event.target.value);
              setError('');
              mutation.reset();
            }}
          />
        </div>
        {mutation.error && (
          <p role="alert" className="text-sm text-rose-700">
            {getTopicErrorMessage(mutation.error)}
          </p>
        )}
        <div className="flex flex-wrap justify-end gap-2 border-t border-slate-100 pt-4">
          <Button type="button" variant="outline" disabled={mutation.isPending} onClick={onClose}>
            Đóng
          </Button>
          <Button
            type="submit"
            leftIcon={<MailCheck />}
            isLoading={mutation.isPending}
            disabled={mutation.isPending}
          >
            Xác nhận tham gia
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
