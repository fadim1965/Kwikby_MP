namespace kwikby_MP.Api.Infrastructure;

public static class ApiPolicies
{
    public const string Authenticated = "ApiAuthenticated";
    public const string Admin = "ApiAdmin";
    public const string Vendor = "ApiVendor";
    public const string Customer = "ApiCustomer";
}

public static class ApiCors
{
    public const string Policy = "ApiCors";
}

public static class ApiRateLimitPolicies
{
    public const string Api = "api";
}
