using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GP.Live_Integration;
using System.Diagnostics;
using System.Globalization;

namespace GP
{
   /// <summary>
   /// Key Figures are just a dictionary of Key Figure IDs and their KeyFigureValue.
   /// Only one of the KeyFigureValue fields (Count, Timer, LevelName) should be filled for any given Key Figure ID.
   /// The Key Figures themselves are a mixture of 'Global' values like 'number of gold chests awarded' and 'Instance'
   /// values that are just for the current execution of a level.
   /// </summary>
   public sealed class KeyFigures : Dictionary<KeyFigureID, KeyFigureValue>
   {
      public KeyFigures(int capacity)
         : base(capacity)
      {
      }
   }

   [DebuggerDisplay("{Count} {Timer} {LevelName}")]
   public sealed class KeyFigureValue
   {
      /// <summary>
      /// Must contain zero or positive values
      /// </summary>
      public int Count;

      /// <summary>
      /// Must contain zero or positive values
      /// </summary>
      public float Timer;

      public string LevelName;

      public KeyFigureValue()
      {
         Wipe();
      }

      public override string ToString()
      {
         if (LevelName != null && LevelName != string.Empty && LevelName != "")
         {
            return LevelName;
         }
         else if (Timer > 0)
         {
            return Timer.ToString("0.00", CultureInfo.InvariantCulture);
         }
         else if (Count > 0)
         {
            return Count.ToString(CultureInfo.InvariantCulture);
         }
         else
         {
            return "(no value)";
         }
      }

      public void Wipe()
      {
         Count = 0;
         Timer = 0f;
         LevelName = null;
      }
   }

   /// <summary>
   /// Key Figures can be Global (persisting across play sessions and levels) or Instance (applicable just for a single
   /// run of a GameplayScreen) as written in their description.
   /// Instance level KFs must be cleared in method SetAllInstanceKeyFiguresToZero.
   /// </summary>
   public enum KeyFigureID
   {
      /// <summary>
      /// Global - Total wipeouts received across all levels
      /// Persists in level repository, read by Award Repository (done)
      /// </summary>
      WipeoutsCount = 0,

      /// <summary>
      /// Global - Total levels completed across all levels
      /// Persists in level repository, read by Award Repository (done)
      /// </summary>
      LevelsCompletedCount,

      /// <summary>
      /// Instance - Name of the level just successfully completed
      /// Updated by GameplayScreen (done)
      /// </summary>
      LevelCompletedName,

      /// <summary>
      /// Instance - Name of the level currently playing (and maybe not completed yet, or ever)
      /// Updated by GameplayScreen (done)
      /// </summary>
      LevelPlayingName,

      /// <summary>
      /// Instance - Time in seconds of how long it took to complete the level
      /// Updated by GameplayScreen (done)
      /// </summary>
      LevelCompletedTimer,

      /// <summary>
      /// Instance - Count of gerbils we popped out of existence
      /// Updated by ActorList (done)
      /// </summary>
      RedGerbilsPoppedCount,

      /// <summary>
      /// Instance - Count of gerbils we disintegrated
      /// Updated by GameplayScreen (done)
      /// </summary>
      RedGerbilsDisintegratedCount,

      /// <summary>
      /// Instance - Count of top speed (so really a speed, but we store as a counter), maximum value for any one
      /// gerbil, analysed across all gerbils. Speed is measured in world coordinates per second. A gerbil that is high up
      /// and fired up with a bomb can get up to a speed of about 2000 when it comes down.
      /// </summary>
      FlyingMaxSpeedForSingleGerbilCount,

      /// <summary>
      /// Instance - Timer of flying time duration, maximum value for any one gerbil, analysed across all gerbils
      /// </summary>
      FlyingMaxTimeForSingleGerbilTimer,

      /// <summary>
      /// Instance - Count of penguins that have exploded
      /// Updated by Flora (done)
      /// </summary>
      PenguinsExplodedCount,

      /// <summary>
      /// Global - Count of gold chests across all levels
      /// Persists in level repository, read by Award Repository (done)
      /// </summary>
      GoldChestsCount,

      /// <summary>
      /// Instance - Count of all weapons used
      /// Updated by GameplayScreen (done)
      /// </summary>
      WeaponsUsedCount,

      /// <summary>
      /// Instance - Count of bomb weapons used
      /// Updated by GameplayScreen (done)
      /// </summary>
      WeaponsUsedBombCount,

      /// <summary>
      /// Instance - Count of disintegrator weapons used
      /// Updated by GameplayScreen (done)
      /// </summary>
      WeaponsUsedDistintegratorCount,

      /// <summary>
      /// Instance - Count of exploder weapons used
      /// Updated by GameplayScreen (done)
      /// </summary>
      WeaponsUsedExploderCount,

      /// <summary>
      /// Instance - Count of pickups collected, in total, across all gerbils
      /// Updated by GameplayScreen (done)
      /// </summary>
      PickupsCollectedCount,

      /// <summary>
      /// Instance - Count of pickups collected, maximum value for any one gerbil, analysed across all gerbils
      /// </summary>
      PickupsMaxForSingleGerbilCount,

      /// <summary>
      /// Instance - Count of maximum rotations for any single gerbil, so the maximum Rotations value for any 
      /// one gerbil, analysed across all gerbils.
      /// </summary>
      RotationsMaxForSingleGerbilCount,

      /// <summary>
      /// Global - Total score earned across all levels
      /// Persists in level repository, read by Award Repository (done)
      /// </summary>
      TotalScoreCount,

      /// <summary>
      /// DO NOT USE
      /// </summary>
      Maximum
   }
}
