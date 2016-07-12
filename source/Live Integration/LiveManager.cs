using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using TimerInterpolator;

namespace GP.Live_Integration
{
   /// <summary>
   /// Top level of the Live managers. Leaderboard and Achievement managers are derived from this.
   /// Live managers are special, because only they should use objects from the GamerServices namespace.
   /// This makes porting easier as only our LiveManagers will need to change if Leaderboards and Achievements change.
   /// 
   /// NOTE ON CALLING LIVE METHODS
   /// ============================
   /// Two layers of checks before any access to a Live method:
   ///   Permissions Check - use LivePermissions to see if player has read only, read write or none
   ///   Sign In Check - if permissions check ok, then do a sign check calling _gamerManager.ValidateSignIn*
   ///   
   /// There is also a special extra check in the LeaderboardsMenuScreen to suppress the leaderboards output if a TU was refused.
   /// See also comments in the GamerManager.
   /// </summary>
   public abstract class LiveManager : IDisposable
   {
      /// <summary>
      /// Current status of our live permissions. Ideally, external objects should not need to read this, all
      /// handling of permissions is done inside the live managers.
      /// </summary>
      public LivePermissions LivePermissions { get { return _gamerManager.LivePermissions; } }

      protected ScreenManager _screenManager;
      protected ContentManager _contentManager;
      protected GamerManager _gamerManager;

      /// <summary>
      /// Flag if all content, including deferred content, has been loaded
      /// </summary>
      protected bool _contentLoaded = false;

      protected TimerCollection _timerCollection;

      public LiveManager(ScreenManager screenManager)
      {
         _screenManager = screenManager;
         _contentManager = _screenManager.ContentManager;
      }

      public virtual void Initialize()
      {
         _gamerManager = (GamerManager)GerbilPhysicsGame.GameServices.GetService(typeof(GamerManager));
         _timerCollection = new TimerCollection();
      }

      /// <summary>
      /// Load critical content - that is, before the splash screen has appeared, which we want to keep to a minimum
      /// This is done before any Update() calls
      /// </summary>
      public virtual void LoadContent() { }

      /// <summary>
      /// Load deferred content - that is, only after the splash screen has appeared
      /// This means we will have called Update() a few times before this is called
      /// </summary>
      public virtual void LoadContentDeferred() { }

      public virtual void Update(float elapsedTime)
      {
         // Permissions are determined by the GamerManager (include disabling GamerServices), but we can react
         // to the permissions here. Bear in mind permissions can change at any time.
         // if (_gamerManager.LivePermissions == LivePermissions.NotPermitted)
         // etc
         _timerCollection.Update(elapsedTime);
      }

      public virtual void Draw(float elapsedTime) { }

      /// <summary>
      /// Close any unmanaged resources, remove subscriptions to static events
      /// </summary>
      public virtual void Dispose()
      {

      }
   }

   public enum LivePermissions
   {
      /// <summary>
      /// No access at all is allowed to any LIVE functionality - all aspects of read and write to leaderboards and achievements is disabled.
      /// NOTE: This used to be what I thoght the right behaviour was for a TU-refusal, but I was wrong.  TU refusal doesnt affect anything(!) to
      /// do with leaderboards and achievements, it just means we should NOT display the leaderboard.  This special case is now handled in the
      /// leaderboards menu screen.
      /// At the time of writing, this NotPermited case is never used by the game at runtime, but I've left it in because it seems useful.
      /// </summary>
      NotPermitted,

      /// <summary>
      /// In Trial mode we can only read Live services, not write to them (although we can store
      /// achievements locally)
      /// </summary>
      Permitted_ReadOnly,

      /// <summary>
      /// Normal play is when the game has been bought and no TU is pending
      /// </summary>
      Permitted_ReadWrite,
   }
}
