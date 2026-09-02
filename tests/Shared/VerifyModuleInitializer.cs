using System.Runtime.CompilerServices;
using DiffEngine;

namespace Shared;

/// <summary>
/// Runs once per test assembly that references this project. Stops Verify from launching an
/// external diff tool when a snapshot mismatches: DiffEngine otherwise spawns the machine's
/// registered diff viewer (Rider here) per failing snapshot, and a run with many mismatches
/// leaves behind dozens of orphaned processes that saturate the CPU and poison any benchmark
/// measured afterwards. Mismatches still fail the test and still write a .received.txt beside
/// the .verified.txt for inspection.
/// </summary>
public static class VerifyModuleInitializer
{
    [ModuleInitializer]
    public static void Initialize() => DiffRunner.Disabled = true;
}
