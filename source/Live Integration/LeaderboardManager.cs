using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.GamerServices;  // <-- only LiveManagers should use objects in this namespace
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using GP.Helpers;
using System.Net;

namespace GP.Live_Integration
{
   /// <summary>
   /// Leaderboard manager
   /// Maintains a dictionary of LeaderboardGenerics, a publically accessible list of platform-independent leaderboards.
   /// Unlike achievements, which all get filled by the manager, leaderboards must be setup by external calls.
   /// To use:
   ///   1) Subscribe to event OnLeaderboardDataChanged, 
   ///   2) Call InitializeLeaderboard()
   ///   3) When you hear event raised, call GetLeaderboardGeneric() which returns the leaderboard datag
   /// Only one leaderboard can be read at a time, and all pageup/down calls are relevant to the last initialised leaderboard.
   /// </summary>
   /// <remarks>
   /// Note on locking using _lockObjectLeaderboards
   ///   CallbackGetAvatarImages uses lock object whilst *updating* _leaderboardsGeneric to add new images.
   ///   CallbackLeaderboardRead uses lock object whilst *updating* _leaderboardsGeneric, deleting it and refreshing it.
   ///   CallbacLeaderboardPageUp Down will need to use lock object as it is *updating* _leaderboardsGeneric.
   /// </remarks>
   public class LeaderboardManager : LiveManager
   {
      /// <summary>
      /// Event raised when leaderboard data changes involving a possible change in the total number of entries.
      /// For example, a leaderboard initialise or a page up/down where the total number of entries changes.
      /// </summary>
      public event EventHandler<LeaderboardDataEventArgs> OnLeaderboardDataChanged = delegate { };

      /// <summary>
      /// Event raised when leaderboard data values change, but no change in the total number of entries.
      /// For example, a page up/down where the total number of entries remain constant.
      /// </summary>
      public event EventHandler<LeaderboardDataEventArgs> OnLeaderboardDataValuesChanged = delegate { };

      /// <summary>
      /// Event raised when leaderboard data images change, but no change in the total number of entries.
      /// For example, an avatar image was retrieved.
      /// </summary>
      public event EventHandler<EventArgs> OnLeaderboardDataImagesChanged = delegate { };

      /// <summary>
      /// Event raised when a leaderboard page up/down errors.  This can happen if LIVE servers timeout, or if the leaderboard
      /// is already in the middle of a previous page up/down call.
      /// </summary>
      public event EventHandler<LeaderboardDataEventArgs> OnLeaderboardPageErrored = delegate { };

      /// <summary>
      /// Event raised when an initial read of a leaderboard errors.  This can happen if LIVE servers timeout, or if the player has
      /// disabled LIVE access on their phone (via games -> settings -> connect with Xbox Live).
      /// </summary>
      public event EventHandler<EventArgs> OnLeaderboardInitialReadErrored = delegate { };

      /// <summary>
      /// Platform independent leaderboards
      /// </summary>
      private Dictionary<LeaderboardGenericID, LeaderboardGeneric> _leaderboardsGeneric;

      /// <summary>
      /// Image to display when gamer is not yet known
      /// </summary>
      private Texture2D _unknownGamer;

      /// <summary>
      /// List of dummy gamertag images, used when not delete once Live in place
      /// </summary>
      private List<Texture2D> _placeholderImages;

      /// <summary>
      /// Flag controlling the one-off initial write to leaderboards, done as soon as permissions allow it
      /// </summary>
      private bool _runOnceWriteLeaderboardScores;

      /// <summary>
      /// Leaderboard currently in use, null if none in use.  This is created in leaderboard initialise callback and
      /// then reused for the page up down calls.
      /// </summary>
      private LeaderboardReader _leaderboardReaderCurrent;

      /// <summary>
      /// Total entries in current leaderboard when we last read it from LIVE
      /// </summary>
      private int _leaderboardReaderTotalEntriesCurrent;

      /// <summary>
      /// Leaderboard identity of leaderboard currently in use. This ID is very important, as any callbacks we recieve
      /// that are NOT for this ID, we will ignore.
      /// </summary>
      private LeaderboardIdentity _leaderboardIdentityCurrent;

      private object _lockObjectLeaderboards = new object();

      private Queue<string> _gamersNeedingAvatars;
      private AvatarImageReadStatus _avatarImageReadStatus;

      public LeaderboardManager(ScreenManager screenManager)
         : base(screenManager)
      {
      }

      private bool LeaderboardIDsAreEqual(LeaderboardIdentity id1, LeaderboardIdentity id2)
      {
         if (id1.GameMode == id2.GameMode && id1.Key == id2.Key)
            return true;
         else
            return false;
      }

      /// <summary>
      /// Called when a leaderboard reader finishes retrieving its initial data from LIVE
      /// </summary>
      protected void CallbackLeaderboardInitialRead(IAsyncResult result)
      {
         Logging.WriteLine("LOG: ASYNCH IN<- LeaderboardManager is in its callback for leaderboard read");
         TrackerObjectLive trackerObject = result.AsyncState as TrackerObjectLive;
         SignedInGamer gamer = trackerObject.Gamer as SignedInGamer;
         LeaderboardIdentity leaderboardIdentity = trackerObject.LeaderboardIdentity;

         if (gamer == null)
         {
            Logging.WriteLine("ERROR: in CallbackLeaderboardRead SignedInGamer was null");
            return;
         }

         // We're modifying our leaderboard lists, dont want to clash with avatar image retrieval or page up/down
         lock (_lockObjectLeaderboards)
         {
            try
            {
               if (LeaderboardIDsAreEqual(_leaderboardIdentityCurrent, leaderboardIdentity))
               {
                  // Our callback is for our current leaderboard, good
                  _leaderboardReaderCurrent = LeaderboardReader.EndRead(result);
                  _leaderboardReaderTotalEntriesCurrent = _leaderboardReaderCurrent.TotalLeaderboardSize;
                  _leaderboardIdentityCurrent = leaderboardIdentity;
                  // Bin all the entries data for this leaderboard, and copy over afresh
                  CopyLeaderboardEntriesToLeaderboardGeneric(true);
                  OnLeaderboardDataChangedRaise(_leaderboardReaderCurrent.CanPageDown, _leaderboardReaderCurrent.CanPageUp);
               }
               else
               {
                  // This callback is not for the leaderboard we're currently looking at
                  LeaderboardReader.EndRead(result);
                  Logging.WriteLine(
                     "WARNING: in CallbackLeaderboardRead, ignoring callback because returned ID (id:{0},{1}) is not current ID (id:{2},{3})",
                     leaderboardIdentity.GameMode, leaderboardIdentity.Key,
                     _leaderboardIdentityCurrent.GameMode, _leaderboardIdentityCurrent.Key
                     );
               }
            }
            catch (Exception e)
            {
               // In case the leaderboard reader times out, or LIVE is disabled on phone
               Logging.WriteLine("ERROR: in CallbackLeaderboardRead {0}", e.Message);
               OnLeaderboardInitialReadErroredRaise();
            }
         }
      }

      /// <summary>
      /// Called when a leaderboard reader finishes a page up or down
      /// </summary>
      protected void CallbackLeaderboardPage(IAsyncResult result)
      {
         Logging.WriteLine("LOG: ASYNCH IN<- LeaderboardManager is in its callback for leaderboard page");
         TrackerObjectLive trackerObject = result.AsyncState as TrackerObjectLive;
         SignedInGamer gamer = trackerObject.Gamer as SignedInGamer;
         LeaderboardIdentity leaderboardIdentity = trackerObject.LeaderboardIdentity;
         bool isPageUp = trackerObject.IsPageUp;

         if (gamer == null)
         {
            Logging.WriteLine("ERROR: in CallbackLeaderboardPage SignedInGamer is null");
            return;
         }

         if (_leaderboardReaderCurrent == null)
         {
            Logging.WriteLine("ERROR: in CallbackLeaderboardPage _leaderboardReaderCurrent is null");
            return;
         }

         // We're modifying our leaderboard lists, dont want to clash with avatar image retrieval or an initialise
         lock (_lockObjectLeaderboards)
         {
            try
            {
               if (LeaderboardIDsAreEqual(_leaderboardIdentityCurrent, leaderboardIdentity))
               {
                  // Our callback is for our current leaderboard, good
                  if (isPageUp)
                     _leaderboardReaderCurrent.EndPageUp(result);
                  else
                     _leaderboardReaderCurrent.EndPageDown(result);

                  // If we get a different total number of leaderboard entries, we must trash the generic leaderboard
                  bool refreshLeaderboard = false;
                  if (_leaderboardReaderTotalEntriesCurrent != _leaderboardReaderCurrent.TotalLeaderboardSize)
                  {
                     Logging.WriteLine("WARNING: in CallbackLeaderboardPage _leaderboardReaderCurrent has different total entries, was {0}, now {1}",
                        _leaderboardReaderTotalEntriesCurrent,
                        _leaderboardReaderCurrent.TotalLeaderboardSize);
                     refreshLeaderboard = true;
                     _leaderboardReaderTotalEntriesCurrent = _leaderboardReaderCurrent.TotalLeaderboardSize;
                  }

                  // Blend or refresh in the new entries contained within _leaderboardReaderCurrent
                  CopyLeaderboardEntriesToLeaderboardGeneric(refreshLeaderboard);

                  // Tell listeners the data has changed
                  if (refreshLeaderboard)
                     OnLeaderboardDataChangedRaise(_leaderboardReaderCurrent.CanPageDown, _leaderboardReaderCurrent.CanPageUp);
                  else
                     OnLeaderboardDataValuesChangedRaise(_leaderboardReaderCurrent.CanPageDown, _leaderboardReaderCurrent.CanPageUp);
               }
               else
               {
                  // This callback is not for the leaderboard we're currently looking at
                  Logging.WriteLine(
                     "WARNING: in CallbackLeaderboardPage, ignoring callback because returned ID (id:{0},{1}) is not current ID (id:{2},{3})",
                     leaderboardIdentity.GameMode, leaderboardIdentity.Key,
                     _leaderboardIdentityCurrent.GameMode, _leaderboardIdentityCurrent.Key
                     );
               }
            }
            catch (Exception e)
            {
               // In case the leaderboard reader times out
               Logging.WriteLine("ERROR: LeaderboardManager in CallbackLeaderboardPage {0}", e.Message);
               OnLeaderboardPageErroredRaise(_leaderboardReaderCurrent.CanPageDown, _leaderboardReaderCurrent.CanPageUp);
            }
         }
      }

      /// <summary>
      /// Copy the entries found in _leaderboardReaderCurrent to our generic leaderboard.
      /// The entries are copied to the right place, so if _leaderboardReaderCurrent contains ranks 6-8 then they get copied
      /// to the right place in the generic leaderboard which contains every entry.
      /// The generic leaderboard can optionally be refreshed first, which should be done if the total number of entries
      /// has changed.
      /// </summary>
      private void CopyLeaderboardEntriesToLeaderboardGeneric(bool refreshGenericLeaderboard)
      {
         int entriesTotalNaturalNumber = _leaderboardReaderCurrent.TotalLeaderboardSize;
         int entriesInThisReadNaturalNumber = _leaderboardReaderCurrent.Entries.Count;
         int rankOfFirstItemZeroBased = _leaderboardReaderCurrent.PageStart;

         LeaderboardGenericID leaderboardGenericID = GetLeaderboardGenericID(_leaderboardIdentityCurrent);

         if (refreshGenericLeaderboard)
         {
            //-------------------------------------------------------------------------------------------------
            // Delete / Recreate
            //-------------------------------------------------------------------------------------------------
            _leaderboardsGeneric[leaderboardGenericID].EntriesGeneric.Clear();
            // Copy to our generic leaderboard, with unknown images for now
            for (int indexZeroBased = 0; indexZeroBased < entriesTotalNaturalNumber; indexZeroBased++)
            {
               if (indexZeroBased >= rankOfFirstItemZeroBased &&
                   indexZeroBased < rankOfFirstItemZeroBased + entriesInThisReadNaturalNumber)
               {
                  // Index is in the page we got back, copy values from reader
                  LeaderboardEntry entry = _leaderboardReaderCurrent.Entries[indexZeroBased - rankOfFirstItemZeroBased];
                  LeaderboardEntryGeneric entryGeneric = new LeaderboardEntryGeneric(entry);
                  entryGeneric.Image = _unknownGamer;
                  entryGeneric.Rank = indexZeroBased + 1;
                  _leaderboardsGeneric[leaderboardGenericID].EntriesGeneric.Add(entryGeneric);
               }
               else
               {
                  // Index is not in the page we got back, create blank entry
                  LeaderboardEntryGeneric entryGeneric = new LeaderboardEntryGeneric();
                  entryGeneric.Image = _unknownGamer;
                  entryGeneric.Rank = indexZeroBased + 1;
                  _leaderboardsGeneric[leaderboardGenericID].EntriesGeneric.Add(entryGeneric);
               }
            }
            _leaderboardsGeneric[leaderboardGenericID].TotalLeaderboardSize = entriesInThisReadNaturalNumber;
         }
         else
         {
            //-------------------------------------------------------------------------------------------------
            // Blend
            //-------------------------------------------------------------------------------------------------
            for (int indexZeroBased = 0; indexZeroBased < entriesInThisReadNaturalNumber; indexZeroBased++)
            {
               int entryLiveIndex = indexZeroBased;
               LeaderboardEntry entryLive = _leaderboardReaderCurrent.Entries[entryLiveIndex];

               int entryGenericIndex = indexZeroBased + rankOfFirstItemZeroBased;
               LeaderboardEntryGeneric entryGeneric = _leaderboardsGeneric[leaderboardGenericID].EntriesGeneric[entryGenericIndex];

               entryGeneric.GamerTag = entryLive.Gamer.Gamertag;
               entryGeneric.GamerTagKnown = true;
               entryGeneric.Image = _unknownGamer;
               entryGeneric.ImageKnown = false;
               entryGeneric.Rating = entryLive.Rating;
               entryGeneric.Rank = entryGenericIndex + 1;
            }
         }
      }

      private void OnLeaderboardDataChangedRaise(bool canPageDown, bool canPageUp)
      {
         Logging.WriteLine("LOG: LeaderboardManager raised OnLeaderboardDataChanged - data refresh, can page up:{0}, can page down:{1}", canPageUp, canPageDown);
         LeaderboardDataEventArgs args = new LeaderboardDataEventArgs()
         {
            CanPageDown = canPageDown,
            CanPageUp = canPageUp
         };
         OnLeaderboardDataChanged(this, args);
      }

      private void OnLeaderboardDataValuesChangedRaise(bool canPageDown, bool canPageUp)
      {
         Logging.WriteLine("LOG: LeaderboardManager raised OnLeaderboardDataValuesChanged - data values changed, can page up:{0}, can page down:{1}", canPageUp, canPageDown);
         LeaderboardDataEventArgs args = new LeaderboardDataEventArgs()
         {
            CanPageDown = canPageDown,
            CanPageUp = canPageUp
         };
         OnLeaderboardDataValuesChanged(this, args);
      }

      private void OnLeaderboardDataImagesChangedRaise()
      {
         Logging.WriteLine("LOG: LeaderboardManager raised OnLeaderboardDataImagesChanged - data images changed");
         OnLeaderboardDataImagesChanged(this, EventArgs.Empty);
      }

      private void OnLeaderboardPageErroredRaise(bool canPageDown, bool canPageUp)
      {
         Logging.WriteLine("LOG: LeaderboardManager raised OnLeaderboardPageErrored, perhaps a timeout or an existing page up/down has not finished, can page up:{0}, can page down{1}", canPageUp, canPageDown);
         LeaderboardDataEventArgs args = new LeaderboardDataEventArgs()
         {
            CanPageDown = canPageDown,
            CanPageUp = canPageUp
         };
         OnLeaderboardPageErrored(this, args);
      }

      public void OnLeaderboardInitialReadErroredRaise()
      {
         Logging.WriteLine("LOG: LeaderboardManager raised LeaderboardInitialReadErrored, perhaps a timeout or Xbox LIVE is disabled on phone");
         OnLeaderboardInitialReadErrored(this, EventArgs.Empty);
      }

      /// <summary>
      /// Returns the leaderboard with the specified ID, or null if the leaderboard does not yet
      /// exist, which can happen due to waiting on asynch calls, being offline etc.
      /// </summary>
      public LeaderboardGeneric GetLeaderboardGeneric(LeaderboardGenericID leaderboardGenericID)
      {
         LeaderboardGeneric leaderboardGeneric = null;

         // We're retrieving our leaderboard list, dont want to clash with possible leaderboard refreshes
         // Doing a "lock (_lockObjectLeaderboards)" here causes the game to freeze so only a try/catch is
         // here to prevent issues.
         try
         {
            if (_leaderboardsGeneric != null && _leaderboardsGeneric.TryGetValue(leaderboardGenericID, out leaderboardGeneric))
            {
               // Do nothing, leaderboardGeneric has now been assigned ok
            }
            else
            {
               Logging.WriteLine("WARNING: LeaderboardManager.GetLeaderboardGeneric couldnt find leaderboard");
               leaderboardGeneric = null;
            }
         }
         catch (Exception e)
         {
            Logging.WriteLine("ERROR: LeaderboardManager.GetLeaderboardGeneric failed, exception: " + e.Message);
            leaderboardGeneric = null;
         }
         return leaderboardGeneric;
      }

      private void GetAvatarImageAsynch(string tag)
      {
         _avatarImageReadStatus = AvatarImageReadStatus.Processing;

         //// Large profile picture 64x64 http://avatar.xboxlive.com/avatar/<GAMERTAG>/avatarpic-l.png <- use this one
         //// These images can be used in game code as textures or graphics.
         //// This example uses an asynchronous HttpWebRequest to create a texture from a gamer's avatar image.

         //string baseURL = "http://avatar.xboxlive.com/avatar/";

         //// For our own, non-submission builds we need to read partner net:
         //// Comment out this line in ALL submission builds:
         //baseURL = "http://avatar.part.xboxlive.com/avatar/";

         //// Retrieve an avatar image Xbox LIVE
         //// Note tags with space will automatically get converted to %20 (eg Dave Smith becomes http://avatar.xboxlive.com/avatar/Dave%20Smith/avatarpic-l.png)
         //string avatarUri = baseURL + tag + "/avatarpic-l.png";

         string avatarUri = AvatarImageURL.GetURL(tag);

         try
         {
            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(avatarUri);
            TrackerObjectHttp trackerObjectHttp = new TrackerObjectHttp(request, tag);
            request.BeginGetResponse(CallbackGetAvatarImage, trackerObjectHttp);
         }
         catch (Exception e)
         {
            _avatarImageReadStatus = AvatarImageReadStatus.Idle;
            //Logging.WriteLine("ERROR: LeaderboardManager.GetAvatarImage failed in request.BeginGetResponse, exception: " + e.Message);
         }
      }

      private void CallbackGetAvatarImage(IAsyncResult result)
      {
         // Whatever happens in this method, exception or success, we regard this item as being processed
         _avatarImageReadStatus = AvatarImageReadStatus.Idle;

         TrackerObjectHttp trackerObjectHttp = result.AsyncState as TrackerObjectHttp;
         if (trackerObjectHttp == null)
         {
            Logging.WriteLine("ERROR: LeaderboardManager.CallbackGetAvatarImage failed, trackerObjectHttp is null");
            return;
         }
         HttpWebRequest request = trackerObjectHttp.Request;
         if (request == null)
         {
            Logging.WriteLine("ERROR: LeaderboardManager.CallbackGetAvatarImage failed, request is null");
            return;
         }
         if (trackerObjectHttp.Tag == null)
         {
            Logging.WriteLine("ERROR: LeaderboardManager.CallbackGetAvatarImage failed, tag is null");
            return;
         }

         // We're modifying our leaderboard lists by adding the images, dont want to clash with leaderboard refreshes or page up/down
         lock (_lockObjectLeaderboards)
         {
            try
            {
               WebResponse response = request.EndGetResponse(result);
               Texture2D avatarPicture = Texture2D.FromStream(GerbilPhysicsGame.GraphicsDevice, response.GetResponseStream());
               if (avatarPicture != null)
               {
                  SetAvatarImage(trackerObjectHttp.Tag, avatarPicture);
               }
               else
               {
                  Logging.WriteLine("ERROR: LeaderboardManager.CallbackGetAvatarImage failed, avatarPicture was null, tag: {0}", trackerObjectHttp.Tag);
               }
            }
            catch (Exception e)
            {
               Logging.WriteLine("ERROR: LeaderboardManager.CallbackGetAvatarImage failed in request.EndGetResponse, tag: {0}, exception: {1}", trackerObjectHttp.Tag, e.Message);
            }
         }
      }

      public override void Initialize()
      {
         base.Initialize();

         _leaderboardsGeneric = new Dictionary<LeaderboardGenericID, LeaderboardGeneric>();
         _avatarImageReadStatus = AvatarImageReadStatus.Idle;

         // Dummy call to the static class to initialise it (it uses Linq to read its config file, can be slow)
         AvatarImageURL.GetURL("Beringela");
      }

      /// <summary>
      /// Refreshes leaderboard from LIVE.  This trashes the existing data and textures and rebuilds the leaderboard
      /// from scratch.
      /// </summary>
      /// <param name="leaderboardGenericID"></param>
      public void InitializeLeaderboard(LeaderboardGenericID leaderboardGenericID)
      {
         // Wipe our Current reference to our leaderboard reader, and the leaderboard being requested
         _leaderboardReaderCurrent = null;
         _leaderboardsGeneric[leaderboardGenericID].WipeLeaderboard();

         // Permissions check
         if (_gamerManager.LivePermissions == LivePermissions.NotPermitted)
         {
            Logging.WriteLine("LOG: LeaderboardManager.InitializeLeaderboard called but permissions are NotPermitted");
            return;
         }

#if NO_LIVE
         // Just some dummy data
         switch (leaderboardGenericID)
         {
            case LeaderboardGenericID.BestTotalScoreForWholeGame:
               LeaderboardGeneric leaderboardGenericScore = _leaderboardsGeneric[LeaderboardGenericID.BestTotalScoreForWholeGame];
               DeleteMe_CreateRandomLeaderboardEntries(leaderboardGenericScore, 8000, 999999);
               OnLeaderboardDataChangedRaise(true, true);
               break;
            case LeaderboardGenericID.MostWipeouts:
               LeaderboardGeneric leaderboardGenericWO = _leaderboardsGeneric[LeaderboardGenericID.MostWipeouts];
               DeleteMe_CreateRandomLeaderboardEntries(leaderboardGenericWO, 1, 40);
               OnLeaderboardDataChangedRaise(true, true);
               break;
            case LeaderboardGenericID.MostGoldChests:
               LeaderboardGeneric leaderboardGenericGolds = _leaderboardsGeneric[LeaderboardGenericID.MostGoldChests];
               DeleteMe_CreateRandomLeaderboardEntries(leaderboardGenericGolds, 1, 60);
               OnLeaderboardDataChangedRaise(true, true);
               break;
         }
#else
         // Only if we have correct access rights and sign in status
         if (!_gamerManager.CheckSignInStatusForLeaderboards("LeaderboardManager.InitializeLeaderboard", false))
         {
            if (_gamerManager.SignedInGamer.IsSignedInToLive == false)
            {
               // This is the situation where we have a signed in gamer, but hey're not signed in to LIVE.  So they have "No Token".
               // It can happen if the LIVE servers are down, or if the player has disabled LIVE access on the phone.
               // It does NOT happen just because the phone loses its mobile signal, as XNA takes care of local caching for us.
               OnLeaderboardInitialReadErroredRaise();
            }
            return;
         }
         _leaderboardIdentityCurrent = GetLeaderboardLiveID(leaderboardGenericID);
         string desc = _leaderboardsGeneric[leaderboardGenericID].LeaderboardDescription;
         Logging.WriteLine("LOG: ASYNCH OUT-> LeaderboardManager is beginning asynch call to get leaderboard {0} (id:{1},{2})", desc, _leaderboardIdentityCurrent.GameMode, _leaderboardIdentityCurrent.Key);

         TrackerObjectLive trackerObject = new TrackerObjectLive(_gamerManager.SignedInGamer, _leaderboardIdentityCurrent, null);
         try
         {
            LeaderboardReader.BeginRead(_leaderboardIdentityCurrent, _gamerManager.SignedInGamer, Dashboard.LeaderboardPageSize, CallbackLeaderboardInitialRead, trackerObject);
         }
         catch (Exception e)
         {
            Logging.WriteLine("ERROR: in LeaderboardReader.InitializeLeaderboard, BeginRead exception: {0}", e.Message);
         }
#endif
      }

      /// <summary>
      /// For the currently initialised leaderboard, get another page's worth of data upwards
      /// </summary>
      public void PageUpLeaderboard()
      {
         PageLeaderboard(true);
      }

      /// <summary>
      /// For the currently initialised leaderboard, get another page's worth of data downwards
      /// </summary>
      public void PageDownLeaderboard()
      {
         PageLeaderboard(false);
      }

      private void PageLeaderboard(bool isPageUp)
      {
#if NO_LIVE
         return;
#else
         // Permissions check
         if (_gamerManager.LivePermissions == LivePermissions.NotPermitted)
         {
            Logging.WriteLine("LOG: LeaderboardManager.PageLeaderboard called but permissions are NotPermitted");
            OnLeaderboardPageErroredRaise(_leaderboardReaderCurrent.CanPageDown, _leaderboardReaderCurrent.CanPageUp);
            return;
         }

         // Sign in check
         if (!_gamerManager.CheckSignInStatusForLeaderboards("LeaderboardManager.PageLeaderboard", false))
         {
            OnLeaderboardPageErroredRaise(_leaderboardReaderCurrent.CanPageDown, _leaderboardReaderCurrent.CanPageUp);
            return;
         }

         // Reader check
         if (_leaderboardReaderCurrent == null)
         {
            Logging.WriteLine("ERROR: LeaderboardManager.PageLeaderboard called but _leaderboardReader is null");
            OnLeaderboardPageErroredRaise(false, false);
            return;
         }

         LeaderboardGenericID leaderboardGenericID = GetLeaderboardGenericID(_leaderboardIdentityCurrent);
         string desc = _leaderboardsGeneric[leaderboardGenericID].LeaderboardDescription;
         TrackerObjectLive trackerObject = new TrackerObjectLive(_gamerManager.SignedInGamer, _leaderboardIdentityCurrent, null);
         trackerObject.IsPageUp = isPageUp;

         if (isPageUp)
         {
            if (_leaderboardReaderCurrent.CanPageUp)
            {
               Logging.WriteLine("LOG: ASYNCH OUT-> LeaderboardManager is beginning a PAGE UP call for leaderboard {0} (id:{1},{2})", desc, _leaderboardIdentityCurrent.GameMode, _leaderboardIdentityCurrent.Key);
               try
               {
                  _leaderboardReaderCurrent.BeginPageUp(CallbackLeaderboardPage, trackerObject);
               }
               catch (Exception e)
               {
                  Logging.WriteLine("ERROR: in LeaderboardReader.PageLeaderboard, BeginPageUp exception: {0}", e.Message);
                  OnLeaderboardPageErroredRaise(_leaderboardReaderCurrent.CanPageDown, _leaderboardReaderCurrent.CanPageUp);
               }
            }
            else
            {
               Logging.WriteLine("LOG: LeaderboardManager received PAGE UP call but cannot fulfill it, either already paging or already at the top, for leaderboard {0} (id:{1},{2})", desc, _leaderboardIdentityCurrent.GameMode, _leaderboardIdentityCurrent.Key);
               OnLeaderboardPageErroredRaise(_leaderboardReaderCurrent.CanPageDown, _leaderboardReaderCurrent.CanPageUp);
            }
         }
         else
         {
            if (_leaderboardReaderCurrent.CanPageDown)
            {
               Logging.WriteLine("LOG: ASYNCH OUT-> LeaderboardManager is beginning a PAGE DOWN call for leaderboard {0} (id:{1},{2})", desc, _leaderboardIdentityCurrent.GameMode, _leaderboardIdentityCurrent.Key);
               try
               {
                  _leaderboardReaderCurrent.BeginPageDown(CallbackLeaderboardPage, trackerObject);
               }
               catch (Exception e)
               {
                  Logging.WriteLine("ERROR: in LeaderboardReader.PageLeaderboard, BeginPageDown exception: {0}", e.Message);
                  OnLeaderboardPageErroredRaise(_leaderboardReaderCurrent.CanPageDown, _leaderboardReaderCurrent.CanPageUp);
               }
            }
            else
            {
               Logging.WriteLine("LOG: LeaderboardManager received PAGE DOWN call but cannot fulfill it, either already paging or already at the bottom, for leaderboard {0} (id:{1},{2})", desc, _leaderboardIdentityCurrent.GameMode, _leaderboardIdentityCurrent.Key);
               OnLeaderboardPageErroredRaise(_leaderboardReaderCurrent.CanPageDown, _leaderboardReaderCurrent.CanPageUp);
            }
         }

#endif
      }

      private void OnSignedIn(object sender, EventArgs args)
      {
         // Nothing to do yet
      }

      public override void LoadContent()
      {
         base.LoadContent();

         //// LIVE testing
         //_timerCollection.Create(20f, false, (timer) => { PageDownLeaderboard(); timer = null; });
         //_timerCollection.Create(30f, false, (timer) => { PageUpLeaderboard(); timer = null; });
         //_timerCollection.Create(30.1f, false, (timer) => { PageUpLeaderboard(); timer = null; }); // this will fail since previous not complete
         //_timerCollection.Create(40f, false, (timer) => { PageUpLeaderboard(); timer = null; });
         //_timerCollection.Create(50f, false, (timer) => { PageUpLeaderboard(); timer = null; });

         GamerManager.OnSignedIn += OnSignedIn;
         OnLeaderboardDataChanged += OnLeaderboardChangedListener;
         OnLeaderboardDataValuesChanged += OnLeaderboardChangedListener;

         _placeholderImages = new List<Texture2D>(3);
         _placeholderImages.Add(_contentManager.Load<Texture2D>(@"Textures\LiveIntegration\gamer1"));
         _placeholderImages.Add(_contentManager.Load<Texture2D>(@"Textures\LiveIntegration\gamer2"));
         _placeholderImages.Add(_contentManager.Load<Texture2D>(@"Textures\LiveIntegration\gamer3"));
         _unknownGamer = _contentManager.Load<Texture2D>(@"Textures\LiveIntegration\gamerUnknownGerbil");

         _gamersNeedingAvatars = new Queue<string>();

         // Prepare generic leaderboard holders
         // Best score
         LeaderboardGeneric leaderboardGenericScore = new LeaderboardGeneric()
         {
            LeaderboardID = LeaderboardGenericID.BestTotalScoreForWholeGame,
            LeaderboardDescription = "Total Best Scores",
            TotalLeaderboardSize = 0,
            EntriesGeneric = new List<LeaderboardEntryGeneric>()
         };
         _leaderboardsGeneric.Add(LeaderboardGenericID.BestTotalScoreForWholeGame, leaderboardGenericScore);

         // Most wipeouts
         LeaderboardGeneric leaderboardGenericWO = new LeaderboardGeneric()
         {
            LeaderboardID = LeaderboardGenericID.MostWipeouts,
            LeaderboardDescription = "Most Wipeouts",
            TotalLeaderboardSize = 0,
            EntriesGeneric = new List<LeaderboardEntryGeneric>()
         };
         _leaderboardsGeneric.Add(LeaderboardGenericID.MostWipeouts, leaderboardGenericWO);

         // Most Gold chests
         LeaderboardGeneric leaderboardGenericGolds = new LeaderboardGeneric()
         {
            LeaderboardID = LeaderboardGenericID.MostGoldChests,
            LeaderboardDescription = "Most Gold Chests",
            TotalLeaderboardSize = 0,
            EntriesGeneric = new List<LeaderboardEntryGeneric>()
         };
         _leaderboardsGeneric.Add(LeaderboardGenericID.MostGoldChests, leaderboardGenericGolds);
      }

      public override void LoadContentDeferred()
      {
         base.LoadContentDeferred();

         _contentLoaded = true;
      }

      public override void Update(float elapsedTime)
      {
         base.Update(elapsedTime);

         if (!_contentLoaded)
            return;

         // Handle our permissions
         if (_gamerManager.LivePermissions == LivePermissions.NotPermitted)
         {
            // Player had refused a TU, so we are not allowed to access live any more and shouldn't display anything
            ClearAllLeaderboards();
         }
         else if (_gamerManager.LivePermissions == LivePermissions.Permitted_ReadWrite)
         {
            // If we haven't already done so, write all scores once if we have permissions
            AwardAllCurrentRatings();
         }

         // Avatar image queue
         if (_gamerManager.LivePermissions == LivePermissions.Permitted_ReadOnly ||
            _gamerManager.LivePermissions == LivePermissions.Permitted_ReadWrite)
         {
            // Only if we're not already processing
            if (_gamersNeedingAvatars.Count > 0 && _avatarImageReadStatus == AvatarImageReadStatus.Idle)
            {
               string tag = _gamersNeedingAvatars.Dequeue();
               Logging.WriteLine("LOG: LeaderboardManager.Update is attempting to retrieve avatar image for tag: " + tag);
               GetAvatarImageAsynch(tag);
            }
         }
      }

      public void AwardAllCurrentRatings()
      {
#if !NO_LIVE
         // If we have permission to write to leaderboards, then we write all scores, but it can only happen once
         // this is because we could have been playing offline for a while and even completed the game, so 
         // must write these scores (since we maybe cannot better these scores, subsequent gameplays might not send anything)
         if (!_runOnceWriteLeaderboardScores)
         {
            // Sign in check (since we do this every tick we suppress the log)
            if (_gamerManager.CheckSignInStatusForLeaderboards("LeaderboardManager.Update", true))
            {
               Logging.WriteLine("LOG: LeaderboardManager is attempting to send best score, gold and wipeouts (one off in case of offline play)");
               if (_screenManager.LevelRepository != null)
               {
                  int score = _screenManager.LevelRepository.GetTotalScore();
                  int wipeouts = _screenManager.LevelRepository.GetTotalWipeouts();
                  int gold = _screenManager.LevelRepository.GetTotalGold();
                  if (score > 0)
                  {
                     AwardRating(Live_Integration.LeaderboardGenericID.BestTotalScoreForWholeGame, score);
                  }
                  if (wipeouts > 0)
                  {
                     AwardRating(Live_Integration.LeaderboardGenericID.MostWipeouts, wipeouts);
                  }
                  if (gold > 0)
                  {
                     AwardRating(Live_Integration.LeaderboardGenericID.MostGoldChests, gold);
                  }
                  _runOnceWriteLeaderboardScores = true;
               }
               else
               {
                  Logging.WriteLine("ERROR: LeaderboardManager failed to send ratings, LevelRepository not yet loaded");
               }
            }
         }
#endif
      }

      /// <summary>
      /// Listen to our own change events, and if data has been refreshed, or data entries changed, then go get their avatars
      /// This method listens to OnLeaderboardDataChanged and OnLeaderboardDataValuesChanged
      /// </summary>
      private void OnLeaderboardChangedListener(object sender, EventArgs e)
      {
         RebuildGamersNeedingAvatarsList();
      }

      private void RebuildGamersNeedingAvatarsList()
      {
         // If any leaderboard changes, we rebuild the list of unknown gamertags
         _gamersNeedingAvatars.Clear();
         foreach (LeaderboardGeneric leaderboard in _leaderboardsGeneric.Values)
         {
            foreach (LeaderboardEntryGeneric leaderboardEntry in leaderboard.EntriesGeneric)
            {
               if (!leaderboardEntry.ImageKnown && leaderboardEntry.GamerTagKnown)
               {
                  string tag = leaderboardEntry.GamerTag;
                  if (!_gamersNeedingAvatars.Contains(tag))
                  {
                     _gamersNeedingAvatars.Enqueue(tag);
                  }
               }
            }
         }
      }

      /// <summary>
      /// For the given gamertag and texture, go through every entry in every leaderboard
      /// and update the avatar texture
      /// </summary>
      /// <param name="tagToUpdate">gamertag whose image we want to update</param>
      /// <param name="texture">avatar image</param>
      private void SetAvatarImage(string tagToUpdate, Texture2D texture)
      {
         Logging.WriteLine("LOG: LeaderboardManager.SetAvatarImage is updating image for tag: " + tagToUpdate);
         bool somethingChanged = false;
         try
         {
            foreach (LeaderboardGeneric leaderboard in _leaderboardsGeneric.Values)
            {
               foreach (LeaderboardEntryGeneric leaderboardEntry in leaderboard.EntriesGeneric)
               {
                  string tagInLeaderboard = leaderboardEntry.GamerTag;
                  if (tagInLeaderboard == tagToUpdate)
                  {
                     leaderboardEntry.Image = texture;
                     leaderboardEntry.ImageKnown = true;
                     somethingChanged = true;
                  }
               }
            }
         }
         catch (Exception e)
         {
            Logging.WriteLine("ERROR: LeaderboardManager.SetAvatarImage failed, exception: {0}", e.Message);
         }

         if (somethingChanged)
         {
            OnLeaderboardDataImagesChangedRaise();
         }
      }

      private void ClearAllLeaderboards()
      {
         // See how many entries we have
         int totalLeaderboardEntries = 0;
         foreach (var leaderboardGenericKeyValuePair in _leaderboardsGeneric)
         {
            totalLeaderboardEntries += leaderboardGenericKeyValuePair.Value.EntriesGeneric.Count;
         }

         // Do we have anything to wipe?
         if (totalLeaderboardEntries > 0)
         {
            // Note we just wipe the leaderboard entries, not the leaderboards themselves, so we can still
            // read their descriptions when logging access requests
            foreach (var leaderboardGenericKeyValuePair in _leaderboardsGeneric)
            {
               leaderboardGenericKeyValuePair.Value.WipeLeaderboard();
            }
            OnLeaderboardDataChangedRaise(false, false);
         }
      }

      /// <summary>
      /// Award a rating to a leaderboard.  The rating can be treated as a score or a time, depending
      /// on the leaderboard.
      /// </summary>
      public void AwardRating(LeaderboardGenericID leaderboardGenericID, int rating)
      {
         string desc = _leaderboardsGeneric[leaderboardGenericID].LeaderboardDescription;
         Logging.WriteLine("LOG: LeaderboardManager received request for rating {0} leaderboard {1}", rating, desc);

         // Permissions check
         switch (_gamerManager.LivePermissions)
         {
            case LivePermissions.NotPermitted:
            case LivePermissions.Permitted_ReadOnly:
               Logging.WriteLine("LOG: LeaderboardManager will not process rating (permissions are not read-write)");
               return;

            case LivePermissions.Permitted_ReadWrite:
               AwardRatingLive(leaderboardGenericID, rating);
               break;

            default:
               throw new Exception("LivePermissions status not known");
         }
      }

      private void AwardRatingLive(LeaderboardGenericID leaderboardGenericID, int rating)
      {
         string desc = _leaderboardsGeneric[leaderboardGenericID].LeaderboardDescription;
#if NO_LIVE
         // If LIVE not enabled, say so, did use popup for this but its too intrusive
         Logging.WriteLine("LOG: (Xbox LIVE disabled) LeaderboardManager would have written rating {0} to leaderboard {1}", rating, desc);
#else
         // Sign in check
         if (!_gamerManager.CheckSignInStatusForLeaderboards("LeaderboardManager.AwardRatingLive", false))
            return;

         // Convert our generic leaderboardGenericID into a platform-specific live LeaderboardIdentity
         LeaderboardIdentity leaderboardID = GetLeaderboardLiveID(leaderboardGenericID);

         // Note - we're not locking this with lock(_lockObjectLeaderboard) as we're not updating anything in our code
         try
         {
            LeaderboardEntry leaderboardEntry = _gamerManager.SignedInGamer.LeaderboardWriter.GetLeaderboard(leaderboardID);

            // DO IT!
            leaderboardEntry.Rating = rating;
            leaderboardEntry.Columns.SetValue("TimeStamp", DateTime.Now);
            Logging.WriteLine("LOG: SYNCHR LeaderboardManager has written rating {0} to leaderboard {1} (id:{2},{3})",
               rating, desc, leaderboardID.GameMode, leaderboardID.Key);
         }
         catch (Exception e)
         {
            Logging.WriteLine("ERROR: LeaderboardManager.AwardRatingLive failed, exception: {0}" + e.Message);
         }
#endif
      }

      /// <summary>
      /// Convert from Generic ID to LIVE ID
      /// </summary>
      private LeaderboardIdentity GetLeaderboardLiveID(LeaderboardGenericID leaderboardGenericID)
      {
         LeaderboardIdentity leaderboardID;
         switch (leaderboardGenericID)
         {
            case LeaderboardGenericID.BestTotalScoreForWholeGame:
               leaderboardID = LeaderboardIdentity.Create(LeaderboardKey.BestScoreLifeTime, (int)LeaderboardLiveIDFromSPAFile.BestTotalScoreLeaderboardLive);
               break;
            case LeaderboardGenericID.MostWipeouts:
               leaderboardID = LeaderboardIdentity.Create(LeaderboardKey.BestScoreLifeTime, (int)LeaderboardLiveIDFromSPAFile.MostWipeoutsLeaderboardLive);
               break;
            case LeaderboardGenericID.MostGoldChests:
               leaderboardID = LeaderboardIdentity.Create(LeaderboardKey.BestScoreLifeTime, (int)LeaderboardLiveIDFromSPAFile.MostGoldChestsLeaderboardLive);
               break;
            default:
               throw new Exception("Unknown leaderboard generic id");
         }
         return leaderboardID;
      }

      /// <summary>
      /// Convert from LIVE ID to Generic ID
      /// </summary>
      private LeaderboardGenericID GetLeaderboardGenericID(LeaderboardIdentity leaderboardIdentity)
      {
         LeaderboardGenericID leaderboardGenericID = LeaderboardGenericID.BestTotalScoreForWholeGame;
         switch (leaderboardIdentity.GameMode)
         {
            case (int)LeaderboardLiveIDFromSPAFile.BestTotalScoreLeaderboardLive:
               leaderboardGenericID = LeaderboardGenericID.BestTotalScoreForWholeGame;
               break;

            case (int)LeaderboardLiveIDFromSPAFile.MostWipeoutsLeaderboardLive:
               leaderboardGenericID = LeaderboardGenericID.MostWipeouts;
               break;

            case (int)LeaderboardLiveIDFromSPAFile.MostGoldChestsLeaderboardLive:
               leaderboardGenericID = LeaderboardGenericID.MostGoldChests;
               break;

            //default:
            //   Logging.WriteLine("ERROR: unknown leaderboard identity");
         }
         return leaderboardGenericID;
      }

      private void DeleteMe_CreateRandomLeaderboardEntries(LeaderboardGeneric leaderboardGeneric, int from, int to)
      {
         leaderboardGeneric.WipeLeaderboard();

         // Create the known entries
         Random rng = new Random();
         for (int i = 0; i < 20; i++)
         {
            long score = (long)(to - (to - from) * (i / 20f));
            DateTime stamp = DateTime.Now + TimeSpan.FromSeconds(rng.Next(60, 3600));

            string tag;
            if (i == 0)
            {
               tag = "WWWWWWWWWWWWWWW";  // max length gamertag
               score = 999999;
            }
            else
            {
               tag = "player" + i.ToString();
            }

            LeaderboardEntryGeneric entry = new LeaderboardEntryGeneric()
            {
               GamerTag = tag,
               Image = _placeholderImages.GetRandomEntry(),
               Rating = score,
            };
            leaderboardGeneric.EntriesGeneric.Add(entry);
         }

         // Create the unknown entries
         for (int i = 0; i < 30; i++)
         {
            LeaderboardEntryGeneric entry = new LeaderboardEntryGeneric()
            {
               GamerTag = "Waiting...",
               Image = _unknownGamer,
               Rating = 0,
            };
            leaderboardGeneric.EntriesGeneric.Add(entry);
         }
      }

      public override void Dispose()
      {
         base.Dispose();
         GamerManager.OnSignedIn -= OnSignedIn;
         OnLeaderboardDataChanged -= OnLeaderboardChangedListener;
         OnLeaderboardDataValuesChanged -= OnLeaderboardChangedListener;
      }

      private enum AvatarImageReadStatus
      {
         Idle,
         Processing,
      }
   }

   /// <summary>
   /// WARNING! This leaderboard enum number has to match what we put in the .SPA config file
   /// Order is therefore important!
   /// </summary>
   public enum LeaderboardLiveIDFromSPAFile
   {
      BestTotalScoreLeaderboardLive = 0,
      MostWipeoutsLeaderboardLive = 1,
      MostGoldChestsLeaderboardLive = 2,
   }
}
