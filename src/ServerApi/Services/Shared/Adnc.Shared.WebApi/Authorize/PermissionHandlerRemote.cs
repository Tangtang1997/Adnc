﻿namespace Microsoft.AspNetCore.Authorization;

public sealed class PermissionHandlerRemote : AbstractPermissionHandler
{
    private readonly IAuthRpcService _authRpcService;
    //private readonly IHttpContextAccessor _contextAccessor;

    public PermissionHandlerRemote(IAuthRpcService authRpcService) => _authRpcService = authRpcService;

    protected override async Task<bool> CheckUserPermissions(long userId, IEnumerable<string> codes, string validationVersion)
    {
        //var jwtToken = await _contextAccessor.HttpContext.GetTokenAsync("access_token");
        //var refitResult = await _authRpcService.GetCurrenUserPermissions($"Bearer {jwtToken}", userId, codes);
        var refitResult = await _authRpcService.GetCurrenUserPermissionsAsync(userId, codes, validationVersion);
        if (!refitResult.IsSuccessStatusCode)
            return false;

        var permissions = refitResult.Content;
        return permissions.IsNotNullOrEmpty();
    }
}