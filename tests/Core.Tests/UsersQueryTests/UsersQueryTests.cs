using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using NGql.Core.Builders;
using Shared;
using Xunit;

namespace NGql.Core.Tests.UsersQueryTests;

public class UsersQueryTests
{
    private static readonly Query UsersBatchQuery = new Query("all_users", nameof(UsersBatchQuery))
        .Include("base",
            b => b
                .Where("z", "last")
                .Where("condition", new Dictionary<string, object>
                {
                    { "site", TestVariablesCatalog.SiteVariable },
                    { "tenant_id", TestVariablesCatalog.TenantIdVariable },
                    { "is_test", false },
                })
                .Where("filters", new Dictionary<string, object>
                {
                    { "user_id", new Dictionary<string, object> { { "in", TestVariablesCatalog.UserIdsVariable } } }
                })
                .Where("a", "first")
                .Include("edges", e => e
                    .Include("node", n => n
                        .Select(
                            "UserName:user_name",
                            "Balance:user_balance",
                            "Email:user_email",
                            "UserId:user_id"))));

    private static QueryBuilder UsersBatchQueryBuilderBase => QueryBuilder
        .CreateDefaultBuilder("all_users")
        .AddField("base", new Dictionary<string, object?>
            {
                {
                    "z", "last"
                },
                {
                    "a", "first"
                },
                {
                    "condition", new Dictionary<string, object>
                    {
                        { "site", TestVariablesCatalog.SiteVariable },
                        { "tenant_id", TestVariablesCatalog.TenantIdVariable },
                        { "is_test", false }
                    }
                },
                {
                    "filters", new Dictionary<string, object>
                    {
                        { "user_id", new Dictionary<string, object> { { "in", TestVariablesCatalog.UserIdsVariable } } }
                    }
                }
            }
        );
    
    private static readonly QueryBuilder UsersBatchQueryBuilder = UsersBatchQueryBuilderBase
        .AddField("base.edges.node", [
            "UserName:user_name",
            "Balance:user_balance",
            "Email:user_email",
            "UserId:user_id"
        ]);
    
    private static QueryBuilder UsersBatchQueryChild => QueryBuilder
        .CreateDefaultBuilder("all_users")
        .AddField("base.edges.node", [
            "UserName:user_name",
            "Balance:user_balance",
            "Email:user_email",
            "UserId:user_id"
        ]);

    private static readonly QueryBuilder UsersBatchQueryMerged = UsersBatchQueryBuilderBase
        .Include(UsersBatchQueryChild);
    
    private static QueryBuilder UsersNameBalanceQueryChild => QueryBuilder
        .CreateDefaultBuilder("all_users")
        .AddField("base.edges.node", [
            "UserName:user_name",
            "Balance:user_balance",
        ]);

    private static QueryBuilder UsersEmailUserIdChild => QueryBuilder
        .CreateDefaultBuilder("all_users")
        .AddField("base.edges.node", [
            "Email:user_email",
            "UserId:user_id"
        ]);

    private static readonly QueryBuilder UsersBatchQueryMergedTwice = UsersBatchQueryBuilderBase
        .Include(UsersNameBalanceQueryChild)
        .Include(UsersEmailUserIdChild);
    
    [Fact]
    public async Task UsersBatchQuery_Manual() => await VerifyBetsQuery(UsersBatchQuery);

    [Fact]
    public async Task UsersBatchQuery_Builder()
    {
        var builderQuery = UsersBatchQueryBuilder;
        
        await VerifyBetsQuery(builderQuery);
    }
    
    [Fact]
    public async Task UsersBatchQueryMerged_Builder()
    {
        var builderQuery = UsersBatchQueryMerged;
        
        await VerifyBetsQuery(builderQuery);
    }
    
    [Fact]
    public async Task UsersBatchQueryMergedTwice_Builder()
    {
        var builderQuery = UsersBatchQueryMergedTwice;
        
        await VerifyBetsQuery(builderQuery);
    }

    private static async Task VerifyBetsQuery(QueryBuilder query)
    {
        await query.Verify("usersBatchQuery");
        
        query.Variables.Should().BeEquivalentTo([TestVariablesCatalog.SiteVariable, TestVariablesCatalog.TenantIdVariable, TestVariablesCatalog.UserIdsVariable]);
    }
    
    private static async Task VerifyBetsQuery(Query query)
    {
        await query.Verify("usersBatchQuery");
        
        query.Variables.Should().BeEquivalentTo([TestVariablesCatalog.SiteVariable, TestVariablesCatalog.TenantIdVariable, TestVariablesCatalog.UserIdsVariable]);
    }
}
