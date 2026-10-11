using System;
using System.Collections.Generic;

namespace CoolSchool.Rewards
{
    /// <summary>How far the child has got in one skill, for example "numbers" at level 3.</summary>
    [Serializable]
    public class SkillLevel
    {
        public string skill;
        public int level;
    }

    /// <summary>Everything the game remembers about the child between sessions. Saved as JSON by ProgressStore.</summary>
    [Serializable]
    public class PlayerProgress
    {
        public int starsInJar;
        public int totalStars;
        public int presentsOpened;
        public List<string> unlockedItems = new List<string>();
        public string equippedSword = "";

        public List<SkillLevel> skillLevels = new List<SkillLevel>();
        // A power-up won in a lesson that the next game uses up, e.g. golden_sword.
        public string pendingPowerUp = "";

        public bool IsUnlocked(string id)
        {
            return unlockedItems != null && unlockedItems.Contains(id);
        }

        /// <summary>The level in this skill, starting at 1 when the child has not played it yet.</summary>
        public int GetLevel(string skill)
        {
            SkillLevel found = FindSkill(skill);
            return found != null ? Math.Max(1, found.level) : 1;
        }

        public void SetLevel(string skill, int level)
        {
            if (skillLevels == null) skillLevels = new List<SkillLevel>();

            SkillLevel found = FindSkill(skill);
            if (found == null)
            {
                found = new SkillLevel { skill = skill };
                skillLevels.Add(found);
            }
            found.level = level;
        }

        SkillLevel FindSkill(string skill)
        {
            if (skillLevels == null) return null;

            foreach (SkillLevel entry in skillLevels)
                if (entry != null && entry.skill == skill) return entry;
            return null;
        }
    }
}
