using System.Text.Json;
using Xunit;
using FluentAssertions;
using foamscript.Models;

namespace foamscript.Tests.Models
{
    public class FoamScriptConfigTests
    {
        [Fact]
        public void RoundTrips_ThroughJson()
        {
            var config = new FoamScriptConfig
            {
                OpenFoamBashrc = "/usr/lib/openfoam/openfoam2512/etc/bashrc",
                OpenFoamVersion = "v2512",
                ConfiguredAt = new DateTime(2026, 3, 16, 20, 0, 0, DateTimeKind.Utc)
            };

            var json = JsonSerializer.Serialize(config, FoamScriptConfig.JsonOptions);
            var deserialized = JsonSerializer.Deserialize<FoamScriptConfig>(json, FoamScriptConfig.JsonOptions);

            deserialized!.OpenFoamBashrc.Should().Be(config.OpenFoamBashrc);
            deserialized.OpenFoamVersion.Should().Be(config.OpenFoamVersion);
            deserialized.ConfiguredAt.Should().Be(config.ConfiguredAt);
        }

        [Fact]
        public void ConfigPath_IsUnderUserHome()
        {
            FoamScriptConfig.ConfigPath.Should().Contain(".foamscript");
            FoamScriptConfig.ConfigPath.Should().EndWith("config.json");
        }
    }
}
