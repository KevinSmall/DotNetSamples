using System;
using System.Collections.Generic;
using System.Text;
using GP.Localisation.Live;
using GP.Storage;
using Microsoft.Xna.Framework.GamerServices;  // <-- only LiveManagers should use objects in this namespace
using Microsoft.Xna.Framework.Graphics;

namespace GP.Live_Integration
{
   public interface IAchievementManager
   {
      /// <summary>
      /// Event raised when achievement list changes, eg perhaps an achievement got awarded
      /// </summary>
      event EventHandler<EventArgs> OnAchievementDataChanged;

      /// <summary>
      /// Current status of our live permissions. Ideally, external objects should not need to read this, all
      /// handling of permissions is done inside the live managers.
      /// </summary>
      LivePermissions LivePermissions { get; }

      /// <summary>
      /// Outwardly visible list of achievements, defined in a platform-independent manner
      /// </summary>
      List<AchievementGeneric> AchievementsGeneric { get ; }

      /// <summary>
      /// The summary text for the header of the Achievements screen, ie. "X of Y G, XX of YY Achievements earned."
      /// </summary>
      string SummaryString { get; }

      void Initialize();

      void Dispose();

      void LoadContent();

      void LoadContentDeferred();

      void Update(float elapsedTime);

      void Draw(float elapsedTime);

      /// <summary>
      /// Delete the locally saved achievements file
      /// Used internally plus by the options menu cheats to delete all local data
      /// </summary>
      void DeleteAchievementsSaveDataFile();

      /// <summary>
      /// String name is the achievement key and it must match list of static achievement names in the Achievement
      /// Manager class and must also match the names in the SPA file.
      /// </summary>
      void AwardAchievement(string achievementName);

   }
}
