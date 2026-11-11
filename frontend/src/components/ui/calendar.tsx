import { useEffect, useId, useRef, useState, type KeyboardEvent } from 'react';
import { CalendarDays, ChevronLeft, ChevronRight, X } from 'lucide-react';
import { cn } from '@/lib/utils';
import { calendarDate, calendarISO, calendarDisplay, calendarInputValue } from './calendar-utils';
import './calendar.css';

export interface DatePickerProps {
  id?: string;
  name?: string;
  value?: string;
  onValueChange: (value: string) => void;
  min?: string;
  max?: string;
  disabled?: boolean;
  required?: boolean;
  className?: string;
  placeholder?: string;
  error?: string;
  'aria-label'?: string;
  'aria-labelledby'?: string;
  'aria-describedby'?: string;
}

const weekdays = ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'];
const control =
  'inline-flex size-8 shrink-0 items-center justify-center rounded-md text-slate-600 hover:bg-slate-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary disabled:cursor-not-allowed disabled:opacity-40';

function CalendarField({
  id,
  name,
  value = '',
  onValueChange,
  min,
  max,
  disabled,
  required,
  className,
  placeholder,
  error,
  withTime = false,
  ...aria
}: DatePickerProps & { withTime?: boolean }) {
  const generatedId = useId();
  const inputId = id ?? generatedId;
  const popupId = `${inputId}-calendar`;
  const input = useRef<HTMLInputElement>(null);
  const popup = useRef<HTMLDivElement>(null);
  const field = useRef<HTMLDivElement>(null);
  const [open, setOpen] = useState(false);
  const [month, setMonth] = useState(() => calendarDate(value) ?? new Date());
  const [view, setView] = useState<'days' | 'months' | 'years'>('days');
  const [viewMotion, setViewMotion] = useState<'expand' | 'drill'>('expand');
  const [focusedDay, setFocusedDay] = useState('');
  const [draft, setDraft] = useState({ source: value, text: calendarDisplay(value, withTime) });
  const text = draft.source === value ? draft.text : calendarDisplay(value, withTime);
  const today = calendarISO(new Date());
  const minimum = min?.slice(0, 10);
  const maximum = max?.slice(0, 10);
  const unavailable = (day: string) =>
    Boolean((minimum && day < minimum) || (maximum && day > maximum));

  useEffect(() => {
    const parsed = calendarInputValue(text, withTime);
    const invalid = text && (!parsed || (min && parsed < min) || (max && parsed > max));
    input.current?.setCustomValidity(
      invalid ? 'Vui lòng nhập ngày hợp lệ trong khoảng cho phép.' : '',
    );
  }, [text, withTime, min, max]);

  useEffect(() => {
    if (!open || disabled) return;
    const element = popup.current;
    if (!element) return;
    // Popover's top layer also works inside native modal dialogs, without a body portal.
    element.showPopover();
    const position = () => {
      const rect = field.current?.getBoundingClientRect();
      if (!rect) return;
      const width = Math.min(304, window.innerWidth - 16);
      element.style.width = `${width}px`;
      const height = element.getBoundingClientRect().height;
      element.style.left = `${Math.max(8, Math.min(rect.left, window.innerWidth - width - 8))}px`;
      element.style.top = `${Math.max(8, rect.bottom + height + 8 <= window.innerHeight ? rect.bottom + 6 : rect.top - height - 6)}px`;
    };
    position();
    const resize = new ResizeObserver(position);
    resize.observe(element);
    const outside = (event: PointerEvent) => {
      if (!element.contains(event.target as Node) && !field.current?.contains(event.target as Node))
        setOpen(false);
    };
    const escape = (event: globalThis.KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.preventDefault();
        event.stopPropagation();
        setOpen(false);
        input.current?.focus();
      }
    };
    document.addEventListener('pointerdown', outside);
    document.addEventListener('keydown', escape, true);
    window.addEventListener('resize', position);
    window.addEventListener('scroll', position, true);
    return () => {
      element.hidePopover();
      resize.disconnect();
      document.removeEventListener('pointerdown', outside);
      document.removeEventListener('keydown', escape, true);
      window.removeEventListener('resize', position);
      window.removeEventListener('scroll', position, true);
    };
  }, [open, disabled]);

  const commit = (next: string, close = !withTime) => {
    onValueChange(next);
    setDraft({ source: next, text: calendarDisplay(next, withTime) });
    if (close) {
      setOpen(false);
      input.current?.focus();
    }
  };
  const show = () => {
    const initial = value.slice(0, 10) || today;
    const bounded =
      minimum && initial < minimum ? minimum : maximum && initial > maximum ? maximum : initial;
    setMonth(calendarDate(bounded) ?? new Date());
    setView('days');
    setFocusedDay(bounded);
    setOpen(true);
  };
  const choose = (day: string) => {
    let next = withTime ? `${day}T${value.split('T')[1] || '00:00'}` : day;
    if (min && next < min) next = min;
    if (max && next > max) next = max;
    commit(next);
  };
  const changeTime = (hour: string, minute: string) => {
    let next = `${value.slice(0, 10) || focusedDay}T${hour}:${minute}`;
    if (min && next < min) next = min;
    if (max && next > max) next = max;
    commit(next, false);
  };
  const gridKey = (event: KeyboardEvent<HTMLButtonElement>, day: string) => {
    const date = calendarDate(day)!;
    const delta = { ArrowLeft: -1, ArrowRight: 1, ArrowUp: -7, ArrowDown: 7 }[event.key];
    if (delta !== undefined) date.setDate(date.getDate() + delta);
    else if (event.key === 'Home') date.setDate(date.getDate() - ((date.getDay() + 6) % 7));
    else if (event.key === 'End') date.setDate(date.getDate() + 6 - ((date.getDay() + 6) % 7));
    else if (event.key === 'PageUp' || event.key === 'PageDown') {
      const original = date.getDate();
      date.setDate(1);
      date.setMonth(date.getMonth() + (event.key === 'PageUp' ? -1 : 1));
      date.setDate(
        Math.min(original, new Date(date.getFullYear(), date.getMonth() + 1, 0).getDate()),
      );
    } else return;
    event.preventDefault();
    const next = calendarISO(date);
    if (unavailable(next)) return;
    setMonth(date);
    setFocusedDay(next);
    requestAnimationFrame(() =>
      popup.current?.querySelector<HTMLButtonElement>(`[data-day="${next}"]`)?.focus(),
    );
  };
  const changeMonth = (offset: number) => {
    const date = new Date(month.getFullYear(), month.getMonth() + offset, 1);
    setMonth(date);
    const first = calendarISO(date);
    setFocusedDay(minimum && first < minimum ? minimum : first);
  };
  const changeView = (next: typeof view) => {
    const levels = { days: 0, months: 1, years: 2 };
    setViewMotion(levels[next] > levels[view] ? 'expand' : 'drill');
    setView(next);
  };
  const decade = Math.floor(month.getFullYear() / 10) * 10;
  const heading =
    view === 'days'
      ? `Tháng ${month.getMonth() + 1}, ${month.getFullYear()}`
      : view === 'months'
        ? String(month.getFullYear())
        : `${decade} – ${decade + 9}`;
  const periodUnavailable = (year: number, monthIndex?: number) => {
    if (year < 100 || year > 9999) return true;
    const first = `${year}-${String((monthIndex ?? 0) + 1).padStart(2, '0')}-01`;
    const last =
      monthIndex === undefined ? `${year}-12-31` : calendarISO(new Date(year, monthIndex + 1, 0));
    return Boolean((minimum && last < minimum) || (maximum && first > maximum));
  };
  const navigate = (direction: number) =>
    changeMonth(direction * (view === 'days' ? 1 : view === 'months' ? 12 : 120));
  const navigationDisabled = (direction: number) => {
    if (view === 'days') {
      const date = new Date(month.getFullYear(), month.getMonth() + direction, 1);
      return periodUnavailable(date.getFullYear(), date.getMonth());
    }
    if (view === 'months') return periodUnavailable(month.getFullYear() + direction);
    const first = decade + direction * 10;
    return (
      first + 9 < Number(minimum?.slice(0, 4) ?? 100) ||
      first > Number(maximum?.slice(0, 4) ?? 9999)
    );
  };
  const periodKey = (event: KeyboardEvent<HTMLButtonElement>) => {
    const buttons = Array.from(
      popup.current?.querySelectorAll<HTMLButtonElement>('[data-period]') ?? [],
    );
    const index = buttons.indexOf(event.currentTarget);
    const delta = { ArrowLeft: -1, ArrowRight: 1, ArrowUp: -4, ArrowDown: 4 }[event.key];
    const target =
      event.key === 'Home'
        ? 0
        : event.key === 'End'
          ? buttons.length - 1
          : delta !== undefined
            ? index + delta
            : -1;
    if (target < 0 || target >= buttons.length) return;
    event.preventDefault();
    if (!buttons[target].disabled) buttons[target].focus();
  };
  const start = new Date(month.getFullYear(), month.getMonth(), 1);
  start.setDate(1 - ((start.getDay() + 6) % 7));
  const days = Array.from({ length: 42 }, (_, index) => {
    const date = new Date(start);
    date.setDate(start.getDate() + index);
    return date;
  });

  return (
    <div className="w-full min-w-0">
      <div
        ref={field}
        className={cn(
          'flex h-10 w-full items-center gap-1 rounded-lg border border-slate-200 bg-white px-3 text-sm text-slate-900 transition-colors hover:border-slate-300 focus-within:border-primary focus-within:ring-2 focus-within:ring-primary/15',
          disabled && 'cursor-not-allowed bg-slate-50 opacity-60',
          error && 'border-destructive',
          className,
        )}
      >
        <input
          {...aria}
          ref={input}
          id={inputId}
          name={name}
          value={text}
          type="text"
          disabled={disabled}
          required={required}
          autoComplete="off"
          placeholder={placeholder ?? (withTime ? 'dd/mm/yyyy hh:mm' : 'dd/mm/yyyy')}
          aria-invalid={error ? true : undefined}
          aria-haspopup="dialog"
          aria-expanded={open && !disabled}
          aria-controls={popupId}
          aria-describedby={error ? `${inputId}-error` : aria['aria-describedby']}
          className="min-w-0 flex-1 bg-transparent py-2 outline-none placeholder:text-slate-500 disabled:cursor-not-allowed"
          onChange={(event) => {
            const next = calendarInputValue(event.target.value, withTime);
            const valid = next && (!min || next >= min) && (!max || next <= max);
            const source = !event.target.value ? '' : valid ? next : value;
            setDraft({ source, text: event.target.value });
            if (!event.target.value || valid) onValueChange(source);
          }}
          onKeyDown={(event) => {
            if (event.key === 'ArrowDown') {
              event.preventDefault();
              show();
              requestAnimationFrame(() =>
                popup.current
                  ?.querySelector<HTMLButtonElement>('[data-day][tabindex="0"]')
                  ?.focus(),
              );
            }
          }}
        />
        {text && !disabled && !required && (
          <button
            type="button"
            className={control}
            aria-label="Xóa ngày"
            onClick={() => commit('', true)}
          >
            <X className="size-3.5" />
          </button>
        )}
        <button
          type="button"
          disabled={disabled}
          className={control}
          aria-label="Mở lịch"
          aria-haspopup="dialog"
          aria-expanded={open && !disabled}
          aria-controls={popupId}
          onClick={() => (open ? setOpen(false) : show())}
        >
          <CalendarDays className="size-4" />
        </button>
      </div>
      <div
        ref={popup}
        id={popupId}
        popover="manual"
        role="dialog"
        aria-label={withTime ? 'Chọn ngày và giờ' : 'Chọn ngày'}
        className="fixed inset-auto m-0 max-h-[calc(100dvh-1rem)] overflow-auto rounded-xl border border-slate-200 bg-white p-3 text-slate-900 shadow-lg"
        onBlur={(event) => {
          if (
            event.relatedTarget &&
            !event.currentTarget.contains(event.relatedTarget as Node) &&
            !field.current?.contains(event.relatedTarget as Node)
          )
            setOpen(false);
        }}
      >
        <div className="mb-3 flex items-center justify-between gap-2">
          <button
            type="button"
            aria-label={
              view === 'days' ? 'Chọn tháng' : view === 'months' ? 'Chọn năm' : 'Khoảng năm'
            }
            disabled={view === 'years'}
            className="min-w-0 flex-1 rounded-md px-2 py-2 text-left text-sm font-semibold hover:bg-slate-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary disabled:text-slate-600 disabled:hover:bg-transparent"
            onClick={() => changeView(view === 'days' ? 'months' : 'years')}
          >
            {heading}
          </button>
          <div className="flex gap-1">
            <button
              type="button"
              aria-label={
                view === 'days'
                  ? 'Tháng trước'
                  : view === 'months'
                    ? 'Năm trước'
                    : 'Thập niên trước'
              }
              className={control}
              disabled={navigationDisabled(-1)}
              onClick={() => navigate(-1)}
            >
              <ChevronLeft className="size-4" />
            </button>
            <button
              type="button"
              aria-label={
                view === 'days' ? 'Tháng sau' : view === 'months' ? 'Năm sau' : 'Thập niên sau'
              }
              className={control}
              disabled={navigationDisabled(1)}
              onClick={() => navigate(1)}
            >
              <ChevronRight className="size-4" />
            </button>
          </div>
        </div>
        <p className="sr-only" aria-live="polite">
          {heading}
        </p>
        <div key={view} className="calendar-view min-h-[17.5rem]" data-motion={viewMotion}>
          {view !== 'days' && (
            <div
              role="group"
              aria-label={view === 'months' ? 'Danh sách tháng' : 'Danh sách năm'}
              className="grid h-[17.5rem] grid-cols-4 gap-2"
            >
              {Array.from({ length: view === 'months' ? 12 : 16 }, (_, index) => {
                const year = view === 'months' ? month.getFullYear() : decade - 2 + index;
                const selected =
                  view === 'months' ? index === month.getMonth() : year === month.getFullYear();
                return (
                  <button
                    key={`${view}-${year}-${index}`}
                    type="button"
                    data-period
                    aria-label={
                      view === 'months' ? `Tháng ${index + 1} năm ${year}` : `Năm ${year}`
                    }
                    aria-pressed={selected}
                    disabled={periodUnavailable(year, view === 'months' ? index : undefined)}
                    onKeyDown={periodKey}
                    className={cn(
                      'rounded-lg text-sm tabular-nums hover:bg-slate-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary disabled:cursor-not-allowed disabled:opacity-30',
                      view === 'years' && (year < decade || year > decade + 9) && 'text-slate-500',
                      selected && 'bg-primary font-semibold text-white hover:bg-primary/90',
                    )}
                    onClick={() => {
                      changeMonth(
                        view === 'months'
                          ? index - month.getMonth()
                          : (year - month.getFullYear()) * 12,
                      );
                      changeView(view === 'months' ? 'days' : 'months');
                    }}
                  >
                    {view === 'months' ? `Thg ${index + 1}` : year}
                  </button>
                );
              })}
            </div>
          )}
          {view === 'days' && (
            <table
              className="w-full table-fixed border-collapse text-center text-sm"
              aria-label={`Tháng ${month.getMonth() + 1} năm ${month.getFullYear()}`}
            >
              <thead>
                <tr>
                  {weekdays.map((day) => (
                    <th key={day} scope="col" className="h-8 text-xs font-medium text-slate-500">
                      {day}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {Array.from({ length: 6 }, (_, week) => (
                  <tr key={week}>
                    {days.slice(week * 7, week * 7 + 7).map((date) => {
                      const day = calendarISO(date);
                      const selected = day === value.slice(0, 10);
                      return (
                        <td key={day} className="p-0.5">
                          <button
                            type="button"
                            data-day={day}
                            tabIndex={day === focusedDay ? 0 : -1}
                            disabled={unavailable(day)}
                            aria-label={date.toLocaleDateString('vi-VN', {
                              weekday: 'long',
                              day: 'numeric',
                              month: 'long',
                              year: 'numeric',
                            })}
                            aria-pressed={selected}
                            aria-current={day === today ? 'date' : undefined}
                            onKeyDown={(event) => gridKey(event, day)}
                            onClick={() => choose(day)}
                            className={cn(
                              'flex aspect-square w-full items-center justify-center rounded-lg tabular-nums hover:bg-slate-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary disabled:cursor-not-allowed disabled:opacity-30',
                              date.getMonth() !== month.getMonth() && 'text-slate-400',
                              day === today && 'ring-1 ring-inset ring-primary text-primary',
                              selected && 'bg-primary font-semibold text-white hover:bg-primary/90',
                            )}
                          >
                            {date.getDate()}
                          </button>
                        </td>
                      );
                    })}
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
        {withTime && (
          <div className="mt-2 flex items-center justify-between gap-3 border-t border-slate-100 pt-3">
            <label htmlFor={`${inputId}-hour`} className="text-sm font-medium">
              Giờ
            </label>
            <div className="flex items-center gap-1">
              <select
                id={`${inputId}-hour`}
                aria-label="Giờ"
                value={value.slice(11, 13) || '00'}
                className="rounded-md border border-slate-200 bg-white px-2 py-1 text-sm focus-visible:outline-primary"
                onChange={(event) => changeTime(event.target.value, value.slice(14, 16) || '00')}
              >
                {Array.from({ length: 24 }, (_, hour) => {
                  const text = String(hour).padStart(2, '0');
                  return (
                    <option key={text} value={text}>
                      {text}
                    </option>
                  );
                })}
              </select>
              <span aria-hidden="true">:</span>
              <select
                aria-label="Phút"
                value={value.slice(14, 16) || '00'}
                className="rounded-md border border-slate-200 bg-white px-2 py-1 text-sm focus-visible:outline-primary"
                onChange={(event) => changeTime(value.slice(11, 13) || '00', event.target.value)}
              >
                {Array.from({ length: 60 }, (_, minute) => {
                  const text = String(minute).padStart(2, '0');
                  return (
                    <option key={text} value={text}>
                      {text}
                    </option>
                  );
                })}
              </select>
            </div>
          </div>
        )}
        <div className="mt-3 flex items-center justify-between border-t border-slate-100 pt-2">
          <button
            type="button"
            disabled={unavailable(today)}
            className="rounded-md px-2 py-1.5 text-sm font-medium text-primary hover:bg-primary/5 focus-visible:outline-primary disabled:opacity-40"
            onClick={() => choose(today)}
          >
            Hôm nay
          </button>
          {withTime ? (
            <button
              type="button"
              onClick={() => {
                setOpen(false);
                input.current?.focus();
              }}
              className="rounded-md bg-primary px-3 py-1.5 text-sm font-medium text-white focus-visible:outline-primary"
            >
              Xong
            </button>
          ) : (
            <button
              type="button"
              disabled={required || !value}
              className="rounded-md px-2 py-1.5 text-sm text-slate-600 hover:bg-slate-100 focus-visible:outline-primary disabled:opacity-40"
              onClick={() => commit('', true)}
            >
              Xóa
            </button>
          )}
        </div>
      </div>
      {error && (
        <p id={`${inputId}-error`} role="alert" className="mt-1.5 text-xs text-destructive">
          {error}
        </p>
      )}
    </div>
  );
}

export function DatePicker(props: DatePickerProps) {
  return <CalendarField {...props} />;
}
export function DateTimePicker(props: DatePickerProps) {
  return <CalendarField {...props} withTime />;
}
