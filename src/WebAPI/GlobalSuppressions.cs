// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
    "Design",
    "CA1054:URI-like parameters should not be strings",
    Justification = "returnUrl is validated with Url.IsLocalUrl before use",
    Scope = "member",
    Target = "~M:Ravuno.WebAPI.Controllers.AuthController.Login(System.String)~Microsoft.AspNetCore.Mvc.IActionResult"
)]
[assembly: SuppressMessage(
    "Design",
    "CA1054:URI-like parameters should not be strings",
    Justification = "returnUrl is validated with Url.IsLocalUrl before use",
    Scope = "member",
    Target = "~M:Ravuno.WebAPI.Controllers.AuthController.Login(System.String,System.String,System.String)~System.Threading.Tasks.Task{Microsoft.AspNetCore.Mvc.IActionResult}"
)]
