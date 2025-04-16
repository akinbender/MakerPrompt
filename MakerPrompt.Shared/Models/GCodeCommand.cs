using MakerPrompt.Shared.Properties;
using System.ComponentModel.DataAnnotations;

namespace MakerPrompt.Shared.Models
{
    public partial class GCodeCommand(string command, string description, List<GCodeCategory> categories)
    {
        public GCodeCommand(string command, string description, List<GCodeCategory> categories, List<GCodeParameter> parameters)
            : this(command, description, categories)
        {
            Parameters = parameters;
        }

        [Display(Name = nameof(Resources.GCodeCommand_Command), ResourceType = typeof(Resources))]
        public string Command { get; } = command;

        [Display(Name = nameof(Resources.GCodeCommand_Description), ResourceType = typeof(Resources))]
        public string Description { get; } = description;

        [Display(Name = nameof(Resources.GCodeCommand_Category), ResourceType = typeof(Resources))]
        public List<GCodeCategory> Categories { get; } = categories;

        [Display(Name = nameof(Resources.GCodeCommand_Parameter), ResourceType = typeof(Resources))]
        public List<GCodeParameter> Parameters { get; } = [];

        public override string ToString()
        {
            if (Parameters == null || Parameters.Count == 0)
                return Command;

            return $"{Command} {string.Join(" ", Parameters
                .Where(p => !string.IsNullOrEmpty(p.Value))
                .Select(p => $"{p.Label}{p.Value}"))}";
        }
    }
}
