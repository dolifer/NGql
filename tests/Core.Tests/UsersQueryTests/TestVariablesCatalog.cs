namespace NGql.Core.Tests.UsersQueryTests;

internal static class TestVariablesCatalog
{
    internal static readonly Variable UserIdsVariable = new("$userIds", "[String!]");
    internal static readonly Variable SiteVariable = new("$site", "String!");
    internal static readonly Variable TenantIdVariable = new("$tenant", "String!");
}
