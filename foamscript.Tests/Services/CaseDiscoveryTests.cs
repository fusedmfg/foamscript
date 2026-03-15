using Xunit;
using FluentAssertions;
using foamscript.Services;

namespace foamscript.Tests.Services
{
    public class CaseDiscoveryTests : IDisposable
    {
        private readonly List<string> _tempDirs = new();

        public void Dispose()
        {
            foreach (var dir in _tempDirs)
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
        }

        private string CreateTempDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"foamscript-test-{Guid.NewGuid()}");
            Directory.CreateDirectory(dir);
            _tempDirs.Add(dir);
            return dir;
        }

        [Fact]
        public void IsCase_WithConstantAndSystem_ReturnsTrue()
        {
            var dir = CreateTempDir();
            Directory.CreateDirectory(Path.Combine(dir, "constant"));
            Directory.CreateDirectory(Path.Combine(dir, "system"));

            CaseDiscovery.IsCase(dir).Should().BeTrue();
        }

        [Fact]
        public void IsCase_WithOnlyConstant_ReturnsFalse()
        {
            var dir = CreateTempDir();
            Directory.CreateDirectory(Path.Combine(dir, "constant"));

            CaseDiscovery.IsCase(dir).Should().BeFalse();
        }

        [Fact]
        public void IsCase_EmptyDir_ReturnsFalse()
        {
            var dir = CreateTempDir();

            CaseDiscovery.IsCase(dir).Should().BeFalse();
        }

        [Fact]
        public void IsCase_StudyDirWithSubcases_ReturnsFalse()
        {
            var studyDir = CreateTempDir();
            var caseDir = Path.Combine(studyDir, "Study_0.0");
            Directory.CreateDirectory(Path.Combine(caseDir, "constant"));
            Directory.CreateDirectory(Path.Combine(caseDir, "system"));

            // Study dir itself is NOT a case
            CaseDiscovery.IsCase(studyDir).Should().BeFalse();
            // But the subdir is
            CaseDiscovery.IsCase(caseDir).Should().BeTrue();
        }
    }
}
