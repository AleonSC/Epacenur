using System;
using Epacenur.Tests;

/// Runs the rules suite outside Unity (Tools/Tests/run.sh).
static class Program
{
    static int Main()
    {
        int failures = 0;
        CoreRulesSuite.Run((ok, what) =>
        {
            Console.WriteLine((ok ? "  ok   " : "  FAIL ") + what);
            if (!ok)
                failures++;
        }, Console.WriteLine);
        Console.WriteLine(failures == 0 ? "All passed." : failures + " FAILED.");
        return failures == 0 ? 0 : 1;
    }
}
