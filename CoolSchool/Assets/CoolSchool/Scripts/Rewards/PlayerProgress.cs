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
        public string equippedSword = "";   // "" = the starter sword

        // What else he wears or carries. Chosen in the present popup or the Toy Room wardrobe.
        public string equippedShield = "";   // "" = the starter shield
        public string equippedHat = "";      // "" = no hat
        public string equippedBuddy = "";    // "" = automatic: the parrot if owned (today's behaviour); "none" = no buddy
        public string equippedCart = "";     // "" = automatic: the best owned body (today's behaviour)
        public List<string> cartExtrasOff = new List<string>();  // owned cart extras switched off in the picker
        public List<string> cartsTried = new List<string>();     // cart bodies/extras used in a race at least once
        public List<string> unseenItems = new List<string>();    // won, not yet looked at in the Toy Room
        public bool toyRoomVisited;
        public int medals;       // medals won after the whole catalog was collected
        public int medalsSeen;   // how many of them the Toy Room has shown

        public List<SkillLevel> skillLevels = new List<SkillLevel>();
        // A power-up won in a lesson that the next game uses up, e.g. golden_sword.
        public string pendingPowerUp = "";

        // The ice cream shop: how big the orders are (1 to 3), and whether the how-to line has been heard once.
        public int shopLevel = 1;
        public bool shopHowtoHeard;

        // Pirate cove: the treasure coins counted so far in total (the trophy chest by the dock fills up with them).
        public int pirateCoinsTotal;

        // Soapbox race: whether the how-to line has been heard once, and the coins collected in all races so far.
        public bool raceHowtoHeard;
        public int raceCoinsTotal;

        // The child's name: lowercase letters a-z only, at most 12. Empty means not set. Set through ProgressStore.SetChildName.
        public string childName = "";

        // The letters lesson: letters already introduced (in order), first-try-correct counts per letter
        // (SkillLevel.skill is the letter, SkillLevel.level its count), the lessons since the last new letter,
        // and which subject the classroom taught last ("numbers" or "letters") so the two alternate.
        public List<string> lettersIntroduced = new List<string>();
        public List<SkillLevel> letterScores = new List<SkillLevel>();
        public int lessonsSinceNewLetter;
        public string lastLessonSubject = "";

        // Hide and seek in the zoo park: the animal kinds (ZooAnimalKind ids) he has found at least once (their pens stay filled), and the rounds played.
        public List<string> zooAnimals = new List<string>();
        public int zooRounds;

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
