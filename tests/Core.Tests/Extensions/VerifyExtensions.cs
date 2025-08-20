using System;
using System.IO;
using System.Runtime.CompilerServices;
using NGql.Core.Builders;
using VerifyTests;
using VerifyXunit;

namespace NGql.Core.Tests.Extensions;

public static class VerifyExtensions
{
    private static VerifySettings GetSettings(string? filename)
    {
        var settings = new VerifySettings();
       
        // Get the file path of the currently executing test
        var testFilePath = new System.Diagnostics.StackFrame(2, true).GetFileName();
        if (string.IsNullOrEmpty(testFilePath))
        {
            throw new InvalidOperationException("Unable to determine the test file path.");
        }

        // Get the directory of the test file
        var testDirectory = Path.GetDirectoryName(testFilePath);

        if (string.IsNullOrEmpty(testDirectory))
        {
            throw new InvalidOperationException("Unable to determine the test file directory.");
        }

        var directory = Path.Combine(testDirectory, "snapshots");

        settings.UseDirectory(directory);
        
        if (!string.IsNullOrEmpty(filename))
            settings.UseFileName(filename);

        settings.DisableRequireUniquePrefix();
        
        return settings;
    }

    public static SettingsTask Verify(this Query query, [CallerMemberName]string? filename=null)
    {
        var settings = GetSettings(filename);
        
        return Verifier.Verify(query, settings);
    }

    public static SettingsTask Verify(this QueryBuilder queryBuilder, [CallerMemberName]string? filename=null)
    {
        var settings = GetSettings(filename);
        
        return Verifier.Verify(queryBuilder, settings);
    }
}
