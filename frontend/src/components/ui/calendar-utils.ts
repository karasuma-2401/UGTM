const pad = (value: number) => String(value).padStart(2, '0');

export function calendarISO(date: Date): string {
  return `${String(date.getFullYear()).padStart(4, '0')}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

export function calendarDate(value: string): Date | null {
  const part = value.slice(0, 10);
  if (!/^\d{4}-\d{2}-\d{2}$/.test(part)) return null;
  const [year, month, day] = part.split('-').map(Number);
  const date = new Date(0);
  date.setHours(0, 0, 0, 0);
  date.setFullYear(year, month - 1, day);
  return year >= 1 && calendarISO(date) === part ? date : null;
}

export function calendarDisplay(value: string, withTime = false): string {
  if (!calendarDate(value)) return '';
  const [year, month, day] = value.slice(0, 10).split('-');
  return `${day}/${month}/${year}${withTime ? ` ${value.slice(11, 16)}` : ''}`;
}

export function calendarInputValue(text: string, withTime = false): string | null {
  const match = text.match(
    withTime ? /^(\d{2})\/(\d{2})\/(\d{4}) (\d{2}):(\d{2})$/ : /^(\d{2})\/(\d{2})\/(\d{4})$/,
  );
  if (!match) return null;
  const [, day, month, year, hour, minute] = match;
  const value = `${year}-${month}-${day}`;
  if (!calendarDate(value) || (withTime && (Number(hour) > 23 || Number(minute) > 59))) return null;
  return withTime ? `${value}T${hour}:${minute}` : value;
}
