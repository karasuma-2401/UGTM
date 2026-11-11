import { modules } from '@/shared/config/modules';

export function permissionModuleLabel(moduleName: string): string {
  if (moduleName.toLowerCase() === 'general') return 'Quyền chung';
  return (
    modules.find((module) => module.key.toLowerCase() === moduleName.toLowerCase())?.label ??
    moduleName
  );
}
