using System;

namespace MeditationApp.Models
{
    public class BreathingTechnique
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int InhaleDuration { get; set; } // in seconds
        public int InhaleHoldDuration { get; set; } // in seconds
        public int ExhaleDuration { get; set; } // in seconds
        public int ExhaleHoldDuration { get; set; } // in seconds
        public int Cycles { get; set; } = 10; // default number of cycles
        public string Instructions { get; set; } = string.Empty;
        public string Benefits { get; set; } = string.Empty;
        public string IconColor { get; set; } = "#4CAF50";
        public bool IsCustom { get; set; } = false;

        // Pre-defined techniques
        public static BreathingTechnique FourSevenEight => new()
        {
            Id = 1,
            Name = "4-7-8 Technique",
            Description = "Inhale for 4, hold for 7, exhale for 8",
            InhaleDuration = 4,
            InhaleHoldDuration = 7,
            ExhaleDuration = 8,
            ExhaleHoldDuration = 0,
            Cycles = 8,
            Instructions = "This technique helps reduce anxiety and promotes sleep. Inhale quietly through your nose for 4 counts, hold for 7, then exhale through your mouth for 8.",
            Benefits = "Reduces anxiety, promotes sleep, calms nervous system",
            IconColor = "#2196F3"
        };

        public static BreathingTechnique BoxBreathing => new()
        {
            Id = 2,
            Name = "Box Breathing",
            Description = "Equal timing: 4-4-4-4",
            InhaleDuration = 4,
            InhaleHoldDuration = 4,
            ExhaleDuration = 4,
            ExhaleHoldDuration = 4,
            Cycles = 10,
            Instructions = "Also known as Square Breathing. Inhale for 4, hold for 4, exhale for 4, hold for 4. Used by Navy SEALs for stress management.",
            Benefits = "Improves focus, reduces stress, enhances performance",
            IconColor = "#FF9800"
        };

        public static BreathingTechnique EqualBreathing => new()
        {
            Id = 3,
            Name = "Equal Breathing",
            Description = "Same length inhale and exhale",
            InhaleDuration = 5,
            InhaleHoldDuration = 0,
            ExhaleDuration = 5,
            ExhaleHoldDuration = 0,
            Cycles = 15,
            Instructions = "Simple and effective. Breathe in and out for equal counts. Start with 5 seconds each and adjust as comfortable.",
            Benefits = "Balances nervous system, improves concentration",
            IconColor = "#4CAF50"
        };

        public static BreathingTechnique RelaxingBreath => new()
        {
            Id = 4,
            Name = "Relaxing Breath",
            Description = "Quick exhale for instant calm",
            InhaleDuration = 4,
            InhaleHoldDuration = 0,
            ExhaleDuration = 6,
            ExhaleHoldDuration = 0,
            Cycles = 12,
            Instructions = "Focus on longer exhales to activate your parasympathetic nervous system for immediate relaxation.",
            Benefits = "Quick stress relief, immediate calm, better sleep preparation",
            IconColor = "#9C27B0"
        };

        public static BreathingTechnique EnergizingBreath => new()
        {
            Id = 5,
            Name = "Energizing Breath",
            Description = "Quick inhales for energy boost",
            InhaleDuration = 6,
            InhaleHoldDuration = 2,
            ExhaleDuration = 4,
            ExhaleHoldDuration = 0,
            Cycles = 8,
            Instructions = "Longer inhales help energize your body and mind. Perfect for morning routines or when you need a natural energy boost.",
            Benefits = "Increases energy, improves alertness, enhances mood",
            IconColor = "#FF5722"
        };

        public static List<BreathingTechnique> GetPredefinedTechniques()
        {
            return new List<BreathingTechnique>
            {
                FourSevenEight,
                BoxBreathing,
                EqualBreathing,
                RelaxingBreath,
                EnergizingBreath
            };
        }
    }
}
