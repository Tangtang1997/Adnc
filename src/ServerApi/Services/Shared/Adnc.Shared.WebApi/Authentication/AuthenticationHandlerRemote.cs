﻿namespace Adnc.Shared.WebApi.Authentication;

public class AuthenticationHandlerRemote : AbstracAuthenticationHandler
{
    public IAuthRestClient _authRestClient;

    public AuthenticationHandlerRemote(IAuthRestClient authRestClient) => _authRestClient = authRestClient;

    protected override async Task<(string ValidationVersion, int Status)> GetValidatedInfoAsync(long userId)
    {
        var apiReuslt = await _authRestClient.GetValidatedInfoAsync();
        if (!apiReuslt.IsSuccessStatusCode)
            return (null, 0);

        return (apiReuslt.Content.ValidationVersion, apiReuslt.Content.Status);
    }
}
