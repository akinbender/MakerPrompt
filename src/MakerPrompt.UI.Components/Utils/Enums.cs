using System.ComponentModel.DataAnnotations;
using System.Resources;

namespace MakerPrompt.UI.Components.Utils
{
    public class Enums
    {
        public enum MicrosteppingMode
        {
            [Display(Name = "1/1 (Full step)")]
            FullStep = 1,
            [Display(Name = "1/2 (Half step)")]
            HalfStep = 2,
            [Display(Name = "1/4")]
            QuarterStep = 4,
            [Display(Name = "1/8")]
            EighthStep = 8,
            [Display(Name = "1/16")]
            SixteenthStep = 16,
            [Display(Name = "1/32")]
            ThirtySecondStep = 32,
            [Display(Name = "1/64")]
            SixtyFourthStep = 64,
            [Display(Name = "1/128")]
            OneTwentyEighthStep = 128
        }

        public enum MotorStepAngle
        {
            [Display(Name = "1.8° (200 steps/rev)")]
            Step1_8 = 180, // 1.8° in tenths of degrees for precision
            [Display(Name = "0.9° (400 steps/rev)")]
            Step0_9 = 90,
            [Display(Name = "7.5° (48 steps/rev)")]
            Step7_5 = 75,
            [Display(Name = "15° (24 steps/rev)")]
            Step15 = 150
        }

        public enum Theme
        {
            [Display(Name = nameof(Resources.Theme_Auto), ResourceType = typeof(Resources))]
            Auto,
            [Display(Name = nameof(Resources.Theme_Light), ResourceType = typeof(Resources))]
            Light,
            [Display(Name = nameof(Resources.Theme_Dark), ResourceType = typeof(Resources))]
            Dark,
            [Display(Name = "MakerPrompt")]
            MpDark
        }

        public enum GCodeCategory
        {
            [Display(Name = nameof(Resources.GCodeCategory_Temperature), ResourceType = typeof(Resources))]
            Temperature,

            [Display(Name = nameof(Resources.GCodeCategory_Movement), ResourceType = typeof(Resources))]
            Movement,

            [Display(Name = nameof(Resources.GCodeCategory_Fan), ResourceType = typeof(Resources))]
            FanControl,

            [Display(Name = nameof(Resources.GCodeCategory_Settings), ResourceType = typeof(Resources))]
            Settings,

            [Display(Name = nameof(Resources.GCodeCategory_Reporting), ResourceType = typeof(Resources))]
            Reporting,

            [Display(Name = nameof(Resources.GCodeCategory_Calibration), ResourceType = typeof(Resources))]
            Calibration,

            [Display(Name = nameof(Resources.GCodeCategory_SdCard), ResourceType = typeof(Resources))]
            SDCard
        }
    }

    public static class EnumExtensions
    {
        public static IEnumerable<T> GetAllValues<T>() where T : Enum
        {
            return Enum.GetValues(typeof(T)).Cast<T>();
        }

        public static List<KeyValuePair<MotorStepAngle, string>> GetMotorStepAngleOptions()
        {
            return [.. GetAllValues<MotorStepAngle>().Select(e => new KeyValuePair<MotorStepAngle, string>(e, e.GetDisplayName()))];
        }

        public static List<KeyValuePair<MicrosteppingMode, string>> GetMicrosteppingOptions()
        {
            return [.. GetAllValues<MicrosteppingMode>().Select(e => new KeyValuePair<MicrosteppingMode, string>(e, e.GetDisplayName()))];
        }

        public static decimal GetStepAngleValue(this MotorStepAngle angle)
        {
            return (decimal)angle / 100m;
        }

        public static string GetDisplayName(this Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attribute = field?.GetCustomAttribute<DisplayAttribute>();
            return attribute?.Name?? value.ToString();
        }

        public static string GetLocalizedDisplayName(this Enum value)
        {
            var rm = new ResourceManager(typeof(Resources));
            var name = value.GetDisplayName();
            var resourceKey = value switch
            {
                PrinterStatus status => $"PrinterStatus_{status}",
                _ => name
            };
            var resourceDisplayName = rm.GetString(resourceKey);

            return string.IsNullOrWhiteSpace(resourceDisplayName) ? name : resourceDisplayName;
        }
    }
}
