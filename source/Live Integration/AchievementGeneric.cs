using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.GamerServices;
using System.IO;

namespace GP.Live_Integration
{
   /// <summary>
   /// A platform-independent definition of an achievement.
   /// Describes a single achievement, including the achievement name, description,
   /// picture, and whether it has been achieved by the currently signed in gamer.
   /// 
   /// Points relevant to LIVE implementation:
   /// 1) String limits are: any string 100 chars max, 229 chars total for all strings.
   /// 2) Note the constructor allows this to be created from a LIVE achievement.
   /// </summary>
   public sealed class AchievementGeneric
   {
      /// <summary>
      /// Achievement key. The value must be one of the static strings defined in the AchievementManager and
      /// must match what achievement keys we put in the SPA file.
      /// </summary>
      public string Key;

      /// <summary>
      /// localized achievement name string, for display to the user, also called the friendly name
      /// </summary>
      public string Name;

      /// <summary>
      /// localized achievement description string, displayed AFTER acquiring
      /// </summary>
      public string Description;

      /// <summary>
      /// localized description of the steps necessary to earn the achievement, displayed BEFORE acquiring
      /// </summary>
      public string HowToEarn;

      /// <summary>
      /// Gets the amount of gamer score awarded for earning this achievement.
      /// </summary>
      public int GamerScore;

      /// <summary>
      /// Image associated with this achievement.
      /// </summary>
      public Texture2D Image;

      /// <summary>
      /// whether this achievement should be displayed before it is earned.
      /// For GPM this will always be TRUE, we dont have secret achievements
      /// </summary>
      public bool DisplayBeforeEarned;

      /// <summary>
      /// whether the current player has earned this achievement.
      /// </summary>
      public bool IsEarned;

      /// <summary>
      /// the date at which this achievement was earned.
      /// </summary>
      public DateTime EarnedDateTime;

      /// <summary>
      /// whether this achievement was earned while online.
      /// </summary>
      public bool EarnedOnline;

      public AchievementGeneric()
      {
      }

      /// <summary>
      /// Create the generic achievement from a platform-specific live achievement
      /// This includes converting the live stream image into a Texture2D
      /// </summary>
      public AchievementGeneric(Achievement achievementLive)
      {
         Key = achievementLive.Key;
         Name = achievementLive.Name;
         Description = achievementLive.Description;
         HowToEarn = achievementLive.HowToEarn;
         GamerScore = achievementLive.GamerScore;
         // Special handling for the image
         using (Stream stream = achievementLive.GetPicture())
         {
            Image = Texture2D.FromStream(GerbilPhysicsGame.GraphicsDevice, stream);
         }
         DisplayBeforeEarned = achievementLive.DisplayBeforeEarned;
         IsEarned = achievementLive.IsEarned;
         EarnedDateTime = achievementLive.EarnedDateTime;
         EarnedOnline = achievementLive.EarnedOnline;
      }
   }
}

