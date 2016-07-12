using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GP.Live_Integration;

namespace GP
{
   /// <summary>
   /// Just the content part of the award repository
   /// </summary>
   public sealed partial class AwardRepository : IDisposable
   {
      public AwardRepositoryTombStoneState GetTombStoneState()
      {
         AwardRepositoryTombStoneState artss = new AwardRepositoryTombStoneState();

         artss.ContentLoaded = _contentLoaded;
         artss.LevelCompletedName = _keyFigures[KeyFigureID.LevelCompletedName].LevelName;
         artss.LevelPlayingName = _keyFigures[KeyFigureID.LevelPlayingName].LevelName;
         artss.LevelCompletedTimer = _keyFigures[KeyFigureID.LevelCompletedTimer].Timer;
         artss.RedGerbilsPoppedCount = _keyFigures[KeyFigureID.RedGerbilsPoppedCount].Count;
         artss.RedGerbilsDisintegratedCount = _keyFigures[KeyFigureID.RedGerbilsDisintegratedCount].Count;
         artss.HighSpeedMaxForSingleGerbilCount = _keyFigures[KeyFigureID.FlyingMaxSpeedForSingleGerbilCount].Count;
         artss.FlyingMaxForSingleGerbilTimer = _keyFigures[KeyFigureID.FlyingMaxTimeForSingleGerbilTimer].Timer;
         artss.PenguinsExplodedCount = _keyFigures[KeyFigureID.PenguinsExplodedCount].Count;
         artss.WeaponsUsedCount = _keyFigures[KeyFigureID.WeaponsUsedCount].Count;
         artss.WeaponsUsedBombCount = _keyFigures[KeyFigureID.WeaponsUsedBombCount].Count;
         artss.WeaponsUsedDistintegratorCount = _keyFigures[KeyFigureID.WeaponsUsedDistintegratorCount].Count;
         artss.WeaponsUsedExploderCount = _keyFigures[KeyFigureID.WeaponsUsedExploderCount].Count;
         artss.PickupsCollectedCount = _keyFigures[KeyFigureID.PickupsCollectedCount].Count;
         artss.PickupsMaxForSingleGerbilCount = _keyFigures[KeyFigureID.PickupsMaxForSingleGerbilCount].Count;
         artss.RotationsMaxForSingleGerbilCount = _keyFigures[KeyFigureID.RotationsMaxForSingleGerbilCount].Count;
         return artss;
      }

      public void SetTombStoneState(AwardRepositoryTombStoneState artss)
      {
         _contentLoaded = artss.ContentLoaded;
         SetKeyFigureLevelName(KeyFigureID.LevelCompletedName, artss.LevelCompletedName);
         SetKeyFigureLevelName(KeyFigureID.LevelPlayingName, artss.LevelPlayingName);
         SetKeyFigureTimerAbsolute(KeyFigureID.LevelCompletedTimer, artss.LevelCompletedTimer);
         SetKeyFigureCountAbsolute(KeyFigureID.RedGerbilsPoppedCount, artss.RedGerbilsPoppedCount);
         SetKeyFigureCountAbsolute(KeyFigureID.RedGerbilsDisintegratedCount, artss.RedGerbilsDisintegratedCount);
         SetKeyFigureCountAbsolute(KeyFigureID.FlyingMaxSpeedForSingleGerbilCount, artss.HighSpeedMaxForSingleGerbilCount);
         SetKeyFigureTimerAbsolute(KeyFigureID.FlyingMaxTimeForSingleGerbilTimer, artss.FlyingMaxForSingleGerbilTimer);
         SetKeyFigureCountAbsolute(KeyFigureID.PenguinsExplodedCount, artss.PenguinsExplodedCount);
         SetKeyFigureCountAbsolute(KeyFigureID.WeaponsUsedCount, artss.WeaponsUsedCount);
         SetKeyFigureCountAbsolute(KeyFigureID.WeaponsUsedBombCount, artss.WeaponsUsedBombCount);
         SetKeyFigureCountAbsolute(KeyFigureID.WeaponsUsedDistintegratorCount, artss.WeaponsUsedDistintegratorCount);
         SetKeyFigureCountAbsolute(KeyFigureID.WeaponsUsedExploderCount, artss.WeaponsUsedExploderCount);
         SetKeyFigureCountAbsolute(KeyFigureID.PickupsCollectedCount, artss.PickupsCollectedCount);
         SetKeyFigureCountAbsolute(KeyFigureID.PickupsMaxForSingleGerbilCount, artss.PickupsMaxForSingleGerbilCount);
         SetKeyFigureCountAbsolute(KeyFigureID.RotationsMaxForSingleGerbilCount, artss.RotationsMaxForSingleGerbilCount);
      }

      /// <summary>
      /// Load critical content - that is, before the splash screen has appeared, which we want to keep to a minimum
      /// This is done before any Update() calls
      /// </summary>
      public void LoadContent() { }

      /// <summary>
      /// Load deferred content - that is, only after the splash screen has appeared
      /// This means we will have called Update() a few times before this is called
      /// </summary>
      public void LoadContentDeferred()
      {
         if (_contentLoaded)
            return;

         // Build dictionary of Key Figure measures, initially all blank values
         for (int i = 0; i < (int)KeyFigureID.Maximum; i++)
         {
            _keyFigures.Add((KeyFigureID)i, new KeyFigureValue());
         }

         // Build a list of the award definitions
         //---------------------------------------------------------------------------------------------
         // 1) Wipeout Progress 00 - Collect a certain number of wipeouts
         //---------------------------------------------------------------------------------------------
         _awardDefinitions.Add(new AwardDefinition()
         {
            AchievementName = AchievementNames.AchievementKeyWipeoutProgress00,
            AwardLogic = () =>
               (_keyFigures[KeyFigureID.WipeoutsCount].Count >= 1),
         });
         //---------------------------------------------------------------------------------------------
         // 2) Wipeout Progress 01 - Collect a certain number of wipeouts
         //---------------------------------------------------------------------------------------------
         _awardDefinitions.Add(new AwardDefinition()
         {
            AchievementName = AchievementNames.AchievementKeyWipeoutProgress01,
            AwardLogic = () =>
               (_keyFigures[KeyFigureID.WipeoutsCount].Count >= 8),
         });
         //---------------------------------------------------------------------------------------------
         // 3) Wipeout Progress 02 - Collect a certain number of wipeouts
         //---------------------------------------------------------------------------------------------
         _awardDefinitions.Add(new AwardDefinition()
         {
            AchievementName = AchievementNames.AchievementKeyWipeoutProgress02,
            AwardLogic = () =>
               (_keyFigures[KeyFigureID.WipeoutsCount].Count >= 16),
         });
         //---------------------------------------------------------------------------------------------
         // 4) Game Progress 01 - Complete a certain number of levels
         //---------------------------------------------------------------------------------------------
         _awardDefinitions.Add(new AwardDefinition()
         {
            AchievementName = AchievementNames.AchievementKeyGameProgress01,
            AwardLogic = () =>
               (_keyFigures[KeyFigureID.LevelsCompletedCount].Count >= 36),
         });
         //---------------------------------------------------------------------------------------------
         // 5) Game Progress 02 - Complete a certain number of levels
         //---------------------------------------------------------------------------------------------
         _awardDefinitions.Add(new AwardDefinition()
         {
            AchievementName = AchievementNames.AchievementKeyGameProgress02,
            AwardLogic = () =>
               (_keyFigures[KeyFigureID.LevelsCompletedCount].Count >= 72),
         });
         //---------------------------------------------------------------------------------------------
         // 6) Disintegration Mad - Zap a red alarm gerbil
         //---------------------------------------------------------------------------------------------
         _awardDefinitions.Add(new AwardDefinition()
         {
            AchievementName = AchievementNames.AchievementKeyDisintegrationMad,
            AwardLogic = () =>
               (_keyFigures[KeyFigureID.RedGerbilsDisintegratedCount].Count >= 1),
         });
         #region removed
         ////---------------------------------------------------------------------------------------------
         //// REMOVED
         //// Dont Fear The Gerbil - Pop a certain number of red gerbils by fearlessly launching then into space 
         //// and still need to complete the level?
         ////---------------------------------------------------------------------------------------------
         //_awardDefinitions.Add(new AwardDefinition()
         //{
         //   AchievementName = AchievementManager.AchievementKeyDontFearTheGerbil,
         //   AwardLogic = () =>
         //      (_keyFigures[KeyFigureID.RedGerbilsPoppedCount].Count >= 1 &&
         //       // any level will do, but they must complete it
         //       (_keyFigures[KeyFigureID.LevelCompletedName].LevelName != null &&
         //       _keyFigures[KeyFigureID.LevelCompletedName].LevelName != String.Empty)),
         //});
         ////---------------------------------------------------------------------------------------------
         //// REMOVED
         //// Airborne High Speed (Rocket) - Get a normal gerbil going at huge speeds (a mid-gameplay award)
         ////---------------------------------------------------------------------------------------------
         //_awardDefinitions.Add(new AwardDefinition()
         //{
         //   AchievementName = AchievementManager.AchievementKeyAirborneHighSpeed,
         //   AwardLogic = () =>
         //      (_keyFigures[KeyFigureID.FlyingMaxSpeedForSingleGerbilCount].Count > 4000),
         //});
         #endregion
         //---------------------------------------------------------------------------------------------
         // 7) One Hit Wonder Exploder - Complete a level "Spooky" using only one Exploder
         //---------------------------------------------------------------------------------------------
         _awardDefinitions.Add(new AwardDefinition()
         {
            AchievementName = AchievementNames.AchievementKeyOneHitWonderExploder,
            AwardLogic = () =>
               (_keyFigures[KeyFigureID.LevelCompletedName].LevelName == LevelRepository.LevelNameSpooky &&
               _keyFigures[KeyFigureID.WeaponsUsedExploderCount].Count == 1)
         });
         //---------------------------------------------------------------------------------------------
         // 8) One Hit Wonder Distintegrator - Complete level "Be Decisive" using only one Disintegrator
         //---------------------------------------------------------------------------------------------
         _awardDefinitions.Add(new AwardDefinition()
         {
            AchievementName = AchievementNames.AchievementKeyOneHitWonderDisintegrator,
            AwardLogic = () =>
               (_keyFigures[KeyFigureID.LevelCompletedName].LevelName == LevelRepository.LevelNameBeDecisive &&
               _keyFigures[KeyFigureID.WeaponsUsedDistintegratorCount].Count == 1)
         });
         //---------------------------------------------------------------------------------------------
         // 9) Penguin Lover - Avoid detonating any penguins on a level with lots
         //---------------------------------------------------------------------------------------------
         _awardDefinitions.Add(new AwardDefinition()
         {
            AchievementName = AchievementNames.AchievementKeyPenguinLover,
            AwardLogic = () =>
               (_keyFigures[KeyFigureID.PenguinsExplodedCount].Count == 0 &&
               _keyFigures[KeyFigureID.LevelCompletedName].LevelName == LevelRepository.LevelNameBadNeighbors),
         });
         //---------------------------------------------------------------------------------------------
         // 10) Gold Progress 00 - Get one gold chest
         //---------------------------------------------------------------------------------------------
         _awardDefinitions.Add(new AwardDefinition()
         {
            AchievementName = AchievementNames.AchievementKeyGoldProgress00,
            AwardLogic = () =>
               (_keyFigures[KeyFigureID.GoldChestsCount].Count >= 1),
         });
         //---------------------------------------------------------------------------------------------
         // 11) Gold Progress 01 - Get gold chest on a certain number of levels
         //---------------------------------------------------------------------------------------------
         _awardDefinitions.Add(new AwardDefinition()
         {
            AchievementName = AchievementNames.AchievementKeyGoldProgress01,
            AwardLogic = () =>
               (_keyFigures[KeyFigureID.GoldChestsCount].Count >= 36),
         });
         //---------------------------------------------------------------------------------------------
         // 12) Gold Progress 02 - Get gold chest on a certain number of levels
         //---------------------------------------------------------------------------------------------
         _awardDefinitions.Add(new AwardDefinition()
         {
            AchievementName = AchievementNames.AchievementKeyGoldProgress02,
            AwardLogic = () =>
               (_keyFigures[KeyFigureID.GoldChestsCount].Count >= 72),
         });
         //---------------------------------------------------------------------------------------------
         // 13) Speed Freak 01 - complete a certain level in a very very quick time
         //---------------------------------------------------------------------------------------------
         _awardDefinitions.Add(new AwardDefinition()
         {
            AchievementName = AchievementNames.AchievementKeySpeedFreak01,
            AwardLogic = () =>
               (_keyFigures[KeyFigureID.LevelCompletedTimer].Timer < 32f &&
               _keyFigures[KeyFigureID.LevelCompletedName].LevelName == LevelRepository.LevelNameTwoSeasons),
         });
         #region removed
         ////---------------------------------------------------------------------------------------------
         //// Speed Freak 02 - complete a certain level in a very very quick time
         ////---------------------------------------------------------------------------------------------
         //_awardDefinitions.Add(new AwardDefinition()
         //{
         //   AchievementName = AchievementManager.AchievementKeySpeedFreak02,
         //   AwardLogic = () =>
         //      (_keyFigures[KeyFigureID.LevelCompletedTimer].Timer < 20f &&
         //      _keyFigures[KeyFigureID.LevelCompletedName].LevelName == LevelRepository.LevelNameTension),
         //});
         #endregion
         //---------------------------------------------------------------------------------------------
         // 14) Einstein - get all pickup items with a single gerbil and a single bomb on level Sink (a mid-gameplay award)
         //---------------------------------------------------------------------------------------------
         _awardDefinitions.Add(new AwardDefinition()
         {
            AchievementName = AchievementNames.AchievementKeyEinstein,
            AwardLogic = () =>
               (_keyFigures[KeyFigureID.WeaponsUsedCount].Count == 1 &&
               _keyFigures[KeyFigureID.PickupsMaxForSingleGerbilCount].Count >= 7 &&
               _keyFigures[KeyFigureID.LevelPlayingName].LevelName == LevelRepository.LevelNameSink),
         });
         //---------------------------------------------------------------------------------------------
         // 15) Spin Cycle - Any one gerbil must rotate 12 times
         //---------------------------------------------------------------------------------------------
         _awardDefinitions.Add(new AwardDefinition()
         {
            AchievementName = AchievementNames.AchievementKeySpinCycle,
            AwardLogic = () =>
               (_keyFigures[KeyFigureID.RotationsMaxForSingleGerbilCount].Count >= 12 &&
               _keyFigures[KeyFigureID.LevelPlayingName].LevelName == LevelRepository.LevelNameNewtonsGerbil),
         });
         //---------------------------------------------------------------------------------------------
         // 16) Bomb Party - Drop 10 bombs
         //---------------------------------------------------------------------------------------------
         _awardDefinitions.Add(new AwardDefinition()
         {
            AchievementName = AchievementNames.AchievementKeyBombParty,
            AwardLogic = () =>
               (_keyFigures[KeyFigureID.WeaponsUsedBombCount].Count >= 10),
         });
         //---------------------------------------------------------------------------------------------
         // 17) First Pickup - First Pickup
         //---------------------------------------------------------------------------------------------
         _awardDefinitions.Add(new AwardDefinition()
         {
            AchievementName = AchievementNames.AchievementKeyFirstPickup,
            AwardLogic = () =>
               (_keyFigures[KeyFigureID.PickupsCollectedCount].Count >= 1),
         });
         //---------------------------------------------------------------------------------------------
         // 18) First Alarm Gerbils - Got past first level with alarm gerbils, which is level Collateral
         //---------------------------------------------------------------------------------------------
         _awardDefinitions.Add(new AwardDefinition()
         {
            AchievementName = AchievementNames.AchievementKeyFirstAlarmGerbils,
            AwardLogic = () =>
                (_keyFigures[KeyFigureID.LevelCompletedName].LevelName == LevelRepository.LevelNameCollateral)
         });
         //---------------------------------------------------------------------------------------------
         // 19) Score Progress 00
         //---------------------------------------------------------------------------------------------
         _awardDefinitions.Add(new AwardDefinition()
         {
            AchievementName = AchievementNames.AchievementKeyScoreProgress00,
            AwardLogic = () =>
               (_keyFigures[KeyFigureID.TotalScoreCount].Count >= 20000),
         });
         //---------------------------------------------------------------------------------------------
         // 20) Score Progress 01
         //---------------------------------------------------------------------------------------------
         _awardDefinitions.Add(new AwardDefinition()
         {
            AchievementName = AchievementNames.AchievementKeyScoreProgress01,
            AwardLogic = () =>
               (_keyFigures[KeyFigureID.TotalScoreCount].Count >= 40000),
         });

         _contentLoaded = true;
      }
   }
}
