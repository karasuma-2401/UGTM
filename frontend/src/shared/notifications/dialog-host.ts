const dialogs: HTMLDialogElement[] = [];
const listeners = new Set<() => void>();
export function registerNotificationDialog(dialog: HTMLDialogElement) {
  dialogs.push(dialog);
  listeners.forEach((listener) => listener());
  return () => {
    const index = dialogs.lastIndexOf(dialog);
    if (index >= 0) dialogs.splice(index, 1);
    listeners.forEach((listener) => listener());
  };
}
export const getNotificationDialog = () => dialogs.at(-1) ?? null;
export function subscribeNotificationDialog(listener: () => void) {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}
