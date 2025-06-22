using NGql.Core;
using VerifyTests;
using VerifyXunit;

namespace Shared;

public static class VerifyExtensions
{
    private static VerifySettings GetSettings(string filename)
    {
        var settings = new VerifySettings();
        
        settings.UseDirectory("snapshots");
        settings.UseFileName(filename);
        settings.DisableRequireUniquePrefix();
        
        return settings;
    }
    
    public static SettingsTask Verify(this Mutation mutation, string filename)
    {
        var settings = GetSettings(filename);
        
        return Verifier.Verify(mutation, settings);
    }
    
    public static SettingsTask Verify(this Query query, string filename)
    {
        var settings = GetSettings(filename);
        
        return Verifier.Verify(query, settings);
    }
    
    public static SettingsTask Verify(this QueryBuilder queryBuilder, string filename)
    {
        var settings = GetSettings(filename);
        
        return Verifier.Verify(queryBuilder, settings);
    }
}
