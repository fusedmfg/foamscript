using System.Runtime.CompilerServices;

namespace foamscript.Tests
{
    /// <summary>
    /// Module initializer to suppress FluentAssertions license warning
    /// </summary>
    internal static class TestSetup
    {
        [ModuleInitializer]
        public static void Initialize()
        {
            // Suppress FluentAssertions license warning for test runs
            Environment.SetEnvironmentVariable("FLUENTASSERTIONS_NOLICENSEBANNER", "1");
        }
    }
}
