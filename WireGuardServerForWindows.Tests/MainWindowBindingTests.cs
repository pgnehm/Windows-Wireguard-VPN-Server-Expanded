using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace WireGuardServerForWindows.Tests
{
    public class MainWindowBindingTests
    {
        [Fact]
        public void ProgressSummaryBindingsShouldNotWriteToReadOnlyProperties()
        {
            string xamlPath = Path.Combine(
                GetRepositoryRoot(),
                "WireGuardServerForWindows",
                "MainWindow.xaml");

            XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
            XDocument document = XDocument.Load(xamlPath);

            XElement progressBar = document.Descendants(presentation + "ProgressBar").Single();

            progressBar.Attribute("Maximum")?.Value.Should().Contain("Mode=OneWay");
            progressBar.Attribute("Value")?.Value.Should().Contain("Mode=OneWay");
        }

        [Fact]
        public void MainWindowShouldExposeGuidedSetupButton()
        {
            string xamlPath = Path.Combine(
                GetRepositoryRoot(),
                "WireGuardServerForWindows",
                "MainWindow.xaml");

            XDocument document = XDocument.Load(xamlPath);

            document.ToString().Should().Contain("Guided setup");
            document.ToString().Should().Contain("GuidedSetupButton_Click");
        }

        private static string GetRepositoryRoot([CallerFilePath] string sourcePath = "")
        {
            return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, ".."));
        }
    }
}
