import type { AccountGroup, SystemRole } from '../types/user.types';

export function defaultRolesForGroup(group: AccountGroup): SystemRole[] {
  return group === 'Student' ? ['Student'] : ['Lecturer'];
}

export function rolesBelongToGroup(roles: SystemRole[], group: AccountGroup): boolean {
  return group === 'Student'
    ? roles.length === 1 && roles[0] === 'Student'
    : roles.length > 0 && !roles.includes('Student');
}
