using System;
using System.Collections.Generic;
using System.Text;
using GP.Localisation.Live;
using GP.Storage;
using Microsoft.Xna.Framework.GamerServices;  // <-- only LiveManagers should use objects in this namespace
using Microsoft.Xna.Framework.Graphics;

namespace GP.Live_Integration
{
   /// <summary>
   /// Achievement manager for Local saving of achievements
   /// Maintains two lists of achievements:
   ///   _achievementsGeneric = publically visible list of platform-independent achievements
   ///   _achievementsLocal = private to this class, collection of platform-specific achievements
   /// </summary>
   public class AchievementManagerLocal : LiveManager, IAchievementManager
   {
      /// <summary>
      /// Event raised when achievement list changes, eg perhaps an achievement got awarded
      /// </summary>
      public virtual event EventHandler<EventArgs> OnAchievementDataChanged = delegate { };

      /// <summary>
      /// Outwardly visible list of achievements, defined in a platform-independent manner
      /// </summary>
      public List<AchievementGeneric> AchievementsGeneric { get { return _achievementsGeneric; } }
      private List<AchievementGeneric> _achievementsGeneric;

      private Texture2D _achOneExplode, _achEinstein, _achWipeOut8, _achSpin, _achOneDisint;
      private Texture2D _achComplete50, _achMadDisint, _achWipeOut16, _achPenLove, _achComplete100;
      private Texture2D _achGold36, _achGerbilSpeed, _achGold72, _achBombParty, _achFirstPickup;
      private Texture2D _achGold01, _achWipeOut01, _achFirstAlarmGerbils, _achScore01, _achScore02;

      /// <summary>
      /// Achievements are written locally
      /// </summary>
      AchievementData _achievementDataFromFile = new AchievementData();

      ToastNotificationSystem _toastNotificationSystem;

      public AchievementManagerLocal(ScreenManager screenManager)
         : base(screenManager)
      {
         Logging.WriteLine("LOG: Using AchievementManagerLocal for local offline achievements");
      }

      public override void Initialize()
      {
         base.Initialize();
       
         // Our local ref to toast
         _toastNotificationSystem = (ToastNotificationSystem)GerbilPhysicsGame.GameServices.GetService(typeof(ToastNotificationSystem));

         _achievementsGeneric = new List<AchievementGeneric>();
      }

      public override void Dispose()
      {
         base.Dispose();
      }

      public override void LoadContent()
      {
         base.LoadContent();
      }

      public override void LoadContentDeferred()
      {
         base.LoadContentDeferred();

         //---------------------------------------------------------
         // Prepare the master list of achievements
         //---------------------------------------------------------
         // Achievements content is all loaded with IsEarned = false
         PopulateAchievements();

         //---------------------------------------------------------
         // Load the local list of already-awarded achievements
         //---------------------------------------------------------   
         StorageManager sm = _screenManager.StorageManager;
         try
         {
            _achievementDataFromFile = (AchievementData)sm.Load(_achievementDataFromFile.GetType());
            if (_achievementDataFromFile.Count > 0)
            {
               // Mark the awarded achievements as awarded so they're visible in displayed list
               for (int i = 0; i < _achievementDataFromFile.Count; i++)
               {
                  AwardAchievementToDisplayedList(_achievementDataFromFile.AchievementKeyNames[i]);
               }
            }
         }
         catch (Exception e)
         {
            Logging.WriteLine("ERROR: AchievementsManagerLocal.LoadContentDeferred failed, could not read file: {0}", e.Message);

            // File is corrupt, trash it and bail
            DeleteAchievementsSaveDataFile();
            return;
         }
         finally
         {
         }

         // Whatever happened, we've loaded a list of achievements
         OnAchievementDataChanged(this, EventArgs.Empty);

         _contentLoaded = true;
      }

      private void AwardAchievementToDisplayedList(string achievementKey)
      {
         bool foundAchievement = false;
         if (_achievementsGeneric != null)
         {
            foreach (var achievementGeneric in _achievementsGeneric)
            {
               if (achievementGeneric.Key == achievementKey)
               {
                  Logging.WriteLine("LOG: AwardAchievementToDisplayedList has awarded: {0}", achievementKey);
                  achievementGeneric.IsEarned = true;
                  foundAchievement = true;
                  break;
               }
            }
         }
         if (!foundAchievement)
         {
            Logging.WriteLine("WARNING: AwardAchievementToDisplayedList could not find achievement: {0}", achievementKey);
         }
      }

      private AchievementGeneric GetAchievement(string achievementKey)
      {
         AchievementGeneric ach = null;
         bool foundAchievement = false;
         if (_achievementsGeneric != null)
         {
            foreach (var achievementGeneric in _achievementsGeneric)
            {
               if (achievementGeneric.Key == achievementKey)
               {
                  ach = achievementGeneric;
                  foundAchievement = true;
                  break;
               }
            }
         }
         if (!foundAchievement)
         {
            Logging.WriteLine("WARNING: GetAchievement could not find achievement: {0}", achievementKey);
         }
         return ach;
      }

      private void UnAwardAllAchievementsFromDisplayedList()
      {
         Logging.WriteLine("LOG: UnAwardAllAchievementsFromDisplayedList is setting IsEarned to false");
         if (_achievementsGeneric != null)
         {
            foreach (var achievementGeneric in _achievementsGeneric)
            {
               achievementGeneric.IsEarned = false;
            }
         }
      }

      public override void Update(float elapsedTime)
      {
         base.Update(elapsedTime);
      }

      public void DeleteAchievementsSaveDataFile()
      {
         StorageManager sm = _screenManager.StorageManager;

         // Delete the file
         if (sm.DeleteFile(_achievementDataFromFile.GetType()))
         {
            // Delete ok
            Logging.WriteLine("LOG: DeleteAchievementsSaveDataFile has deleted local achievements file");
         }
         else
         {
            Logging.WriteLine("LOG: DeleteAchievementsSaveDataFile couldnt delete file, perhaps it didnt exist");
         }

         UnAwardAllAchievementsFromDisplayedList();
      }

      public void AwardAchievement(string achievementName)
      {
         Logging.WriteLine("LOG: AchievementManager received request to award {0}", achievementName);

         // Store achievement in file, if it is indeed new
         bool isThisANewAchievement = AwardAchievementToSaveDataFile(achievementName);

         // If achievement is genuinely new, then its toast time
         if (isThisANewAchievement)
         {
            // Mark achievement as awarded in the display list
            AwardAchievementToDisplayedList(achievementName);

            // Toast
            AchievementGeneric ach = GetAchievement(achievementName);
            if (ach != null)
            {
               _toastNotificationSystem.CreateToastRequest("Achievement Earned!", ach.Name, ach.Image);
            }

            // Tell our listeners
            OnAchievementDataChanged(this, EventArgs.Empty);
         }
      }

      private bool AwardAchievementToSaveDataFile(string achievementNameNew)
      {
         bool isThereANewAchievement = false;

         // Read file, if it exists, into a local list
         StorageManager sm = _screenManager.StorageManager;
         AchievementData achievementDataFromFile = new AchievementData();
         List<string> achievementDataAsStringList = new List<string>();

         if (_screenManager.StorageManager.FileExists(achievementDataFromFile.GetType()))
         {
            // Load the local achievements
            try
            {
               achievementDataFromFile = (AchievementData)sm.Load(achievementDataFromFile.GetType());
            }
            catch (Exception e)
            {
               Logging.WriteLine("ERROR: AwardAchievementToSaveDataFile could not read file, even though it exists: {0}", e.Message);

               // File is corrupt, trash it and bail               
               DeleteAchievementsSaveDataFile();
               return isThereANewAchievement;
            }
         }
         // Store any achievement we got from the file in a local list
         if (achievementDataFromFile.Count > 0)
         {
            for (int i = 0; i < achievementDataFromFile.Count; i++)
            {
               achievementDataAsStringList.Add(achievementDataFromFile.AchievementKeyNames[i]);
            }
         }

         // Now, is our new achievement really new or have we seen it before
         if (!achievementDataAsStringList.Contains(achievementNameNew))
         {
            achievementDataAsStringList.Add(achievementNameNew);
            isThereANewAchievement = true;
         }

         // if our achievement is new, save file
         if (isThereANewAchievement)
         {
            // Convert the list into our serialisable struct
            AchievementData achievementData = new AchievementData(achievementDataAsStringList);

            // Save
            Logging.WriteLine("LOG: AchievementManager is awarding locally to save file: {0}", achievementNameNew);
            try
            {
               _screenManager.StorageManager.Save(achievementData);
            }
            catch (Exception e)
            {
               Logging.WriteLine("ERROR: AchievementManager failed to award locally, because could not save file: {0}", e.Message);
            }
         }
         else
         {
            Logging.WriteLine("LOG: AchievementManager did not award locally to save file. Achievement already there: {0}", achievementNameNew);
         }

         return isThereANewAchievement;
      }

      /// <summary>
      /// The summary text for the header of the Achievements screen, ie. "X of Y G, XX of YY Achievements earned."
      /// </summary>
      public string SummaryString
      {
         get
         {
            StringBuilder sb = new StringBuilder();
            int earnedPoints = 0, totalPoints = 0, earnedAch = 0, totalAch = 0;
            if (_achievementsGeneric != null)
            {
               foreach (var ag in _achievementsGeneric)
               {
                  if (ag.IsEarned)
                  {
                     earnedPoints += ag.GamerScore;
                     earnedAch++;
                  }
                  totalPoints += ag.GamerScore;
                  totalAch++;
               }
               sb.AppendFormat("{0} of {1} (G), {2} of {3} Achievements", earnedPoints, totalPoints, earnedAch, totalAch);
               return sb.ToString();
            }
            else
            {
               return "Error in AchievementManagerLocal.SummaryString";
            }
         }
      }

      private void PopulateAchievements()
      {
         // 01 - 05
         _achOneExplode = _contentManager.Load<Texture2D>(@"Textures\LiveIntegration\AchOneExplode");
         _achEinstein = _contentManager.Load<Texture2D>(@"Textures\LiveIntegration\AchEinstein");
         _achWipeOut8 = _contentManager.Load<Texture2D>(@"Textures\LiveIntegration\AchWipeOut8");
         _achSpin = _contentManager.Load<Texture2D>(@"Textures\LiveIntegration\AchSpin");
         _achOneDisint = _contentManager.Load<Texture2D>(@"Textures\LiveIntegration\AchOneDisint");
         // 06 - 10
         _achComplete50 = _contentManager.Load<Texture2D>(@"Textures\LiveIntegration\AchComplete50");
         _achMadDisint = _contentManager.Load<Texture2D>(@"Textures\LiveIntegration\AchMadDisint");
         _achWipeOut16 = _contentManager.Load<Texture2D>(@"Textures\LiveIntegration\AchWipeOut16");
         _achPenLove = _contentManager.Load<Texture2D>(@"Textures\LiveIntegration\AchPenLove");
         _achComplete100 = _contentManager.Load<Texture2D>(@"Textures\LiveIntegration\AchComplete100");
         // 11 - 15
         _achGold36 = _contentManager.Load<Texture2D>(@"Textures\LiveIntegration\AchGold36");
         _achGerbilSpeed = _contentManager.Load<Texture2D>(@"Textures\LiveIntegration\AchGerbilSpeed");
         _achGold72 = _contentManager.Load<Texture2D>(@"Textures\LiveIntegration\AchGold72");
         _achBombParty = _contentManager.Load<Texture2D>(@"Textures\LiveIntegration\AchBombParty");
         _achFirstPickup = _contentManager.Load<Texture2D>(@"Textures\LiveIntegration\AchFirstPickup");
         // 16 - 20
         _achGold01 = _contentManager.Load<Texture2D>(@"Textures\LiveIntegration\AchGold01");
         _achWipeOut01 = _contentManager.Load<Texture2D>(@"Textures\LiveIntegration\AchWipeout01");
         _achFirstAlarmGerbils = _contentManager.Load<Texture2D>(@"Textures\LiveIntegration\AchFirstAlarmGerbils");
         _achScore01 = _contentManager.Load<Texture2D>(@"Textures\LiveIntegration\AchScore01");
         _achScore02 = _contentManager.Load<Texture2D>(@"Textures\LiveIntegration\AchScore02");

         _achievementsGeneric.Clear();
         //-------------------------------------------------------------------------------- 01
         _achievementsGeneric.Add(new AchievementGeneric()
         {
            Key = AchievementNames.AchievementKeyOneHitWonderExploder,
            Name = "One Hit Wonder Exploder",
            Description = "Completed the site 25. Spooky using only one Exploder",
            HowToEarn = "Complete the site 25. Spooky using only one Exploder",            
            GamerScore = 5,
            Image = _achOneExplode,
            DisplayBeforeEarned = true,
            IsEarned = false,
         });
         //-------------------------------------------------------------------------------- 02
         _achievementsGeneric.Add(new AchievementGeneric()
         {
            Key = AchievementNames.AchievementKeyEinstein,
            Name = "Einstein was a Physicist",
            Description = "Collected all pickup items with a single gerbil and a single bomb on the site 10. Sink.",
            HowToEarn = "Collect all pickup items with a single gerbil and a single bomb on the site 10. Sink.",
            GamerScore = 10,
            Image = _achEinstein,
            DisplayBeforeEarned = true,
            IsEarned = false,
         });
         //-------------------------------------------------------------------------------- 03
         _achievementsGeneric.Add(new AchievementGeneric()
         {
            Key = AchievementNames.AchievementKeyWipeoutProgress01,
            Name = "8 Wipeouts",
            Description = "Collected 8 Wipeouts by being fast or frugal with weapon usage.",
            HowToEarn = "Collect 8 Wipeouts by being fast or frugal with weapon usage.",
            GamerScore = 5,
            Image = _achWipeOut8,
            DisplayBeforeEarned = true,
            IsEarned = false,
         });
         //-------------------------------------------------------------------------------- 04
         _achievementsGeneric.Add(new AchievementGeneric()
         {
            Key = AchievementNames.AchievementKeySpinCycle,
            Name = "Spin Cycle",
            Description = "Made a gerbil rotate 12 times on site 35. Newton's Gerbil.",
            HowToEarn = "Make a gerbil rotate 12 times on site 35. Newton's Gerbil.",
            GamerScore = 5,
            Image = _achSpin,
            DisplayBeforeEarned = true,
            IsEarned = false,
         });
         //-------------------------------------------------------------------------------- 05
         _achievementsGeneric.Add(new AchievementGeneric()
         {
            Key = AchievementNames.AchievementKeyOneHitWonderDisintegrator,
            Name = "One Hit Wonder Disintegrator",
            Description = "Completed the site 9. Be Decisive using only one Disintegrator",
            HowToEarn = "Complete the site 9. Be Decisive using only one Disintegrator",
            GamerScore = 10,
            Image = _achOneDisint,
            DisplayBeforeEarned = true,
            IsEarned = false,
         });
         //-------------------------------------------------------------------------------- 06
         _achievementsGeneric.Add(new AchievementGeneric()
         {
            Key = AchievementNames.AchievementKeyGameProgress01,
            Name = "36 Sites Demolished",
            Description = "Demolished 36 Gerbil Sites.",
            HowToEarn = "Demolish 36 Gerbil Sites.",
            GamerScore = 15,
            Image = _achComplete50,
            DisplayBeforeEarned = true,
            IsEarned = false,
         });
         //-------------------------------------------------------------------------------- 07
         _achievementsGeneric.Add(new AchievementGeneric()
         {
            Key = AchievementNames.AchievementKeyDisintegrationMad,
            Name = "Distintegration Mad",
            Description = "Disintegrated a Red Alarm Gerbil.",
            HowToEarn = "Disintegrate something you think you shouldn't touch!",            
            GamerScore = 5,
            Image = _achMadDisint,
            DisplayBeforeEarned = true,
            IsEarned = false,
         });
         //-------------------------------------------------------------------------------- 08
         _achievementsGeneric.Add(new AchievementGeneric()
         {
            Key = AchievementNames.AchievementKeyWipeoutProgress02,
            Name = "16 Wipeouts",
            Description = "Collected 16 Wipeouts by being fast or frugal with weapon usage.",
            HowToEarn = "Collect 16 Wipeouts by being fast or frugal with weapon usage.",
            GamerScore = 10,
            Image = _achWipeOut16,
            DisplayBeforeEarned = true,
            IsEarned = false,
         });
         //-------------------------------------------------------------------------------- 09
         _achievementsGeneric.Add(new AchievementGeneric()
         {
            Key = AchievementNames.AchievementKeyPenguinLover,
            Name = "Penguin Lover",
            Description = "Avoided detonating any penguins on level 72. Bad Neighbors",
            HowToEarn = "Avoid detonating any penguins on level 72. Bad Neighbors",
            GamerScore = 5,
            Image = _achPenLove,
            DisplayBeforeEarned = true,
            IsEarned = false,
         });
         //-------------------------------------------------------------------------------- 10
         _achievementsGeneric.Add(new AchievementGeneric()
         {
            Key = AchievementNames.AchievementKeyGameProgress02,
            Name = "72 Sites Demolished",
            Description = "Demolished 72 Gerbil Sites.",
            HowToEarn = "Demolish 72 Gerbil Sites.",
            GamerScore = 20,
            Image = _achComplete100,
            DisplayBeforeEarned = true,
            IsEarned = false,
         });
         //-------------------------------------------------------------------------------- 11
         _achievementsGeneric.Add(new AchievementGeneric()
         {
            Key = AchievementNames.AchievementKeyGoldProgress01,
            Name = "36 Gold Chests",
            Description = "Got 36 Gold Chests.",
            HowToEarn = "Get 36 Gold Chests.",
            GamerScore = 10,
            Image = _achGold36,
            DisplayBeforeEarned = true,
            IsEarned = false,
         });
         //-------------------------------------------------------------------------------- 12
         _achievementsGeneric.Add(new AchievementGeneric()
         {
            Key = AchievementNames.AchievementKeySpeedFreak01,
            Name = "Speed Freak (Mika Hakkin-Gerbil)",
            Description = "Completed site 41. Two Seasons in 32 seconds or less.",
            HowToEarn = "Complete site 41. Two Seasons in 32 seconds or less.",
            GamerScore = 5,
            Image = _achGerbilSpeed,
            DisplayBeforeEarned = true,
            IsEarned = false,
         });
         //-------------------------------------------------------------------------------- 13
         _achievementsGeneric.Add(new AchievementGeneric()
         {
            Key = AchievementNames.AchievementKeyGoldProgress02,
            Name = "72 Gold Chests",
            Description = "Got 72 Gold Chests.",
            HowToEarn = "Get 72 Gold Chests.",
            GamerScore = 15,
            Image = _achGold72,
            DisplayBeforeEarned = true,
            IsEarned = false,
         });
         //-------------------------------------------------------------------------------- 14
         _achievementsGeneric.Add(new AchievementGeneric()
         {
            Key = AchievementNames.AchievementKeyBombParty,
            Name = "Bomb Party",
            Description = "Used at least 10 bombs on any site.",
            HowToEarn = "Use at least 10 bombs on any site.",
            GamerScore = 10,
            Image = _achBombParty,
            DisplayBeforeEarned = true,
            IsEarned = false,
         });
         //-------------------------------------------------------------------------------- 15
         _achievementsGeneric.Add(new AchievementGeneric()
         {
            Key = AchievementNames.AchievementKeyFirstPickup,
            Name = "First Pickup",
            Description = "Got any pickup on any site.",
            HowToEarn = "Get any pickup on any site.",
            GamerScore = 5,
            Image = _achFirstPickup,
            DisplayBeforeEarned = true,
            IsEarned = false,
         });
         //-------------------------------------------------------------------------------- 16
         _achievementsGeneric.Add(new AchievementGeneric()
         {
            Key = AchievementNames.AchievementKeyGoldProgress00,
            Name = "First Gold Chest",
            //Name = "Collected all pickup items with a single gerbil and a single bomb on the site 10. Sink.",
            Description = "Got one Gold Chest by scoring highly on any site.",            
            HowToEarn = "Get one Gold Chest by scoring highly on any site.",
            GamerScore = 10,
            Image = _achGold01,
            DisplayBeforeEarned = true,
            IsEarned = false,
         });
         //-------------------------------------------------------------------------------- 17
         _achievementsGeneric.Add(new AchievementGeneric()
         {
            Key = AchievementNames.AchievementKeyWipeoutProgress00,
            Name = "First Wipeout",
            Description = "Collected one Wipeout by being fast or frugal with weapon usage.",
            HowToEarn = "Collect one Wipeout by being fast or frugal with weapon usage.",
            GamerScore = 10,
            Image = _achWipeOut01,
            DisplayBeforeEarned = true,
            IsEarned = false,
         });
         //-------------------------------------------------------------------------------- 18
         _achievementsGeneric.Add(new AchievementGeneric()
         {
            Key = AchievementNames.AchievementKeyFirstAlarmGerbils,
            Name = "First Alarm Gerbils",
            Description = "Avoided the Red Alarm Gerbils on site 4. Collateral.",
            HowToEarn = "Avoid the Red Alarm Gerbils on site 4. Collateral.",
            GamerScore = 10,
            Image = _achFirstAlarmGerbils,
            DisplayBeforeEarned = true,
            IsEarned = false,
         });
         //-------------------------------------------------------------------------------- 19
         _achievementsGeneric.Add(new AchievementGeneric()
         {
            Key = AchievementNames.AchievementKeyScoreProgress00,
            Name = "Score 20,000",
            Description = "Scored 20,000 points.",
            HowToEarn = "Score 20,000 points.",
            GamerScore = 15,
            Image = _achScore01,
            DisplayBeforeEarned = true,
            IsEarned = false,
         });
         //-------------------------------------------------------------------------------- 20
         _achievementsGeneric.Add(new AchievementGeneric()
         {
            Key = AchievementNames.AchievementKeyScoreProgress01,
            Name = "Score 40,000",
            Description = "Scored 40,000 points.",
            HowToEarn = "Score 40,000 points.",
            GamerScore = 20,
            Image = _achScore02,
            DisplayBeforeEarned = true,
            IsEarned = false,
         });
      }
   }
}