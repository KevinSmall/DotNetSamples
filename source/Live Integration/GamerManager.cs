using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using PerformanceUtility.GameDebugTools;
using GP.Localisation.Live;
using GP.Storage;

namespace GP.Live_Integration
{
   /// <summary>
   /// Gamer management component containing information about Title Update (TU) status and IsTrialMode status
   /// This component handles all TU logic and purchasing logic, including all of the Guide calls and our own
   /// MessageBox calls and the command console on the phone.
   /// Works out what live permissions we currently have in this.LivePermissions.
   /// See also comments in abstract parent class of LeaderboardManager and AchievementManager, the LiveManager
   /// </summary>
   /// <remarks>
   /// When a TU is refused:
   ///   Achivements stay read only or read write - ie no change.
   ///   Leaderboards can be written to, but should not be displayed.  This suppression if their display is handlded in the
   ///   LeaderboardsMenuScreen, which checks GamerManager.TitleUpdateWasRefused flag.
   /// </remarks>
   public sealed class GamerManager : DrawableGameComponent, IDisposable, ILoadContentDeferrable
   {
#if DEBUG
      /// <summary>
      /// Mango doesnt support a nice simulated purchase process, this is for our equivalent.  
      /// This gets tombstoned in the ScreenManagerTombstoneState.
      /// </summary>
      public static bool SimulateTrialMode { get; set; }

      /// <summary>
      /// Only for cheats, allows testng game purchase when offline eg on emu with Debug No Live.
      /// </summary>
      public static bool AllowOfflinePurchase { get; set; }
#endif

      /// <summary>
      /// Event we raise after player successfully signs in to LIVE with a valid user token
      /// </summary>
      public static event EventHandler<EventArgs> OnSignedIn;

      /// <summary>
      /// Event we raise after player successfully signs in, but without signing in to LIVE with a valid user token
      /// </summary>
      public static event EventHandler<EventArgs> OnSignedInWithoutLive;

      /// <summary>
      /// Player name
      /// For platform-specific LIVE gamertags, they can be at most 15 characters long, and only contain the ASCII
      /// characters a-z, A-Z, 0-9, comma, and space. So the longest string is usually 15 'W's in a row.
      /// This value can never be saved, it is a TCR that it is never stored!
      /// </summary>
      public string GamerTag { get { return _gamerTag; } }
      private string _gamerTag;

      /// <summary>
      /// Count of game executions since install - saved in IS and updated when game starts
      /// Exposed as static for Flurry
      /// </summary>
      public static int GameExecutionCount { get { return _gameExecutionCount; } }
      private static int _gameExecutionCount = 0;
      ////-- Just for debugging
      //private static int _gameExecutionCount
      //{
      //   get { return _gameExecountBacker; }
      //   set
      //   {
      //      _gameExecountBacker = value;
      //   }
      //}
      //private static int _gameExecountBacker = 0;
      ////--

      /// <summary>
      /// Trial mode flag exposed as static for Flurry, this has the same value as _gameManager.IsTrialMode
      /// </summary>
      public static bool IsTrialModeStatic { get { return _isTrialModeStatic; } }
      private static bool _isTrialModeStatic = true;

      /// <summary>
      /// Count of how many times the upsell screen has appeared, exposed as static in case we want to record it on Flurry
      /// </summary>
      public static int UpsellDisplayCount { get { return _upsellDisplayCount; } }
      private static int _upsellDisplayCount;

      /// <summary>
      /// Count of how many times an achievement has been made in live (ie ignores offline awards made before purchase).
      /// This figure comes from the Xbox LIVE servers, it will get written to at least once by Achivemnent Mgr at game startup.
      /// </summary>
      public static int AchievementsGainedCount
      {
         get
         {
            return _achievementsGainedCount;
         }
         set
         {
            // Catch unique event of a new achivement happening
            if (_achievementsGainedCount < value)
            {
               FlurryEvents.AchievementsGainedCount("Achievements", "Number of Achievements", value);
            }
            _achievementsGainedCount = value;
         }
      }

      private static int _achievementsGainedCount = 0;  // default to 1 to allow testing//change to 0 as this is default for a trial game

      /// <summary>
      /// Trial mode flag.
      /// This is just a cached value taken from Guide.IsTrialMode (which can take 60ms to read, which is why we cache it).
      /// It defaults to "worst case" which is true
      /// </summary>
      public bool IsTrialMode { get { return _isTrialMode; } }
      private bool _isTrialMode = true;

      /// <summary>
      /// What are our permissions for accessing Live?  If we've refused a TU then we wont get full access, if we're in Trial
      /// Mode then we get read-only.  This does not tell us if Live services actually ARE available, it just says what level
      /// of access we should be allowed when we access them.
      /// Note this value can change and any time, due to TU being refused, or delayed knowledge of IsTrialMode being true.
      /// Defaults to ReadOnly.
      /// </summary>
      public LivePermissions LivePermissions { get { return _livePermissions; } }
      private LivePermissions _livePermissions = LivePermissions.Permitted_ReadOnly;

      /// <summary>
      /// Flag if player actually refused a TU
      /// </summary>
      public bool TitleUpdateWasRefused { get { return _titleUpdateWasRefused; } }
      private bool _titleUpdateWasRefused;

      /// <summary>
      /// Can be signed in locally or to live.  Use .IsSignedInToLive to tell which.  Achievements can work with
      /// IsSignedInToLive == false as they cache data locally, but Leaderboards cannot do this, they must be signed in.
      /// </summary>
      public SignedInGamer SignedInGamer { get { return _signedInGamer; } }
      private SignedInGamer _signedInGamer;

      private GamerManagerState _gamerManagerState;
      private ScreenManager _screenManager;
      private GamerServicesComponent _gamerServices;

      // UI options buttons
      private List<string> _dialogButtonsYesNo = new List<string>() { LiveIntegrationResource.Guide_ButtonYes, LiveIntegrationResource.Guide_ButtonNo };
      private List<string> _dialogButtonsOK = new List<string>() { LiveIntegrationResource.Guide_ButtonOK };

      /// <summary>
      /// Flag requesting we want a title update choice to be presented to the player
      /// </summary>
      private bool _displayTitleUpdateMessage;

      /// <summary>
      /// Flag requesting we want a title update refused information message to be presented to the player
      /// </summary>
      private bool _displayTitleUpdateRefusedInfoMessage;

      /// <summary>
      /// Flag requesting we want marketplace to be presented to the player
      /// </summary>
      private bool _displayShowMarketPlace;

      private bool _displayPhoneKeyboardForConsole;
      private bool _displayGuideSignInUI;

      private ContentManager _contentManager;

      // For displaying command console
      private IAsyncResult _keyboardResult;
      private string _keyboardTypedCharacters;

      // For debug overlay, positions in HUD space 1280x720
      private Texture2D _blankTexture;
      private Vector2 _transpPos;
      private Color _transpCol;
      private Vector2 _topLeftHUDSpace = new Vector2(910f, 580f);
      private float _lineIncrementHUDSpace = 30f;
      private SpriteBatch _spriteBatch;
      private SpriteFont _spriteFont;
      private bool _IsContentLoaded;

      /// <summary>
      /// For storing our own flags to detect new users and purchases
      /// </summary>
      private GamerManagerData _gamerManagerData;

      /// <summary>
      /// Flag if we've already loaded gamer manager data this session
      /// </summary>
      private bool _isGamerManagerDataAlreadyLoaded;

      public GamerManager(Game game)
         : base(game)
      {
         Logging.WriteLine("LOG: GamerManager Constructor");

         // Add ourselves as service
         game.Services.AddService(typeof(GamerManager), this);

         _contentManager = new ContentManager(game.Services);
         _contentManager.RootDirectory = Dashboard.RootDirectoryMain;
      }

      /// <summary>
      /// Allows the game component to perform any initialization it needs to before starting
      /// to run.  This is where it can query for any required services and load content.
      /// </summary>
      public override void Initialize()
      {
         base.Initialize();

         // Our local ref to screen manager
         _screenManager = (ScreenManager)GerbilPhysicsGame.GameServices.GetService(typeof(ScreenManager));

         // Our local ref to gamer Services, real or pretend
#if NO_LIVE
         // Just pretend we have access (eg not running on actual device)
         _gamerServices = new GamerServicesComponent(Game);
#else
         _gamerServices = (GamerServicesComponent)GerbilPhysicsGame.GameServices.GetService(typeof(GamerServicesComponent));
         // Get notification when the gamer signs in
         SignedInGamer.SignedIn += OnGamerSignedIn;
#endif
         _gamerManagerState = GamerManagerState.WaitingToSignIn;

         // Listen to achievements, we need to, to expose count of them for Flurry
         if (_screenManager.AchievementManager != null)
         {
            _screenManager.AchievementManager.OnAchievementDataChanged += OnAchievementsDataChanged;
         }
         else
         {
            throw new Exception("arse biscuits");
         }

         // Default trial mode to worst case then make our expensive call of 60ms
         _isTrialMode = true;
         UpdateIsTrialMode();

         // UI 
         _displayTitleUpdateMessage = false;
         _displayShowMarketPlace = false;
         _displayTitleUpdateRefusedInfoMessage = false;

         // Debug overlay
         float x = _topLeftHUDSpace.X;
         float y = _topLeftHUDSpace.Y;
         _transpPos = new Vector2(x - 5, y - 5).ToCurrentBackBuffer();
         _transpCol = new Color(0, 0, 0, 142);
      }

      /// <summary>
      /// For Flurry
      /// </summary>
      private void OnAchievementsDataChanged(object sender, EventArgs e)
      {
         int totalAch = 0;

         if (_screenManager == null || _screenManager.AchievementManager == null || _screenManager.AchievementManager.AchievementsGeneric == null)
         {
            // leave as zero
         }
         else
         {
            foreach (AchievementGeneric achieve in _screenManager.AchievementManager.AchievementsGeneric)
            {
               if (achieve.IsEarned)
                  totalAch++;
            }
         }
         AchievementsGainedCount = totalAch;
         //if (!GerbilPhysicsGame.IsRecoveringFromTombstoneOrFAS)
         //{
         //   Logging.WriteLineGrouped("Check values have been loaded correctly before flurry launch {0} {1} {2} {3} {4} ", Logging.FlurryGroup, _gamerManagerData.GameExecutionCount, _gamerManagerData.IsGamePurchased, _gamerManagerData.IsUserNew, _gamerManagerData.UpsellDisplayCount, AchievementsGainedCount);
         //   Flurry.Launching("zStart Session", _gamerManagerData);
         //   Logging.WriteLineGrouped("XXXX Flurry Launched XXXX", Logging.FlurryGroup);
         //}
         //else
         //{
         //   Logging.WriteLineGrouped("FAS Check values have been loaded correctly before flurry launch {0} {1} {2} {3} {4}  ", Logging.FlurryGroup, _gamerManagerData.GameExecutionCount, _gamerManagerData.IsGamePurchased, _gamerManagerData.IsUserNew, _gamerManagerData.UpsellDisplayCount, AchievementsGainedCount);
         //   Flurry.Launching("zStart Session FAS", _gamerManagerData);
         //   Logging.WriteLineGrouped("XXXX Flurry Launched XXXX", Logging.FlurryGroup);
         //}
      }

      protected override void LoadContent()
      {
         base.LoadContent();
         _blankTexture = _contentManager.Load<Texture2D>(@"Textures\System\blank");
      }

      public void LoadContentDeferred()
      {
         _spriteBatch = new SpriteBatch(GraphicsDevice);
         _spriteFont = _contentManager.Load<SpriteFont>(@"Fonts\monitorFont");

         // Flurry wants to know if we're a first time user
         UpdateFlurryForNewUser();

         // Increment our counter for game executions
         UpdateGameUsageCount();

         _IsContentLoaded = true;

         if (!GerbilPhysicsGame.IsRecoveringFromTombstoneOrFAS)
         {
            Logging.WriteLineGrouped("Check values have been loaded correctly before flurry launch {0} {1} {2} {3} {4} ", Logging.FlurryGroup, _gamerManagerData.GameExecutionCount, _gamerManagerData.IsGamePurchased, _gamerManagerData.IsUserNew, _gamerManagerData.UpsellDisplayCount, AchievementsGainedCount);
            Flurry.Launching("zStart Session", _gamerManagerData);
            Logging.WriteLineGrouped("XXXX Flurry Launched XXXX", Logging.FlurryGroup);
         }
         else
         {
            Logging.WriteLineGrouped("FAS Check values have been loaded correctly before flurry launch {0} {1} {2} {3} {4}  ", Logging.FlurryGroup, _gamerManagerData.GameExecutionCount, _gamerManagerData.IsGamePurchased, _gamerManagerData.IsUserNew, _gamerManagerData.UpsellDisplayCount, AchievementsGainedCount);
            Flurry.Launching("zStart Session FAS", _gamerManagerData);
            Logging.WriteLineGrouped("XXXX Flurry Launched XXXX", Logging.FlurryGroup);
         }
      }

      private void UpdateGameUsageCount()
      {
         // Ensure we're loaded
         LoadGamerManagerData();

         try
         {
            if (!GerbilPhysicsGame.IsRecoveringFromTombstoneOrFAS)
            {
               // We are a fresh run, not a TS or FAS
               _gamerManagerData.GameExecutionCount++;
               SaveGamerManagerData();
            }

            // Expose value
            _gameExecutionCount = _gamerManagerData.GameExecutionCount;
         }
         catch (Exception e)
         {
            // Any errors, metrics could be wrong
            Logging.WriteLine("ERROR: GamerManager.UpdateGameUsageCount failed:", e.Message);
         }

      }

      /// <summary>
      /// Increment Upsell Display count which is stored in IS
      /// </summary>
      public void IncrementUpsellDisplayCount()
      {
         // Ensure we're loaded
         LoadGamerManagerData();

         try
         {
            _gamerManagerData.UpsellDisplayCount++;

            // Save to IS
            SaveGamerManagerData();

            // Expose value
            _upsellDisplayCount = _gamerManagerData.UpsellDisplayCount;

            // Flurry call goes in here
            // 
         }
         catch (Exception e)
         {
            // Any errors dont really matter, this just affects visuals during upsell display and possibly wrong metrics
            Logging.WriteLine("ERROR: GamerManager.IncrementUpsellDisplayCount failed:", e.Message);
         }
      }

      /// <summary>
      /// Called when player signs in
      /// </summary>
      private void OnGamerSignedIn(object sender, SignedInEventArgs args)
      {
         Logging.WriteLine("LOG: SignedInGamer.SignedIn was raised, GamerManager.OnGamerSignedIn is executing");
#if ANDROID
		 // Android monogame implemenation doesn't have an args.Gamer property
			SignedInGamer gamer = null;
#else
         SignedInGamer gamer = args.Gamer;
#endif
			if (gamer == null)
         {
            Logging.WriteLine("WARNING: GamerManager.OnGamerSignedIn failed, gamer is null");
            return;
         }

         // We have signed in
         _signedInGamer = gamer;
         _gamerTag = _signedInGamer.Gamertag;

         // What sort of sign in is it?
         if (gamer.IsSignedInToLive)
         {
            // We are now signed in properly, with a LIVE user token
            _gamerManagerState = GamerManagerState.SignedIn;
            // Tell our listeners
            Logging.WriteLine("LOG: GamerManager.OnGamerSignedIn has successfully signed into Xbox LIVE");
            OnSignedIn(null, null);
         }
         else
         {
            // We have a signed in gamer object, but it has no LIVE user token
            _gamerManagerState = GamerManagerState.WaitingToSignIn;
            Logging.WriteLine("LOG: GamerManager.OnGamerSignedIn has signed in without Xbox LIVE and is now requesting Xbox LIVE user token refresh");
            OnSignedInWithoutLive(null, null);
            RefreshUserToken();
         }
      }

      /// <summary>
      /// Only used by test manager to disabled LIVE before mass test script runs
      /// </summary>
      public void SetLivePermissionsToNone()
      {
         _titleUpdateWasRefused = true;
         _livePermissions = LivePermissions.NotPermitted;
      }

      public override void Update(GameTime gameTime)
      {
         // Determine permissions (note this is separate logic from sign in status)
         // TU refusal used to disable everything, but not any more
         #region old
         //if (_titleUpdateWasRefused)
         //   _livePermissions = LivePermissions.NotPermitted;
         //else
         #endregion
         // TU refusal now only means that the leaderboards screen shouldnt be displayed (an error is displayed instead),
         // otherwise we can read/write achievements and write to leaderboards ok.
         // Notice that LivePermissions.None is now not used!

         if (_isTrialMode)
         {
            _livePermissions = LivePermissions.Permitted_ReadOnly;
         }
         else
         {
            _livePermissions = LivePermissions.Permitted_ReadWrite;
         }

         // Check what Guide and UI stuff is needed
         // A title update is available, we need to prompt the player to take the update
         if (_displayTitleUpdateMessage)
         {
            // Only display the selection once.
            if (!GuideIsVisible())
            {
               _displayTitleUpdateMessage = false;
               DisplayTitleUpdateMessage();
            }
         }
         // A title update was refused, inform player of consequences
         else if (_displayTitleUpdateRefusedInfoMessage)
         {
            // Only display the selection once.
            if (!GuideIsVisible())
            {
               _displayTitleUpdateRefusedInfoMessage = false;
               DisplayTitleUpdateRefusedMessage();
            }
         }
         // A show marketplace request has been received
         else if (_displayShowMarketPlace)
         {
            // Only display the selection once.
            if (!GuideIsVisible())
            {
               _displayShowMarketPlace = false;
               DisplayShowMarketPlace();
            }
         }
         // A show Sign In UI has been received
         else if (_displayGuideSignInUI)
         {
            // Only display the selection once.
            if (!GuideIsVisible())
            {
               _displayGuideSignInUI = false;
               DisplayGuideSignInUI();
            }
         }
         // A show keyboard for console request has been received
         else if (_displayPhoneKeyboardForConsole)
         {
            if (!GuideIsVisible())
            {
               _displayPhoneKeyboardForConsole = false;
               CommandConsoleShowPhoneKeyboard();
            }
         }

         base.Update(gameTime);
      }

      private void RefreshUserToken()
      {
         _displayGuideSignInUI = true;
      }

      public override void Draw(GameTime gameTime)
      {
         base.Draw(gameTime);

         if (!_IsContentLoaded)
            return;

         if (!_screenManager.DebugLiveEnabled)
            return;

         // Draw debug overlay panel with Live status info
         _spriteBatch.Begin();

         // Transparent background
         Rectangle rc = new Rectangle((int)_transpPos.X, (int)_transpPos.Y, 185, 70);
         _spriteBatch.Draw(_blankTexture, rc, _transpCol);

         // Info
#if NO_LIVE
         string enabled = string.Format("Live Enabled: No");
#else
         string enabled = string.Format("Live Enabled: Yes");
#endif
         string permission = string.Format("LiveAuth: {0}", GetDebugInfoPermission());
         string gamer = string.Format("Signed In: {0}", GetDebugInfoGamer());
         string bought = string.Format("Game Bought: {0}", (!IsTrialMode).ToStringYesNo());

         Color color = Color.White;

         // Info
         float x = _topLeftHUDSpace.X;
         float y = _topLeftHUDSpace.Y;
         Vector2 pos = new Vector2(x, y).ToCurrentBackBuffer();

         _spriteBatch.DrawString(_spriteFont, enabled, pos, color);
         y += _lineIncrementHUDSpace;
         pos = new Vector2(x, y).ToCurrentBackBuffer();

         _spriteBatch.DrawString(_spriteFont, permission, pos, color);
         y += _lineIncrementHUDSpace;
         pos = new Vector2(x, y).ToCurrentBackBuffer();

         _spriteBatch.DrawString(_spriteFont, gamer, pos, color);
         y += _lineIncrementHUDSpace;
         pos = new Vector2(x, y).ToCurrentBackBuffer();

         _spriteBatch.DrawString(_spriteFont, bought, pos, color);
         y += _lineIncrementHUDSpace;
         pos = new Vector2(x, y).ToCurrentBackBuffer();

         _spriteBatch.End();
      }

      private string GetDebugInfoPermission()
      {
         switch (_livePermissions)
         {
            case LivePermissions.NotPermitted:
               return "None";
               break;
            case LivePermissions.Permitted_ReadOnly:
               return "ReadOnly";
               break;
            case LivePermissions.Permitted_ReadWrite:
               return "ReadWrite";
               break;
            default:
               return "Error";
               break;
         }
      }

      private string GetDebugInfoGamer()
      {
         if (_signedInGamer != null)
         {
            if (_signedInGamer.IsSignedInToLive)
            {
               return "Live";
            }
            else
            {
               return "NoToken";
            }
         }
         else
         {
            return "Waiting";
         }
      }

      /// <summary>
      /// Wrapper around Guide.IsVisible to allow Windows builds compatibility
      /// </summary>
      private bool GuideIsVisible()
      {
#if WINDOWS_PHONE
         return Guide.IsVisible;
#else
         return false;
#endif
      }

      /// <summary>
      /// Force a check and update of IsTrialMode
      /// Potentially expensive 60ms, so don't call in a loop!
      /// </summary>
      public void UpdateIsTrialMode()
      {
         // We dont bother if we know we've already bought it.
         if (!_isTrialMode)
            return;

#if WINDOWS || ANDROID
         // On Windows builds it doesnt matter what you set Guide.IsTrialMode to, as IsTrialMode will always be true
         // Since we cannot currently simulate purchases on Windows builds anyway, we always act like we've bought it
         _isTrialMode = false;
         return;
#endif
         // The 60ms expensive check
         _isTrialMode = Guide.IsTrialMode;

#if DEBUG
         // Simulate trial mode 
         if (SimulateTrialMode)
         {
            _isTrialMode = true;
         }
         else
         {
            _isTrialMode = false;
         }
#endif
         // Whatever the result of the above, expose to our static 
         _isTrialModeStatic = _isTrialMode;

         // Flurry wants to know if this is the very first time we buy
         UpdateFlurryForPurchase();
      }

      /// <summary>
      /// Updates Flurry if game bought for the first time.
      /// Doesnt read IS, but could write to it, if we've bought title or if the IS flags are out of synch with the truth.
      /// </summary>
      private void UpdateFlurryForPurchase()
      {
         // Ensure we're loaded
         LoadGamerManagerData();

         Logging.WriteLineGrouped("Purchase Check", Logging.FlurryGroup);
         if (_gamerManagerData.IsGamePurchased)
         {
            // Our IS view tells us game is purchased, _isTrialMode contains the truth, let's compare them
            if (_isTrialMode)
            {
               // This should never happen, update IS to align it with truth
               _gamerManagerData.IsGamePurchased = false;
               Logging.WriteLineGrouped("Fixing IS flag IsGamePurchased to false so it matches IsTrialMode", Logging.FlurryGroup);
               SaveGamerManagerData();
            }
            else
            {
               // This is normal once bought, do nothing
            }
         }
         else
         {
            // Our IS view tells us game is NOT purchased, _isTrialMode contains the truth, let's compare them
            if (_isTrialMode)
            {
               // This is normal before bought, do nothing
            }
            else
            {
               // This is the point of purchase, update IS and update Flurry
               //FlurryEvents.ParamEvent("AppUnlocked", "Purchases", "Total Purchases");

               //Kev
               //ONLY ONCE - this should only ever be call once, the first time the game is run after purchase
               //after the game has been purchased this is being called every time the game is launched
               Logging.WriteLineGrouped("One Off Purchase Check", Logging.FlurryGroup);
               Flurry.AppUnlocked = true;
               if (Flurry.FlurryStarted)
               {
                  Flurry.OneOffChecks();//for late post flurry launch checks we need to explicitly call Flurry.OneOffChecks();
               }
               _gamerManagerData.IsGamePurchased = true;
               SaveGamerManagerData();
            }
         }
      }

      /// <summary>
      /// Reads IS and works out if we're a first ever user.
      /// Write to IS and updates Flurry if we are.
      /// </summary>
      private void UpdateFlurryForNewUser()
      {
         // Ensure we're loaded
         LoadGamerManagerData();

         Logging.WriteLineGrouped("New User Check", Logging.FlurryGroup);
         if (_gamerManagerData.IsUserNew)
         {
            //FlurryEvents.ParamEvent("AppDownloaded", "New Users", "New Users Total");
            Flurry.NewUser = true;
            //if (Flurry.FlurryStarted)
            //{
            //   Flurry.OneOffChecks();
            //}

            _gamerManagerData.IsUserNew = false;
            SaveGamerManagerData();
         }
      }

      /// <summary>
      /// Load gamermanager file into _gamerManagerData, if there is one.
      /// Returns true if we did, false if we didnt.
      /// </summary>
      private bool LoadGamerManagerData()
      {
         if (_isGamerManagerDataAlreadyLoaded)
            return false;

         // Whatever happens now, we regard ourselves as loaded
         _isGamerManagerDataAlreadyLoaded = true;

         // Defaults will be overwritten if we successfully load from file, and will persist if we cannot load from file
         _gamerManagerData = new GamerManagerData()
         {
            IsUserNew = true,
            IsGamePurchased = false,
            GameExecutionCount = 0,
            UpsellDisplayCount = 0,
         };

         if (_screenManager.StorageManager.FileExists(_gamerManagerData.GetType()))
         {
            try
            {
               _gamerManagerData = (GamerManagerData)_screenManager.StorageManager.Load(_gamerManagerData.GetType());
               return true;
            }
            catch (Exception e)
            {
               // Any errors, we dont really care, we will default anyway
               Logging.WriteLine("LOG: WARNING: GamerManager.LoadGamerManagerData failed although file exists:", e.Message);
               return false;
            }
         }
         else
         {
            // We couldnt find file
            return false;
         }
      }

      private void SaveGamerManagerData()
      {
         try
         {
            _screenManager.StorageManager.Save(_gamerManagerData);
         }
         catch (Exception e)
         {
            // Any errors, we dont really care, we will be defaulting to panel zero next time
            Logging.WriteLine("LOG: WARNING: GamerManager.SaveGamerManagerData failed:", e.Message);
         }
      }

      /// <summary>
      /// Delete the locally saved GamerManager data file, then load a new default one
      /// Used by the options menu cheats to delete all local data
      /// </summary>
      public void DeleteAndRecreateGamerManagerSaveDataFile()
      {
         StorageManager sm = _screenManager.StorageManager;

         // Delete the file
         if (sm.DeleteFile(_gamerManagerData.GetType()))
         {
            // Delete ok
            Logging.WriteLine("LOG: DeleteGamerManagerSaveDataFile has deleted local GamerManager data file");
         }
         else
         {
            Logging.WriteLine("LOG: DeleteGamerManagerSaveDataFile couldnt delete file, perhaps it didnt exist");
         }

         // Now load a default file
         LoadGamerManagerData();
      }

      protected override void UnloadContent()
      {
         _gamerManagerData.AchievementsCount = AchievementsGainedCount;

         base.UnloadContent();
      }

      /// <summary>
      /// The Title Update TCR says we have to be able to simulate a TU at any point in the game.
      /// When a TU happens, it is in fact the GamerServicesComponent that will throw GameUpdateRequiredException.
      /// However, we are also a component, and get Updated() by the base.Update() call inside the main Game update
      /// and so if we throw the same exception we are pretty close to simulating this correctly.
      /// </summary>
      public void SimulateTitleUpdate()
      {
         throw new GameUpdateRequiredException();
      }

      /// <summary>
      /// Example exception handler for a GamerServices title update exception.
      /// Note: when the exception fires, additional components added to the 
      /// application component list will miss a frame’s worth of processing.
      /// </summary>
      public void HandleGameUpdateRequired(GameUpdateRequiredException e)
      {
         // Disable the Gamer Services Component
         // This is critical to avoid multiple occurrences of the exception being fired (it will get set to true
         // next time game starts and no exceptions are thrown)
         _gamerServices.Enabled = false;

         // Signal to our UI code that we want a title update choice to be presented to the user.
         _displayTitleUpdateMessage = true;
      }

      /// <summary>
      /// For achievements, the player does not need to be signed in to LIVE.  So someone can play with LIVE disabled and
      /// still get achievements.
      /// </summary>
      public bool CheckSignInStatusForAchievements(string caller, bool suppressLog)
      {
         bool isSignInOK = false;
         //if (_signedInGamer != null && _signedInGamer.IsSignedInToLive && !_signedInGamer.IsDisposed)
         if (_signedInGamer != null && !_signedInGamer.IsDisposed)
         {
            isSignInOK = true;
         }
         else
         {
            if (!suppressLog)
            {
               string reason = string.Empty;
               if (_signedInGamer == null)
                  reason = "SignedInGamer is null";
               //else if (!_signedInGamer.IsSignedInToLive)
               //   reason = "SignedInGamer is not signed in to Live";
               else if (_signedInGamer.IsDisposed)
                  reason = "SignedInGamer is disposed";

               Logging.WriteLine("LOG: WARNING: CheckSignInStatusForAchievements failed for caller: {0}, reason: {1}", caller, reason);
            }
         }
         return isSignInOK;
      }

      /// <summary>
      /// For leaderboards, the player does need to be signed in to LIVE
      /// </summary>
      public bool CheckSignInStatusForLeaderboards(string caller, bool suppressLog)
      {
         bool isSignInOK = false;
         if (_signedInGamer != null && _signedInGamer.IsSignedInToLive && !_signedInGamer.IsDisposed)
         {
            isSignInOK = true;
         }
         else
         {
            if (!suppressLog)
            {
               string reason = string.Empty;
               if (_signedInGamer == null)
                  reason = "SignedInGamer is null";
               else if (!_signedInGamer.IsSignedInToLive)
                  reason = "SignedInGamer is not signed in to Live";
               else if (_signedInGamer.IsDisposed)
                  reason = "SignedInGamer is disposed";

               Logging.WriteLine("LOG: WARNING: CheckSignInStatusForLeaderboards failed for caller: {0}, reason: {1}", caller, reason);
            }
         }
         return isSignInOK;
      }

      /// <summary>
      /// Show marketplace to buy game
      /// </summary>
      public void BuyGame()
      {
         // If we've already bought it then bail
         if (!_isTrialMode)
         {
            FlurryEvents.ParamEvent("Menu - Main", "Buy Tapped", "Already Bought");
            return;
         }

#if DEBUG
         // If we're running a test script, ie we've been called by a test script, then we've bought it
         if (TestManager.Instance != null && TestManager.Instance.IsRunningTests)
         {
            _isTrialMode = false;
            FlurryEvents.ParamEvent("Menu - Main Menu", "Buy Tapped", "Panic if you see this in live data!!");
            return;
         }
#endif
         bool canPlayerBuyGame = CheckCanPlayerBuyGame();
         if (canPlayerBuyGame)
         {
            // Just set flag for Guide UI part to kick in later
            _displayShowMarketPlace = true;
            FlurryEvents.ParamEvent("Menu - Main Menu", "Buy Tapped", "Sent To Marketplace");
         }
         else
         {
            // Info message saying game cannot be bought
            MessageBoxScreen cannotBuyGame = new MessageBoxScreen(LiveIntegrationResource.MessageBox_CannotBuyGameTitle,
               LiveIntegrationResource.MessageBox_CannotBuyGame, MessageBoxButtons.OK, 580);
            _screenManager.AddScreen(cannotBuyGame, PlayerIndex.One);
            FlurryEvents.ParamEvent("Menu - Main Menu", "Buy Tapped", "Cannot Buy Popup");
         }
      }

      /// <summary>
      /// Check to see if player has permissions for, and access to, LIVE so that
      /// we know its safe to call the marketplace dialog to buy the game
      /// </summary>
      public bool CheckCanPlayerBuyGame()
      {
#if DEBUG
         // Just testing using emu (when we can never be connected to live so can never buy game)
         if (GamerManager.AllowOfflinePurchase)
            return true;
#endif
         // If the player isn't signed in, they can't buy games
         if (_signedInGamer == null)
            return false;

         // If the player isn't on LIVE, they can't buy games
         if (!_signedInGamer.IsSignedInToLive)
            return false;

         // Apparanlty purchase auths are checked by MS, so below prob not required
         //// lastly check to see if the account is allowed to buy games
         //return gamer.Privileges.AllowPurchaseContent;
         return true;
      }

      ///// <summary>
      ///// DEPRECATED in favour of Upsell version below
      ///// </summary>
      ///// <param name="playerIndex"></param>
      //public void TrialModeExpiredPopupDisplay(PlayerIndex playerIndex)
      //{
      //   // Popup says end of trial mode do you want to buy
      //   MessageBoxScreen trialModeExpiredPopup =
      //     new MessageBoxScreen(LiveIntegrationResource.MessageBox_EndOfTriaMode, MessageBoxButtons.YesNo);

      //   trialModeExpiredPopup.Accepted += TrialModeExpiredPopupBox_Purchased;
      //   trialModeExpiredPopup.Cancelled += TrialModeExpiredPopupBox_Declined;
      //   _screenManager.AddScreen(trialModeExpiredPopup, playerIndex);
      //}

      public void TrialModeExpiredPopupUpsellDisplay(PlayerIndex playerIndex)
      {
         if (!_screenManager.IsThereAnyOtherUpsellScreensBeingDisplayed())
         {
            // Popup says end of trial mode do you want to buy
            MessageBoxUpsellScreen trialModeExpiredPopup =
              new MessageBoxUpsellScreen(LiveIntegrationResource.MessageBox_EndOfTrialModeTitle, LiveIntegrationResource.MessageBox_EndOfTriaMode, MessageBoxButtons.YesNo);

            trialModeExpiredPopup.Accepted += TrialModeExpiredPopupBox_Purchased;
            trialModeExpiredPopup.Cancelled += TrialModeExpiredPopupBox_Declined;
            _screenManager.AddScreen(trialModeExpiredPopup, playerIndex);
         }
      }

      /// <summary>
      /// Accepted the offer of purchase
      /// </summary>
      void TrialModeExpiredPopupBox_Purchased(object sender, PlayerIndexEventArgs e)
      {
         BuyGame();
      }

      /// <summary>
      /// Declined the offer of purchase, off back to level select then
      /// </summary>
      void TrialModeExpiredPopupBox_Declined(object sender, PlayerIndexEventArgs e)
      {
         LoadingScreen.Load(_screenManager, BackButtonBehaviour.Pause, false, null,
                                                         new BackgroundScreen(),
                                                         new MainMenuScreen(),
                                                         new LevelsMenuScreen());
      }

      /// <summary>
      /// The Guide UI runs asynchronously; this is the callback following a user selection.
      /// </summary>
      private void DisplayTitleUpdateMessageGetResult(IAsyncResult userResult)
      {
         // The 60ms check on trial mode status
         UpdateIsTrialMode();

         int? buttonChoice = Guide.EndShowMessageBox(userResult);

         if (buttonChoice.HasValue)
         {
            // Button order matters here; 0 is “Yes” in our displayed strings list.
            if ((int)buttonChoice == 0)
            {
               // User has requested we take the update
               if (_isTrialMode)
               {
                  // When we are in trial mode, we use Guide.ShowMarketplace
                  // We'll deal with this from Update() as it involves waiting for previous messages to disappear
                  _displayShowMarketPlace = true;
               }
#if WINDOWS_PHONE
               else
               {
                  // When we are not in trial mode, Guide.ShowMarketplace does nothing (it seems)
                  // The sample says "Temporary workaround for dealing with a fully-purchased title" about this code:
                  Microsoft.Phone.Tasks.MarketplaceDetailTask details = new Microsoft.Phone.Tasks.MarketplaceDetailTask();
                  details.ContentType = Microsoft.Phone.Tasks.MarketplaceContentType.Applications;
                  details.Show();
               }
#endif
            }
            else
            {
               // User has declined the update
               // Flag to make info popup appear
               _displayTitleUpdateRefusedInfoMessage = true;

               // Remember the refusal for calculating permissions
               _titleUpdateWasRefused = true;
            }

         }
      }

      /// <summary>
      /// Displays marketplace guide on the phone offering a purchase.
      /// On Windows just inform this would happen.
      /// </summary>
      private void DisplayShowMarketPlace()
      {

#if DEBUG
         // Remove command console
         DebugSystem.Instance.DebugCommandUI.Hide();
#endif

#if WINDOWS_PHONE
         // Phone ------------------------------------------------------
#if DEBUG
         // Debug on phone, means we offer chance for fake purchase
         MessageBoxScreen gamePurchased =
         new MessageBoxScreen(LiveIntegrationResource.MessageBox_SimulatePurchaseTitle,
            LiveIntegrationResource.MessageBox_SimulatePurchase, MessageBoxButtons.YesNo, 580);
         gamePurchased.Accepted += OnSimulatePurchaseMessageBoxAccepted;
         _screenManager.AddScreen(gamePurchased, PlayerIndex.One);
#else
         // Phone release, means use marketplace
         Guide.ShowMarketplace(PlayerIndex.One);
         UpdateIsTrialMode();
#endif

#else
         // Windows ----------------------------------------------------
         // Do a MessageBox equivalent
         string str = "Marketplace would be displayed here" +
                    "\noffering in game purchase. Cannot" +
                    "\nsimulate this in Windows builds.";
         //System.Windows.Forms.MessageBox.Show(str, "Marketplace", System.Windows.Forms.MessageBoxButtons.OK, 580);
#endif
      }

#if DEBUG
      void OnSimulatePurchaseMessageBoxAccepted(object sender, PlayerIndexEventArgs e)
      {
         // Simulate purchase
         GamerManager.SimulateTrialMode = false;
         UpdateIsTrialMode();
      }
#endif

      /// <summary>
      /// Used to refresh user token
      /// </summary>
      private void DisplayGuideSignInUI()
      {
#if DEBUG
         DebugSystem.Instance.DebugCommandUI.Hide();
#endif
#if WINDOWS_PHONE
         Guide.ShowSignIn(1, true);
#else
         // Do nothing
#endif
      }

      /// <summary>
      /// Displays TU update information, suggesting the user takes it.
      /// On Windows just inform this would happen.
      /// </summary>
      private void DisplayTitleUpdateMessage()
      {
#if DEBUG
         DebugSystem.Instance.DebugCommandUI.Hide();
#endif
#if WINDOWS_PHONE || ANDROID
         // Guide offers the TU
         Guide.BeginShowMessageBox(LiveIntegrationResource.Guide_TUOfferTitle, LiveIntegrationResource.Guide_TUOfferMessage,
            _dialogButtonsYesNo, 0, MessageBoxIcon.Alert, DisplayTitleUpdateMessageGetResult, null);
#else
         // Do a MessageBox equivalent
         System.Windows.Forms.MessageBox.Show("An offer of a Title Update would be displayed here", "Update available", System.Windows.Forms.MessageBoxButtons.OK);
#endif
      }

      /// <summary>
      /// Displays TU update information, suggesting the user takes it.
      /// On Windows just inform this would happen.
      /// </summary>
      private void DisplayTitleUpdateRefusedMessage()
      {
#if DEBUG
         DebugSystem.Instance.DebugCommandUI.Hide();
#endif
#if WINDOWS_PHONE || ANDROID
         // Tell player TU was declined
         Guide.BeginShowMessageBox(LiveIntegrationResource.Guide_TUDeclineTitle, LiveIntegrationResource.Guide_TUDeclineMessage,
            _dialogButtonsOK, 0, MessageBoxIcon.Alert, null, null);
#else
         // Do a MessageBox equivalent
         System.Windows.Forms.MessageBox.Show("No access to Xbox Live.  Leaderboards and Achievements not updated.", "Update Was Declined", System.Windows.Forms.MessageBoxButtons.OK);
#endif
      }

      /// <summary>
      /// Request that we show phone keyboard and pass command to console
      /// </summary>
      public void DisplayPhoneKeyboardForConsole()
      {
         _displayPhoneKeyboardForConsole = true;
      }

      private void CommandConsoleShowPhoneKeyboard()
      {
#if DEBUG && ( WINDOWS_PHONE || ANDROID )
         _keyboardResult = Guide.BeginShowKeyboardInput(PlayerIndex.One, "Debug Console. Type a Command:",
            "Type help for help or: far, fps, tr, l p, l w, l f, clamp, etc.", "",
            CommandConsoleGetAndSendCharacters, null);
#endif
      }

      private void CommandConsoleGetAndSendCharacters(IAsyncResult result)
      {
         _keyboardTypedCharacters = Guide.EndShowKeyboardInput(result);
         DebugSystem.Instance.DebugCommandUI.Hide();
         if (_keyboardTypedCharacters != null && _keyboardTypedCharacters != "")
         {
            DebugSystem.Instance.DebugCommandUI.ExecuteCommand(_keyboardTypedCharacters);
         }
      }

      protected override void Dispose(bool disposing)
      {
         // When we start shutting down, we want to update the achivement count in IS, this ensures the achivement
         // count is available for reading by Flurry when Flurry next starts up
         _gamerManagerData.AchievementsCount = AchievementsGainedCount;
         SaveGamerManagerData();

         base.Dispose(disposing);

#if !NO_LIVE
         SignedInGamer.SignedIn -= OnGamerSignedIn;
#endif
         // This isn't needed, because neither object is short lived
         //if (_screenManager != null && _screenManager.AchievementManager != null && _screenManager.AchievementManager.OnAchievementDataChanged != null)
         //{
         //   _screenManager.AchievementManager.OnAchievementDataChanged -= OnAchievementsDataChanged;
         //}
      }

      private enum GamerManagerState
      {
         WaitingToSignIn,
         SignedIn,
      }
   }
}
