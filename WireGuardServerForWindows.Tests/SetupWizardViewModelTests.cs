using System;
using FluentAssertions;
using WireGuardServerForWindows;
using WireGuardServerForWindows.Models;
using Xunit;

namespace WireGuardServerForWindows.Tests
{
    public class SetupWizardViewModelTests
    {
        [Fact]
        public void ShouldCreateGuidedStepsFromMainChecklist()
        {
            var model = new MainWindowModel();
            model.PrerequisiteItems.Add(new FakePrerequisite("WireGuard.exe"));
            model.PrerequisiteItems.Add(new FakePrerequisite("Server Configuration"));
            model.PrerequisiteItems.Add(new FakePrerequisite("Client Configuration(s)"));

            var wizard = new SetupWizardViewModel(model);

            wizard.Steps.Should().HaveCount(3);
            wizard.CurrentStep.Title.Should().Be("WireGuard.exe");
            wizard.CurrentStep.PlainLanguageInstructions.Should().Contain("VPN engine");
            wizard.StepCounter.Should().Be("Step 1 of 3");
        }

        [Fact]
        public void ShouldMoveThroughSteps()
        {
            var model = new MainWindowModel();
            model.PrerequisiteItems.Add(new FakePrerequisite("WireGuard.exe"));
            model.PrerequisiteItems.Add(new FakePrerequisite("Server Configuration"));

            var wizard = new SetupWizardViewModel(model);

            wizard.NextCommand.Execute(null);

            wizard.CurrentStep.Title.Should().Be("Server Configuration");
            wizard.StepCounter.Should().Be("Step 2 of 2");
            wizard.CanGoBack.Should().BeTrue();
            wizard.CanGoNext.Should().BeFalse();
        }

        private sealed class FakePrerequisite : PrerequisiteItem
        {
            public FakePrerequisite(string title)
                : base(title, "Done", "Not done", "Fix", "Configure")
            {
                HelpText = "Plain language help.";
            }

            public override BooleanTimeCachedProperty Fulfilled { get; } =
                new BooleanTimeCachedProperty(TimeSpan.Zero, () => false);

            public override void Resolve()
            {
            }

            public override void Configure()
            {
            }
        }
    }
}
