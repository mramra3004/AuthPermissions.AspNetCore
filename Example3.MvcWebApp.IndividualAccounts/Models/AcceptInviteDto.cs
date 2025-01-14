﻿// Copyright (c) 2021 Jon P Smith, GitHub: JonPSmith, web: http://www.thereformedprogrammer.net/
// Licensed under MIT license. See License.txt in the project root for license information.

namespace Example3.MvcWebApp.IndividualAccounts.Models;

public class AcceptInviteDto
{
    public string Email { get; set; }
    public string Password { get; set; }
    public string Verify { get; set; }
}