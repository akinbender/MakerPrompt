using Xunit.Abstractions;
using Xunit.Sdk;

namespace MakerPrompt.E2E.Maui.Fixtures;

/// <summary>
/// Orders test cases alphabetically by class name then method name.
/// This ensures AppLaunchTests runs before ThemeAndLanguageTests etc.
/// </summary>
public class AlphabeticalOrderer : ITestCaseOrderer
{
    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases)
        where TTestCase : ITestCase
    {
        return testCases.OrderBy(tc => tc.TestMethod.TestClass.Class.Name)
                        .ThenBy(tc => tc.TestMethod.Method.Name);
    }
}
