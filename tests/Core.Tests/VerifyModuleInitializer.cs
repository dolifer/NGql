using System.Runtime.CompilerServices;
using DiffEngine;

namespace NGql.Core.Tests;

/// <summary>
/// Runs once when this test assembly loads. Stops Verify from launching an external diff tool
/// when a snapshot mismatches: DiffEngine otherwise spawns the machine's registered diff viewer
/// (Rider here) per failing snapshot, and a run with many mismatches leaves behind dozens of
/// orphaned processes that saturate the CPU and poison any benchmark measured afterwards.
/// Mismatches still fail the test and still write a .received.txt beside the .verified.txt.
/// </summary>
public static class VerifyModuleInitializer
{
    [ModuleInitializer]
    public static void Initialize() => DiffRunner.Disabled = true;
}
