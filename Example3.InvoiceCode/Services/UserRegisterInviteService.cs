﻿// Copyright (c) 2021 Jon P Smith, GitHub: JonPSmith, web: http://www.thereformedprogrammer.net/
// Licensed under MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AuthPermissions.AdminCode;
using AuthPermissions.CommonCode;
using Example3.InvoiceCode.Dtos;
using ExamplesCommonCode.UsefulCode;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StatusGeneric;

namespace Example3.InvoiceCode.Services;

public class UserRegisterInviteService : IUserRegisterInviteService
{
    private readonly IAuthTenantAdminService _tenantAdminService;
    private readonly IAuthUsersAdminService _authUsersAdmin;
    private readonly UserManager<IdentityUser> _userManager;

    //NOTE: This is NOT the way to do this in a real app.
    //In real apps you should load the key from appsettings.json file, and have a different (and private) key in production
    private const string EncryptionTextKey = "Asaadjn33TbAw441azn";

    private readonly Dictionary<TenantVersionTypes, List<string>> _rolesToAddUserForVersions = new()
    {
        { TenantVersionTypes.Free, null },
        { TenantVersionTypes.Pro, new List<string> { "Tenant Admin" } },
        { TenantVersionTypes.Enterprise, new List<string> { "Tenant Admin" } }
    };

    private readonly Dictionary<TenantVersionTypes, List<string>> _rolesToAddTenantForVersion = new()
    {
        { TenantVersionTypes.Free, new List<string> { "Tenant User" } },
        { TenantVersionTypes.Pro, new List<string> { "Tenant User", "Tenant Admin" } },
        { TenantVersionTypes.Enterprise, new List<string> { "Tenant User", "Tenant Admin", "Enterprise" } },
    };

    public UserRegisterInviteService(IAuthTenantAdminService tenantAdminService, IAuthUsersAdminService authUsersAdmin, UserManager<IdentityUser> userManager)
    {
        _tenantAdminService = tenantAdminService;
        _authUsersAdmin = authUsersAdmin;
        _userManager = userManager;
    }

    /// <summary>
    /// This does three things (with lots of checks)
    /// - Adds the new user to the the individual account
    /// - Adds an AuthUser for this person
    /// - Creates the tenant with the correct tenant roles
    /// NOTE: On return you MUST sign in the user using the email and password they provided via the individual accounts signInManager
    /// </summary>
    /// <param name="dto">The information from the user</param>
    /// <returns>Status with the individual accounts user</returns>
    public async Task<IStatusGeneric<IdentityUser>> CreateNewTenantAsync(CreateTenantDto dto)
    {
        var status = new StatusGenericHandler<IdentityUser>
        {
            Message =
                $"Successfully created the tenant called '{dto.TenantName}' and registered you as the tenant admin"
        };

        var tenantVersion = dto.GetTenantVersionType();

        if (tenantVersion == TenantVersionTypes.NotSet)
            throw new AuthPermissionsException("The Version string in the dto wasn't set properly");

        //Check if tenant name is available
        if (_tenantAdminService.QueryTenants().Any(x => x.TenantFullName == dto.TenantName))
            return status.AddError($"The tenant name '{dto.TenantName}' is already taken", new []{nameof(CreateTenantDto.TenantName) });

        //Add the new individual user, or return existing user
        //Will sent back error if already an AuthUser, because a user can't be linked to multiple tenants
        var userStatus = await GetIndividualAccountUserAndCheckNotAuthUser(dto.Email, dto.Password);
        if (status.CombineStatuses(userStatus).HasErrors)
            return status;

        //Now we can create the tenant, with the correct tenant roles
        var tenantStatus = await _tenantAdminService.AddSingleTenantAsync(dto.TenantName, _rolesToAddTenantForVersion[tenantVersion]);
        if (status.CombineStatuses(tenantStatus).HasErrors)
            return status;

        //This creates a user, with the roles suitable for the version of the version of the app
        status.CombineStatuses(await _authUsersAdmin.AddNewUserAsync(userStatus.Result.Id, dto.Email, null,
            _rolesToAddUserForVersions[dto.GetTenantVersionType()], dto.TenantName));

        status.SetResult(userStatus.Result);

        return status;
    }

    /// <summary>
    /// This creates a an encrypted string containing the tenantId and the user's email
    /// so that you can confirm the user is valid
    /// </summary>
    /// <param name="tenantId">Id of the tenant you want the user to join</param>
    /// <param name="emailOfJoiner">email of the user</param>
    /// <returns>encrypted string to send the user encoded to work with urls</returns>
    public string InviteUserToJoinTenantAsync(int tenantId, string emailOfJoiner)
    {
        var encryptor = new EncryptDecrypt(EncryptionTextKey);
        var verify = encryptor.Encrypt($"{tenantId},{emailOfJoiner.Trim()}");
        return Base64UrlEncoder.Encode(verify);
    }

    /// <summary>
    /// This will take the new user's information plus the encrypted invite code and
    /// 1. decides if the invite matches the user's email
    /// 2. It will create an individual accounts user (if not there), plus a check teh user isn't already an authP user
    /// 3. Then it will create an authP user linked to the tenant they were invited to
    /// NOTE: On return you MUST sign in the user using the email and password they provided via the individual accounts signInManager
    /// </summary>
    /// <param name="email">email given to log in</param>
    /// <param name="password">password given to log in</param>
    /// <param name="verify">The encrypted part of the url encoded to work with urls
    /// that was created by <see cref="InviteUserToJoinTenantAsync"/></param>
    /// <returns>Status with the individual accounts user</returns>
    public async Task<IStatusGeneric<IdentityUser>> AcceptUserJoiningATenantAsync(string email, string password, string verify)
    {
        var status = new StatusGenericHandler<IdentityUser>();

        var encryptor = new EncryptDecrypt(EncryptionTextKey);
        int tenantId;
        string emailOfJoiner;
        try
        {
            var decrypted = encryptor.Decrypt(Base64UrlEncoder.Decode(verify));

            var parts = decrypted.Split(',');
            tenantId = int.Parse(parts[0]);
            emailOfJoiner = parts[1].Trim();
        }
        catch (Exception e)
        {
            //Could add a log here
            return status.AddError("Sorry, the verification failed.");
        }

        if (emailOfJoiner != email.Trim())
            return status.AddError("Sorry, your email didn't match the invite.");

        var tenant = await _tenantAdminService.QueryTenants()
            .SingleOrDefaultAsync(x => x.TenantId == tenantId);
        if (tenant == null)
            return status.AddError("Sorry, your invite is rejected. Please talk to your admin person.");

        //Add the new individual user, or return existing user
        //Will sent back error if already an AuthUser, because a user can't be linked to multiple tenants
        var userStatus = await GetIndividualAccountUserAndCheckNotAuthUser(email, password);
        if (status.CombineStatuses(userStatus).HasErrors)
            return status;

        //There is no need to add roles as the "Tenant User" role is automatically added via the tenant's roles
        status.CombineStatuses(await _authUsersAdmin.AddNewUserAsync(userStatus.Result.Id, email, null,
            new List<string>(), tenant.TenantFullName));

        if (status.HasErrors)
            return status;

        status.SetResult(userStatus.Result);
        status.Message = $"You have successfully joined the tenant '{tenant.TenantFullName}'";
        return status;
    }

    //---------------------------------------------------------------
    //private methods

    private async Task<IStatusGeneric<IdentityUser>> GetIndividualAccountUserAndCheckNotAuthUser(string email, string password)
    {
        var status = new StatusGenericHandler<IdentityUser>();

        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new IdentityUser { UserName = email, Email = email };
            var result = await _userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                result.Errors.Select(x => x.Description).ToList().ForEach(error => status.AddError(error));
            }
        }
        else
        {
            if (!await _userManager.CheckPasswordAsync(user, password))
                throw new AuthPermissionsException("The user was known, but the password was wrong");
        }

        //Check if user is already in the AuthUsers (because a AuthUser can only be linked to one tenant)
        if ((await _authUsersAdmin.FindAuthUserByEmailAsync(email)).Result != null)
            status.AddError("You are already registered as a user, which means you can't ask to access another tenant.");

        return status.SetResult(user);
    }
}