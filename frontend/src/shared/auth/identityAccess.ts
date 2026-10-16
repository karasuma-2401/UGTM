export function getIdentityAccess(hasPermission: (permissions: string | string[]) => boolean) {
  const canReadUsers = hasPermission('identity.users.read');
  const canManageRoles =
    hasPermission('identity.roles.manage') && hasPermission('identity.permissions.read');

  return { canReadUsers, canManageRoles, canAccess: canReadUsers || canManageRoles };
}
