using System.Collections.Generic;
using NUnit.Framework;

namespace Epacenur.Tests
{
    /// Runs the rules suite in Unity's Test Runner (Window > General > Test Runner > EditMode).
    public class CoreRulesTests
    {
        [Test]
        public void AllRulesHold()
        {
            var failures = new List<string>();
            CoreRulesSuite.Run((ok, what) => { if (!ok) failures.Add(what); }, _ => { });
            Assert.IsEmpty(failures, "Failed:\n" + string.Join("\n", failures));
        }
    }
}
