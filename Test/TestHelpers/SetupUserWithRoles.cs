﻿// Copyright (c) 2021 Jon P Smith, GitHub: JonPSmith, web: http://www.thereformedprogrammer.net/
// Licensed under MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using AuthPermissions.CommonCode;
using AuthPermissions.DataLayer.Classes;
using AuthPermissions.DataLayer.Classes.SupportTypes;
using AuthPermissions.DataLayer.EfCode;
using Xunit.Extensions.AssertExtensions;

namespace Test.TestHelpers;

public class SetupUserWithRoles
{
    public AuthUser CurrentUser { get; }

    /// <summary>
    /// This sets up roles and and a AuthUser. If the role2Type isn't <see cref="RoleTypes.Normal"/>,
    /// then a tenant added
    /// </summary>
    /// <param name="context"></param>
    /// <param name="role2Type"></param>
    /// <param name="userHasTenant"></param>
    public SetupUserWithRoles(AuthPermissionsDbContext context, RoleTypes role2Type, bool userHasTenant)
    {
        var rolePer1 = new RoleToPermissions("Role1", null, $"{(char)1}{(char)3}");
        var rolePer2 = new RoleToPermissions("Role2", null, $"{(char)2}{(char)3}", role2Type);

        var rolesForTenant = role2Type == RoleTypes.TenantAutoAdd || role2Type == RoleTypes.TenantAdminAdd
            ? new List<RoleToPermissions> { rolePer2 }
            : new List<RoleToPermissions>();

        var tenant = userHasTenant
            ? Tenant.CreateSingleTenant("Tenant1", rolesForTenant).Result 
                ?? throw new AuthPermissionsException("CreateSingleTenant had errors.")
            : null;

        context.AddRange(rolePer1, rolePer2);
        var rolesForUsers = role2Type == RoleTypes.Normal || role2Type == RoleTypes.HiddenFromTenant
            ? new List<RoleToPermissions>() { rolePer1, rolePer2 }
            : new List<RoleToPermissions>() { rolePer1 };

        var status = AuthUser.CreateAuthUser("User1", "User1@g.com", null, rolesForUsers, tenant);
        status.IsValid.ShouldBeTrue(status.GetAllErrors());

        CurrentUser = status.Result;
        context.Add(CurrentUser);
        context.SaveChanges();

        context.ChangeTracker.Clear();
    }
}