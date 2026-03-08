using Xunit;
using FluentAssertions;
using foamscript.Services;

namespace foamscript.Tests.Services
{
    public class CoreResolverTests : IDisposable
    {
        private readonly string? _originalEnvValue;

        public CoreResolverTests()
        {
            _originalEnvValue = Environment.GetEnvironmentVariable(CoreResolver.MaxCoresEnvVar);
        }

        public void Dispose()
        {
            // Restore original env var state
            if (_originalEnvValue != null)
                Environment.SetEnvironmentVariable(CoreResolver.MaxCoresEnvVar, _originalEnvValue);
            else
                Environment.SetEnvironmentVariable(CoreResolver.MaxCoresEnvVar, null);
        }

        // ── Resolve — Explicit cores ───────────────────────────────────────────

        [Fact]
        public void Resolve_ExplicitCores_UsesSpecifiedValue()
        {
            var (cores, _) = CoreResolver.Resolve(4);
            cores.Should().Be(4);
        }

        [Fact]
        public void Resolve_SingleCore_ReturnsSerialFalse()
        {
            var (cores, parallel) = CoreResolver.Resolve(1);
            cores.Should().Be(1);
            parallel.Should().BeFalse();
        }

        [Fact]
        public void Resolve_MultipleCores_ReturnsParallelTrue()
        {
            var (cores, parallel) = CoreResolver.Resolve(4);
            cores.Should().Be(4);
            parallel.Should().BeTrue();
        }

        // ── Resolve — Auto-detect (cores == 0) ────────────────────────────────

        [Fact]
        public void Resolve_ZeroCores_AutoDetectsProcessorCount()
        {
            Environment.SetEnvironmentVariable(CoreResolver.MaxCoresEnvVar, null);

            var (cores, _) = CoreResolver.Resolve(0);
            cores.Should().Be(Environment.ProcessorCount);
        }

        [Fact]
        public void Resolve_ZeroCores_ParallelWhenMultiCore()
        {
            Environment.SetEnvironmentVariable(CoreResolver.MaxCoresEnvVar, null);

            var (cores, parallel) = CoreResolver.Resolve(0);

            if (Environment.ProcessorCount > 1)
                parallel.Should().BeTrue();
            else
                parallel.Should().BeFalse();
        }

        // ── FOAMSCRIPT_MAX_CORES env var ───────────────────────────────────────

        [Fact]
        public void Resolve_MaxCoresEnvVar_CapsAutoDetection()
        {
            Environment.SetEnvironmentVariable(CoreResolver.MaxCoresEnvVar, "2");

            var (cores, _) = CoreResolver.Resolve(0);
            cores.Should().BeLessThanOrEqualTo(2);
        }

        [Fact]
        public void Resolve_MaxCoresEnvVar_DoesNotAffectExplicitCores()
        {
            Environment.SetEnvironmentVariable(CoreResolver.MaxCoresEnvVar, "2");

            var (cores, _) = CoreResolver.Resolve(8);
            cores.Should().Be(8);
        }

        [Fact]
        public void Resolve_MaxCoresEnvVar_InvalidValue_IgnoresEnvVar()
        {
            Environment.SetEnvironmentVariable(CoreResolver.MaxCoresEnvVar, "not-a-number");

            var (cores, _) = CoreResolver.Resolve(0);
            cores.Should().Be(Environment.ProcessorCount);
        }

        [Fact]
        public void Resolve_MaxCoresEnvVar_ZeroValue_IgnoresEnvVar()
        {
            Environment.SetEnvironmentVariable(CoreResolver.MaxCoresEnvVar, "0");

            var (cores, _) = CoreResolver.Resolve(0);
            cores.Should().Be(Environment.ProcessorCount);
        }

        [Fact]
        public void Resolve_MaxCoresEnvVar_NegativeValue_IgnoresEnvVar()
        {
            Environment.SetEnvironmentVariable(CoreResolver.MaxCoresEnvVar, "-4");

            var (cores, _) = CoreResolver.Resolve(0);
            cores.Should().Be(Environment.ProcessorCount);
        }

        [Fact]
        public void Resolve_MaxCoresEnvVar_LargerThanAvailable_UsesAvailable()
        {
            Environment.SetEnvironmentVariable(CoreResolver.MaxCoresEnvVar, "9999");

            var (cores, _) = CoreResolver.Resolve(0);
            cores.Should().Be(Environment.ProcessorCount);
        }
    }
}
