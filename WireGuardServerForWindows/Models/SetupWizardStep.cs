namespace WireGuardServerForWindows.Models
{
    public class SetupWizardStep
    {
        public SetupWizardStep(PrerequisiteItem prerequisiteItem, string plainLanguageInstructions, string actionText)
        {
            PrerequisiteItem = prerequisiteItem;
            PlainLanguageInstructions = plainLanguageInstructions;
            ActionText = actionText;
        }

        public PrerequisiteItem PrerequisiteItem { get; }

        public string Title => PrerequisiteItem.Title;

        public string PlainLanguageInstructions { get; }

        public string ActionText { get; }

        public string StatusText => PrerequisiteItem.IsInformational
            ? PrerequisiteItem.SuccessMessage
            : PrerequisiteItem.Fulfilled
                ? PrerequisiteItem.SuccessMessage
                : PrerequisiteItem.ErrorMessage;
    }
}
