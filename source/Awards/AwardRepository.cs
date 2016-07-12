using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GP.Live_Integration;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using System.Globalization;

namespace GP
{
   /// <summary>
   /// Contains a list of key figures, which are measures of gameplay activity like count of penguins popped or
   /// number of red gerbils triggered. This list of key figures is used by AwardDefinitions to determine if an
   /// award should be awarded.
   /// It is the reponsiblity of the GameplayScreen or the individual Actors to update most Key Figures but some
   /// that span >1 level can only be retrieved here.
   /// </summary>
   public sealed partial class AwardRepository : IDisposable
   {
      private ScreenManager _screenManager;
      private bool _contentLoaded = false;

      private KeyFigures _keyFigures;
      private List<AwardDefinition> _awardDefinitions;
      private float _timerInitialDelay = 6f;
      private float _timerHeartbeat;

      // For debug overlay
      private Vector2 _transpPos;
      private Color _transpCol;
      private Vector2 _topLeftHUDSpace;
      private float _lineIncrementHUDSpace;

      public AwardRepository(ScreenManager screenManager)
      {
         _screenManager = screenManager;
      }

      public void Initialize()
      {
         _keyFigures = new KeyFigures(16);
         _awardDefinitions = new List<AwardDefinition>(16);

         // Debug overlay
         _topLeftHUDSpace = new Vector2(10f, 30f);
         _lineIncrementHUDSpace = 32f;
         _transpPos = new Vector2(_topLeftHUDSpace.X - 5, _topLeftHUDSpace.Y - 5).ToCurrentBackBuffer();
         _transpCol = new Color(0, 0, 0, 142);
      }

      public void Update(float elapsedTime)
      {
         //disable awards
         return;

         //if (!_contentLoaded)
         //   return;

         //// We need an initial delay to not have our dummy achievement notifications sent whilst loading screen is still starting
         //// up, because the notification popup then gets squashed by the real main menu screen appearing
         //if (_timerInitialDelay > 0f)
         //{
         //   _timerInitialDelay -= elapsedTime;
         //   return;
         //}

         //// We check for awards on a regular basis, but not every frame. Critical checks can be forced from outside
         //// by calling the Check* method (for example after a pickup is collected we'd like to check for the Einstein award)
         //if (_timerHeartbeat > 0f)
         //{
         //   _timerHeartbeat -= elapsedTime;
         //   return;
         //}
         //_timerHeartbeat = 2f;

         //// Update key figures that we have to get ourselves (most other data is pushed to us from gameplayscreen or actors)
         //_keyFigures[KeyFigureID.WipeoutsCount].Count = _screenManager.LevelRepository.GetTotalWipeouts();
         //_keyFigures[KeyFigureID.LevelsCompletedCount].Count = _screenManager.LevelRepository.GetTotalComplete();
         //_keyFigures[KeyFigureID.GoldChestsCount].Count = _screenManager.LevelRepository.GetTotalGold();
         //_keyFigures[KeyFigureID.TotalScoreCount].Count = _screenManager.LevelRepository.GetTotalScore();

         //// Award the awards!
         //CheckAwardsAndAwardThem();
      }

      /// <summary>
      /// Examine all awards to see if any should be awarded
      /// </summary>
      public void CheckAwardsAndAwardThem()
      {
         //disable awards
         return;

         //foreach (AwardDefinition ad in _awardDefinitions)
         //{
         //   if (!ad.HasBeenAwardedThisSession && ad.AwardLogic())
         //   {
         //      _screenManager.AchievementManager.AwardAchievement(ad.AchievementName);
         //      ad.HasBeenAwardedThisSession = true;
         //   }
         //}
      }

      /// <summary>
      /// Clear the "award has been awarded this session" flags on all awards, meaning we can re-send
      /// them to the achievement manager.
      /// Intended for cheat/test purposes only.
      /// </summary>
      public void SetAllAwardsAsNotAwardedThisSession()
      {
         foreach (AwardDefinition ad in _awardDefinitions)
         {
            ad.HasBeenAwardedThisSession = false;
         }
         SetAllInstanceKeyFiguresToZero();
      }

      public void Draw(float elapsedTime)
      {
         if (!_contentLoaded)
            return;

         if (!_screenManager.DebugAwardsEnabled)
            return;

         // Draw debug overlay panel with award key figure info
         SpriteBatch sb = _screenManager.SpriteBatch;
         SpriteFont sf = _screenManager.SpriteFonts.FrameRateCounterFont;
         Color color = Color.White;

         sb.Begin();

         // Transparent background
         Rectangle rc = new Rectangle((int)_transpPos.X, (int)_transpPos.Y, 530, 345);
         sb.Draw(_screenManager.BlankTexture, rc, _transpCol);

         // Info
         float x = _topLeftHUDSpace.X;
         float y = _topLeftHUDSpace.Y;
         Vector2 pos = new Vector2(x, y).ToCurrentBackBuffer();

         string strHeader = string.Format("{0,30}   {1,-20}", "Key", "Value ");
         sb.DrawString(sf, strHeader, pos, color);
         y += _lineIncrementHUDSpace;

         foreach (var kf in _keyFigures)
         {
            string strKey = kf.Key.ToString();
            string strValue = kf.Value.ToString();
            string strAll = string.Format("{0,30}   {1,-20}", strKey, strValue);
            pos = new Vector2(x, y).ToCurrentBackBuffer();
            sb.DrawString(sf, strAll, pos, color);
            y += _lineIncrementHUDSpace;
         }

         sb.End();
      }

      public void SetAllInstanceKeyFiguresToZero()
      {
         _keyFigures[KeyFigureID.LevelCompletedName].Wipe();
         _keyFigures[KeyFigureID.LevelPlayingName].Wipe();
         _keyFigures[KeyFigureID.LevelCompletedTimer].Wipe();
         _keyFigures[KeyFigureID.RedGerbilsPoppedCount].Wipe();
         _keyFigures[KeyFigureID.RedGerbilsDisintegratedCount].Wipe();

         _keyFigures[KeyFigureID.FlyingMaxSpeedForSingleGerbilCount].Wipe();
         _keyFigures[KeyFigureID.FlyingMaxTimeForSingleGerbilTimer].Wipe();
         _keyFigures[KeyFigureID.PenguinsExplodedCount].Wipe();
         _keyFigures[KeyFigureID.WeaponsUsedCount].Wipe();
         _keyFigures[KeyFigureID.WeaponsUsedBombCount].Wipe();

         _keyFigures[KeyFigureID.WeaponsUsedDistintegratorCount].Wipe();
         _keyFigures[KeyFigureID.WeaponsUsedExploderCount].Wipe();
         _keyFigures[KeyFigureID.PickupsCollectedCount].Wipe();
         _keyFigures[KeyFigureID.PickupsMaxForSingleGerbilCount].Wipe();
         _keyFigures[KeyFigureID.RotationsMaxForSingleGerbilCount].Wipe();
      }

      private void SetKeyFigureCountAbsolute(KeyFigureID kf, int absoluteValue)
      {
         KeyFigureValue kfv = _keyFigures[kf];
         kfv.Count = absoluteValue;
      }

      public void SetKeyFigureCountKeepMaximum(KeyFigureID kf, int newContenderForMaxCount)
      {
         KeyFigureValue kfv = _keyFigures[kf];
         if (newContenderForMaxCount > kfv.Count)
         {
            kfv.Count = newContenderForMaxCount;
         }
      }

      public void SetKeyFigureCountDelta(KeyFigureID kf, int deltaValue)
      {
         KeyFigureValue kfv = _keyFigures[kf];
         kfv.Count += deltaValue;
      }

      public void SetKeyFigureTimerAbsolute(KeyFigureID kf, float absoluteValue)
      {
         KeyFigureValue kfv = _keyFigures[kf];
         kfv.Timer = absoluteValue;
      }

      public void SetKeyFigureTimerKeepMaximum(KeyFigureID kf, float newContenderForMaxTimer)
      {
         KeyFigureValue kfv = _keyFigures[kf];
         if (newContenderForMaxTimer > kfv.Timer)
         {
            kfv.Timer = newContenderForMaxTimer;
         }
      }

      public void SetKeyFigureLevelName(KeyFigureID kf, string levelName)
      {
         KeyFigureValue kfv = _keyFigures[kf];
         kfv.LevelName = levelName;
      }

      /// <summary>
      /// Close any unmanaged resources, remove subscriptions to static events
      /// </summary>
      public void Dispose()
      {

      }
   }
}
