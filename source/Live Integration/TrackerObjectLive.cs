using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.GamerServices;
namespace GP.Live_Integration
{
   /// <summary>
   /// Tracker object to pass back and forth to asynch calls to the LIVE methods
   /// The GUID is there to guarantee uniqueness, the other fields are needed in some of the callbacks.
   /// </summary>
   public class TrackerObjectLive
   {
      public Gamer Gamer;
      public LeaderboardIdentity LeaderboardIdentity;
      public string AchievementKey;
      public Guid Guid;
      public bool IsPageUp;

      public TrackerObjectLive(Gamer gamer, LeaderboardIdentity? leaderboardIdentity, string achievementKey)
      {
         Gamer = gamer;
         if (leaderboardIdentity.HasValue)
         {
            LeaderboardIdentity = (LeaderboardIdentity)leaderboardIdentity;
         }
         AchievementKey = achievementKey;
         Guid = Guid.NewGuid();
      }
   }
}
