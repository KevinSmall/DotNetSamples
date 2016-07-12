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
   /// Achievement manager for Xbox LIVE servers
   /// Maintains two lists of achievements:
   ///   _achievementsGeneric = publically visible list of platform-independent achievements
   ///   _achievementsLive = private to this class, collection of platform-specific Live achievements
   /// </summary>
   /// <remarks>
   /// Note on locking using _lockObjectAchievments
   ///   CallbackGetAchievements uses lock object whilst *updating* _achievementsLive and _achievementsGeneric.
   ///   AwardAchievementLive uses lock object whilst *processing* _achievementsLive.
   ///   CallbackAwardAchievement doesnt need to use the lock object as it neither updates nor processes these lists.
   /// </remarks>
   public class AchievementManagerXBL : LiveManager, IAchievementManager
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

      /// <summary>
      /// Platform-specific list of achievements, copied to _achievementsGeneric for external use
      /// </summary>
      private AchievementCollection _achievementsLive;

      /// <summary>
      /// When playing in full mode, we may have achievements written locally that we need to award. The data from that
      /// file is loaded to here.
      /// </summary>
      AchievementData _achievementDataFromFile = new AchievementData();

      /// <summary>
      /// Used in asynch calls
      /// </summary>
      private object _lockObjectAchievements = new object();

      // These are probably just temporary while we dont have access to gamerservices
      private Texture2D _placeholderImage1, _placeholderImage2;
      private float _timerForAchievementsDummyLoad;
      private bool _timerForAchievementsDummyLoadHasExpired;

      /// <summary>
      /// Timer so we dont start awarding achievements from a local file too early, ie whilst screens are still fading in
      /// it looks ugly to have popups on top.
      /// </summary>
      private float _timerForAchievementsLocalFileCheck;
      private bool _timerForAchievementsLocalFileCheckHasExpired;
      private bool _hasLocalAchievementCheckBeenDone;
      private bool _suppressLog;

      /// <summary>
      /// Used for singular method call check.  We only allow one OnSignedIn to execute at a time because we listen to OnSignedIn (without Live) 
      /// *and* OnSignedInToLive, but dont want two asynch calls out getting achievements list running concurrently.
      /// </summary>
      private bool _onSignedInMethodIsExecuting;

      public AchievementManagerXBL(ScreenManager screenManager)
         : base(screenManager)
      {
         Logging.WriteLine("LOG: Using AchievementManagerXBL for Xbox LIVE");
      }

      public override void Initialize()
      {
         base.Initialize();
         _achievementsGeneric = new List<AchievementGeneric>();

      }

      public override void Dispose()
      {
         base.Dispose();
         GamerManager.OnSignedInWithoutLive -= OnSignedIn;
         GamerManager.OnSignedIn -= OnSignedIn;
      }

      /// <summary>
      /// The achievements OnSignedIn listener has a small complication.
      /// Achievements can be queried and awarded and use cached local data when not able to reach LIVE, ie even if LIVE switched off.
      /// Therefore we can be called if EITHER gamerManager.OnSignedIn or OnSignedInToLive are called.  The thing is, sometimes we can get *both*
      /// these being called one after the other, and sometimes we can get *only* the OnSignedInToLive, and sometimes *only* the OnSignedIn.
      /// </summary>      
      private void OnSignedIn(object sender, EventArgs args)
      {
         // Permissions check
         if (_gamerManager.LivePermissions == LivePermissions.NotPermitted)
         {
            Logging.WriteLine("LOG: AchievementManager.OnSignedIn raised but permissions are NotPermitted");
            return;
         }

         // Sign In check
         if (!_gamerManager.CheckSignInStatusForAchievements("AchievementManager.OnSignedIn", false))
            return;
         
         // Already Done check
         if (_achievementsGeneric.Count > 0)
         {
            Logging.WriteLine("LOG: AchievementManager.OnSignedIn raised but _achivementsGeneric already filled.  Suppressing this OnSignedIn call.");
            return;
         }

         // Singular method call check
         // We only allow one OnSignedIn to execute at a time because we listen to OnSignedIn (without Live) *and* OnSignedInToLive, but dont want
         // two asynch calls out running concurrently.
         if (_onSignedInMethodIsExecuting)
         {
            Logging.WriteLine("LOG: WARNING AchievementManager.OnSignedIn has made asynch call to get achievements and not heard back. Suppressing this OnSignedIn call.");
            return;
         }         
         
         // Get achievement list
         Logging.WriteLine("LOG: ASYNCH OUT-> AchievementManager is beginning asynch call to get Achievements");
         _onSignedInMethodIsExecuting = true;
         TrackerObjectLive trackerObject = new TrackerObjectLive(_gamerManager.SignedInGamer, null, null);

         try
         {
            _gamerManager.SignedInGamer.BeginGetAchievements(CallbackGetAchievements, trackerObject);
         }
         catch (Exception e)
         {
            Logging.WriteLine("ERROR: AchievementManager.OnSignedIn, BeginGetAchievements failed, exception: {0}" + e.Message);
            _onSignedInMethodIsExecuting = false;
            // No other action needed since this is just refreshing list
         }
      }

      /// <summary>
      /// Asynchronously retrieve the achievement collection
      /// </summary>
      protected void CallbackGetAchievements(IAsyncResult result)
      {
         Logging.WriteLine("LOG: ASYNCH IN<- AchievementManager is in its callback to process Achievements received");

         TrackerObjectLive trackerObject = result.AsyncState as TrackerObjectLive;
         SignedInGamer gamer = trackerObject.Gamer as SignedInGamer;         

         if (gamer == null)
         {
            Logging.WriteLine("ERROR: in CallbackGetAchievements SignedInGamer was null");
            _onSignedInMethodIsExecuting = false;
            return;
         }

         // the achievement list is being modified, don't compete with the UI thread
         lock (_lockObjectAchievements)
         {
            _achievementsGeneric.Clear();
            try
            {
               _achievementsLive = gamer.EndGetAchievements(result);
            }
            catch (Exception e)
            {
               Logging.WriteLine("ERROR: in CallbackGetAchievements, EndGetAchievements failed exception: {0}", e.Message);
               _onSignedInMethodIsExecuting = false;
               // No other action needed, just the list refresh failed
            }

            if (_achievementsLive != null)
            {
               // Copy list out to our generic, publicly exposed, list
               for (int i = 0; i < _achievementsLive.Count; i++)
               {
                  Achievement achievementLive = _achievementsLive[i];
                  _achievementsGeneric.Add(new AchievementGeneric(achievementLive));
               }
               // Our list has changed
               OnAchievementDataChanged(this, EventArgs.Empty);
            }
            else
            {
               // This can happen if e.g. the SPA file achievement order has changed
               Logging.WriteLine("WARNING: in CallbackGetAchievements, EndGetAchievements _achievementsLive was null");
            }

            _onSignedInMethodIsExecuting = false;
         }
      }

      public override void LoadContent()
      {
         base.LoadContent();

         //---------------------------------------------------------------------------------------
         // Prepare to read achievements list
         //---------------------------------------------------------------------------------------
#if NO_LIVE
         // Just pretend we are making an asynch call by setting this timer, when it expires fill _achievementsGeneric directly 
         _timerForAchievementsDummyLoad = 4; // seconds
         _timerForAchievementsDummyLoadHasExpired = false;
#endif
         // We wait until gamer has signed in (both with, and without LIVE token) before reading the achievements
         // Achivements only needs the "without LIVE" but sometimes that is never called, if the player signs in immediately
         GamerManager.OnSignedInWithoutLive += OnSignedIn;
         GamerManager.OnSignedIn += OnSignedIn;

         // For check for local achievements file
         _timerForAchievementsLocalFileCheck = 15;
         _timerForAchievementsLocalFileCheckHasExpired = false;
      }

      public override void LoadContentDeferred()
      {
         base.LoadContentDeferred();
         _contentLoaded = true;
      }

      public override void Update(float elapsedTime)
      {
         base.Update(elapsedTime);

#if NO_LIVE
         // Pretend asynch call to initially populate the achievements list, to simulate us getting list from gamerservices.
         if (!_timerForAchievementsDummyLoadHasExpired)
         {
            _timerForAchievementsDummyLoad -= elapsedTime;
            if (_timerForAchievementsDummyLoad < 0)
            {
               DeleteMe_FillAchievementsDirectly();
               _timerForAchievementsDummyLoadHasExpired = true;
            }
         }
#endif

         //---------------------------------------------------------------------------------------
         // Check to see if we have a save file and have bought the game, then award appropriate achievements
         //---------------------------------------------------------------------------------------
         CheckAndAwardLocalAchievements(elapsedTime);

         //---------------------------------------------------------------------------------------
         // Check to see if we've lost our permissions and wipe achievements data accordingly
         //---------------------------------------------------------------------------------------
         if (_gamerManager.LivePermissions == LivePermissions.NotPermitted)
         {
            // Player had refused a TU, so we are not allowed to access live any more and shouldnt display anything
            _achievementsGeneric.Clear();
         }
      }

      private void CheckAndAwardLocalAchievements(float elapsedTime)
      {
         // We must have not checked already
         if (_hasLocalAchievementCheckBeenDone)
            return;

         // We must have bought it
         if (_gamerManager.IsTrialMode)
            return;

         // We must have read-write permissions
         if (_gamerManager.LivePermissions != LivePermissions.Permitted_ReadWrite)
            return;

#if !NO_LIVE
         // We must be signed in, even just locally is ok 
         if (!_gamerManager.CheckSignInStatusForAchievements("AchievementManager.CheckAndAwardLocalAchievements", _suppressLog))
         {
            // if we fail once, that's enough, we can suppress the log since this method gets called a lot
            _suppressLog = true;
            return;
         }
#endif

         // We must have waited a bit so achievement popups dont appear during startup
         if (!_timerForAchievementsLocalFileCheckHasExpired)
         {
            _timerForAchievementsLocalFileCheck -= elapsedTime;
            if (_timerForAchievementsLocalFileCheck < 0)
            {
               _timerForAchievementsLocalFileCheckHasExpired = true;
            }
         }

         if (!_timerForAchievementsLocalFileCheckHasExpired)
            return;

         // If we reach here, we are good to go, but we only try once
         _hasLocalAchievementCheckBeenDone = true;

         // Check for existence of a local achievements file
         if (_screenManager.StorageManager.FileExists(_achievementDataFromFile.GetType()))
         {
            AwardAchievementsFromSaveDataFile();
         }
      }

      /// <summary>
      /// Read local achievements, award them, and delete the file.
      /// </summary>
      private void AwardAchievementsFromSaveDataFile()
      {
         StorageManager sm = _screenManager.StorageManager;

         // Load the local achievements
         try
         {
            _achievementDataFromFile = (AchievementData)sm.Load(_achievementDataFromFile.GetType());
         }
         catch (Exception e)
         {
            Logging.WriteLine("ERROR: AwardAchievementsFromSaveDataFile failed, could not read file: {0}", e.Message);
            
            // File is corrupt, trash it and bail
            DeleteAchievementsSaveDataFile();
            return;
         }
         finally
         {
         }

         // Award the achievements
         if (_achievementDataFromFile.Count > 0)
         {
            for (int i = 0; i < _achievementDataFromFile.Count; i++)
            {
               AwardAchievement(_achievementDataFromFile.AchievementKeyNames[i]);
            }
         }
         else
         {
            Logging.WriteLine("ERROR: AwardAchievementsFromSaveDataFile failed, file loaded but empty");
            return;
         }

         // Delete the local file now we are finished with it
         DeleteAchievementsSaveDataFile();
      }

      /// <summary>
      /// Delete the locally saved achievements file
      /// Used internally plus by the options menu cheats to delete all local data
      /// </summary>
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
      }

      /// <summary>
      /// String name is the achievement key and it must match list of static achievement names in the Achievement
      /// Manager class and must also match the names in the SPA file.
      /// </summary>
      public void AwardAchievement(string achievementName)
      {
         Logging.WriteLine("LOG: AchievementManager received request to award {0}", achievementName);

         if (achievementName == null || achievementName == "")
         {
            return;
         }

         // Permissions check
         switch (_gamerManager.LivePermissions)
         {
            case LivePermissions.NotPermitted:
               return;

            case LivePermissions.Permitted_ReadOnly:
               // Award the achievement to our local offline file, it'll get awarded proper if they buy
               bool isThereANewAchievement = AwardAchievementToSaveDataFile(achievementName);
               if (isThereANewAchievement)
               {
                  // Display the upsell popup message box
                  DisplayUpsellAchievementMessageBox(achievementName);
               }
               break;

            case LivePermissions.Permitted_ReadWrite:
               AwardAchievementLive(achievementName);
               break;

            default:
               throw new Exception("LivePermissions status not known");
         }
      }

      /// <summary>
      /// Achievements can be stored locally and then awarded if the player buys the game. This appends to any existing
      /// achievements and ensures saved list is a unique list.
      /// This method is also used if LIVE achievement awarding gives exceptions, we always store here and will try to recover
      /// next time we start up.
      /// </summary>
      /// <param name="achievementNameNew">Achievement key name (valid values are the static strings in the Achievement Manager
      /// and must match the key names in the SPA file)</param>
      /// <returns>True if this was a new achievement not already in the file</returns>   
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
      /// Display MessageBox for encouraging an upsell after an achievement was found. The messagebox is not shown if there are other
      /// upsell screens already there, as can happen when multiple achievements are awarded at once and game not bought.
      /// </summary>
      /// <param name="achievementNameNew">Achievement key name (valid values are the static strings in the Achievement Manager
      /// and must match the key names in the SPA file)</param>
      private void DisplayUpsellAchievementMessageBox(string achievementNameNew)
      {
         // If there are no upsell screens already being displayed, tell user that they have been awarded an achievement and should buy the game
         if (!_screenManager.IsThereAnyOtherUpsellScreensBeingDisplayed())
         {
            string achName = GetAchievementFriendlyName(achievementNameNew);
            if (achName != null)
            {
               string str = string.Format(LiveIntegrationResource.MessageBox_UpsellAchievement, achName);   
               MessageBoxUpsellScreen gotLocalAchievement = new MessageBoxUpsellScreen(
                  LiveIntegrationResource.MessageBox_UpsellAchievementTitle, str, MessageBoxButtons.YesNo);
               gotLocalAchievement.Accepted += GotLocalAchievementMessageBoxAccepted;
               _screenManager.AddScreen(gotLocalAchievement, null);
            }
         }
      }
      
      /// <summary>
      /// Event handler for when the user selects Yes on the "do you want to buy game" message box.
      /// </summary>
      void GotLocalAchievementMessageBoxAccepted(object sender, PlayerIndexEventArgs e)
      {
         _gamerManager.BuyGame();
      }

      private void AwardAchievementLive(string achievementKey)
      {
#if NO_LIVE
         //// If LIVE not enabled, say so
         //string title = "~1== Config set to NO LIVE ==~";
         //string str = "Acquired achievement but" +
         //             "it won't really be awarded:" +
         //             "~1 " + GetAchievementFriendlyName(achievementKey) + "~";
         //MessageBoxScreen gotAchievement = new MessageBoxScreen(title, str, MessageBoxButtons.OK, 580);
         //_screenManager.AddScreen(gotAchievement, null);

         // Here is local ach awarding

#else
         // Sign in check
         // Achievements can be queried and awarded and use cached local data when not able to reach LIVE
         if (!_gamerManager.CheckSignInStatusForAchievements("AchievementManager.AwardAchievementLive", false))
            return;

         // Only award it, if its new
         // We're looping through _achievementsLive here, so it musnt get changed whilst we're looking at it
         lock (_lockObjectAchievements)
         {
            if (_achievementsLive != null)
            {
               foreach (Achievement a in _achievementsLive)
               {
                  if (a.Key == achievementKey)
                  {
                     if (!a.IsEarned)
                     {
                        Logging.WriteLine("LOG: ASYNCH OUT-> AchievementManager is awarding achievement {0}", achievementKey);
                        TrackerObjectLive trackerObject = new TrackerObjectLive(_gamerManager.SignedInGamer, null, achievementKey);
                        try
                        {
                           _gamerManager.SignedInGamer.BeginAwardAchievement(achievementKey, CallbackAwardAchievement, trackerObject);
                        }
                        catch (Exception e)
                        {
                           Logging.WriteLine("ERROR: AchievementManager.AwardAchievementLive, BeginAwardAchievement failed, exception: " + e.Message);
                           // Store achievement for awarding next time we startup
                           AwardAchievementLiveAfterException(a.Key);
                        }
                     }

                     // we found the desired achievement, so early out
                     return;
                  }
               }
            }
         }
#endif
      }

      private void AwardAchievementLiveAfterException(string achievementKey)
      {
         Logging.WriteLine("ERROR: An exception occured above, storing achievement locally: " + achievementKey);
         AwardAchievementToSaveDataFile(achievementKey);
      }

      /// <summary>
      /// Asynchronously finish awarding an achievement
      /// </summary>
      protected void CallbackAwardAchievement(IAsyncResult result)
      {
         TrackerObjectLive trackerObject = result.AsyncState as TrackerObjectLive;
         Logging.WriteLine("LOG: ASYNCH IN<- AchievementManager is in its callback finishing awarding achievement {0}", trackerObject.AchievementKey);
         SignedInGamer gamer = trackerObject.Gamer as SignedInGamer;

         if (gamer == null)
         {
            Logging.WriteLine("ERROR: in CallbackAwardAchievement SignedInGamer was null");
            return;
         }

         try
         {
            gamer.EndAwardAchievement(result);

         }
         catch (Exception e)
         {
            Logging.WriteLine("ERROR: AchievementManager.CallbackAwardAchievement, EndAwardAchievement failed, exception: " + e.Message);
            // Store achievement for awarding next time we startup
            AwardAchievementLiveAfterException(trackerObject.AchievementKey);
         }

         // Refresh the achievement view
         TrackerObjectLive trackerObjectRefresh = new TrackerObjectLive(gamer, null, null);
         Logging.WriteLine("LOG: ASYNCH OUT-> AchievementManager is refreshing list of Achievements after an award");
         try
         {
            gamer.BeginGetAchievements(CallbackGetAchievements, trackerObjectRefresh);
         }
         catch (Exception e)
         {
            Logging.WriteLine("ERROR: AchievementManager.CallbackAwardAchievement, BeginGetAchievements failed, exception: {0}" + e.Message);
            // No other action needed since this is just refreshing list
         }
      }

      /// <summary>
      /// This is our dummy achievements when Live services are not available (ie during testing on the emulator)
      /// </summary>
      private void DeleteMe_FillAchievementsDirectly()
      {
         // Images will also come from Live, but for now we can use these placeholders
         _placeholderImage1 = _contentManager.Load<Texture2D>(@"Textures\LiveIntegration\achievement_Hurdle");
         _placeholderImage2 = _contentManager.Load<Texture2D>(@"Textures\LiveIntegration\achievement_OneHitWonder");

         _achievementsGeneric.Add(new AchievementGeneric()
         {
            Key = AchievementNames.AchievementKeyDisintegrationMad,
            Name = "Disintegration Mad",
            Description = "Zapped a red gerbil.",
            HowToEarn = "Disintegrate something you shouldn't",
            GamerScore = 10,
            Image = _placeholderImage2,
            DisplayBeforeEarned = true,
            IsEarned = false,
         });

         _achievementsGeneric.Add(new AchievementGeneric()
         {
            Key = AchievementNames.AchievementKeyWipeoutProgress01,
            Name = "8 Wipeouts",
            Description = "Collect 8 Wipeouts by being fast or frugal with weapon usage.",
            HowToEarn = "Scored 8 Wipeouts.",
            GamerScore = 5,
            Image = _placeholderImage1,
            DisplayBeforeEarned = true,
            IsEarned = true,
         });

         _achievementsGeneric.Add(new AchievementGeneric()
         {
            Key = AchievementNames.AchievementKeyWipeoutProgress02,
            Name = "16 Wipeouts",
            Description = "Collect 16 Wipeouts by being fast or frugal with weapon usage.",
            HowToEarn = "Scored 32 Wipeouts.",
            GamerScore = 10,
            Image = _placeholderImage2,
            DisplayBeforeEarned = true,
            IsEarned = false,
         });

         _achievementsGeneric.Add(new AchievementGeneric()
         {
            Key = AchievementNames.AchievementKeyGameProgress01,
            Name = "36 Sites Demolished",
            Description = "Demolish 36 Gerbil Sites.",
            HowToEarn = "Demolished 36 Gerbil Sites.",
            GamerScore = 20,
            Image = _placeholderImage1,
            DisplayBeforeEarned = true,
            IsEarned = true,
         });

         _achievementsGeneric.Add(new AchievementGeneric()
         {
            Key = AchievementNames.AchievementKeyGameProgress02,
            Name = "72 Sites Demolished",
            Description = "Demolish 72 Gerbil Sites.",
            HowToEarn = "Demolished 72 Gerbil Sites.",
            GamerScore = 20,
            Image = _placeholderImage2,
            DisplayBeforeEarned = true,
            IsEarned = false,
         });

         //_achievementsGeneric.Add(new AchievementGeneric()
         //{
         //   Key = AchievementKeyDontFearTheGerbil,
         //   Name = "No Fear",
         //   Description = "Remove some Red Alarm Gerbils and still complete site.  But how?",
         //   HowToEarn = "Removed some Red Alarm Gerbils and completed site.",
         //   GamerScore = 20,
         //   Image = _placeholderImage2,
         //   DisplayBeforeEarned = true,
         //   IsEarned = false,
         //});

         //_achievementsGeneric.Add(new AchievementGeneric()
         //{
         //   Key = AchievementKeyAirborneHighSpeed,
         //   Name = "Rocket",
         //   Description = "Get a Gerbil going at huge speeds.",
         //   HowToEarn = "Got a Gerbil going at huge speeds.",
         //   GamerScore = 10,
         //   Image = _placeholderImage1,
         //   DisplayBeforeEarned = true,
         //   IsEarned = false,
         //});

         _achievementsGeneric.Add(new AchievementGeneric()
         {
            Key = AchievementNames.AchievementKeyOneHitWonderExploder,
            Name = "One Hit Wonder Exploder",
            Description = "Complete the site 'Spooky' using only one Exploder",
            HowToEarn = "Completed the site 'Spooky' using only one Exploder",
            GamerScore = 10,
            Image = _placeholderImage1,
            DisplayBeforeEarned = true,
            IsEarned = false,
         });

         _achievementsGeneric.Add(new AchievementGeneric()
         {
            Key = AchievementNames.AchievementKeyOneHitWonderDisintegrator,
            Name = "One Hit Wonder Disintegrator",
            Description = "Complete the site 'Be Decisive' using only one Disintegrator",
            HowToEarn = "Completed the site 'Be Decisive' using only one Disintegrator",
            GamerScore = 5,
            Image = _placeholderImage2,
            DisplayBeforeEarned = true,
            IsEarned = false,
         });

         _achievementsGeneric.Add(new AchievementGeneric()
         {
            Key = AchievementNames.AchievementKeyPenguinLover,
            Name = "Penguin Lover",
            Description = "Avoid detonating any penguins on level 'Bad Neighbors'",
            HowToEarn = "Avoided detonating any penguins on level 'Bad Neighbors'",
            GamerScore = 15,
            Image = _placeholderImage1,
            DisplayBeforeEarned = true,
            IsEarned = false,
         });

         _achievementsGeneric.Add(new AchievementGeneric()
         {
            Key = AchievementNames.AchievementKeyGoldProgress01,
            Name = "36 Gold Chests",
            Description = "Get 36 Gold Chests",
            HowToEarn = "Got 36 Gold Chests",
            GamerScore = 15,
            Image = _placeholderImage2,
            DisplayBeforeEarned = true,
            IsEarned = false,
         });

         _achievementsGeneric.Add(new AchievementGeneric()
         {
            Key = AchievementNames.AchievementKeyGoldProgress02,
            Name = "72 Gold Chests",
            Description = "Get 72 Gold Chests",
            HowToEarn = "Got 72 Gold Chests",
            GamerScore = 30,
            Image = _placeholderImage1,
            DisplayBeforeEarned = true,
            IsEarned = false,
         });

         _achievementsGeneric.Add(new AchievementGeneric()
         {
            Key = AchievementNames.AchievementKeySpeedFreak01,
            Name = "Speed Freak 1",
            Description = "Complete site",
            HowToEarn = "Completed site",
            GamerScore = 10,
            Image = _placeholderImage2,
            DisplayBeforeEarned = true,
            IsEarned = false,
         });

         //_achievementsGeneric.Add(new AchievementGeneric()
         //{
         //   Key = AchievementKeySpeedFreak02,
         //   Name = "Speed Freak 2",
         //   Description = "Complete site",
         //   HowToEarn = "Completed site",
         //   GamerScore = 10,
         //   Image = _placeholderImage1,
         //   DisplayBeforeEarned = true,
         //   IsEarned = false,
         //});

         _achievementsGeneric.Add(new AchievementGeneric()
         {
            Key = AchievementNames.AchievementKeyEinstein,
            Name = "Einstein was a Physicist",
            Description = "Collect all pickup items with a single bomb on the site 'Sink'.",
            HowToEarn = "Collected all pickup items with a single bomb on the site 'Sink'.",
            GamerScore = 10,
            Image = _placeholderImage2,
            DisplayBeforeEarned = true,
            IsEarned = false,
         });

         _achievementsGeneric.Add(new AchievementGeneric()
         {
            Key = AchievementNames.AchievementKeySpinCycle,
            Name = "Spin Cycle",
            Description = "Make a gerbil spin 12 times on level Newtons Gerbil.",
            HowToEarn = "Make a gerbil spin 12 times on level Newtons Gerbil.",
            GamerScore = 10,
            Image = _placeholderImage2,
            DisplayBeforeEarned = true,
            IsEarned = false,
         });
      }

      private string GetAchievementFriendlyName(string achievementKey)
      {
         foreach (var ag in _achievementsGeneric)
         {
            if (ag.Key == achievementKey)
            {
               return ag.Name;
            }
         }
         Logging.WriteLine("ERROR: AchievementManager.GetAchievementFriendlyName() could not find key: {0}", achievementKey);
         return "Key Not found";
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
      }
   }
}