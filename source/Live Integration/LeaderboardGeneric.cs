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
   /// A platform-independent definition of an leaderboard, made up of leaderboard entries.
   /// </summary>
   public sealed class LeaderboardGeneric
   {
      /// <summary>
      /// Unique ID of the leaderboard
      /// </summary>
      public LeaderboardGenericID LeaderboardID;

      /// <summary>
      /// Localised description of the leaderboard
      /// </summary>
      public string LeaderboardDescription;

      /// <summary>
      /// Total number of entries in the leaderboard
      /// </summary>
      public int TotalLeaderboardSize;

      /// <summary>
      /// List of leaderboard entries
      /// </summary>
      public List<LeaderboardEntryGeneric> EntriesGeneric;

      public void WipeLeaderboard()
      {
         TotalLeaderboardSize = 0;
         // Note if some external object had a handle on one of the EntriesGeneric then it would not be GCd
         EntriesGeneric.Clear();
      }
   }

   /// <summary>
   /// Platform independent leaderboard id
   /// </summary>
   public enum LeaderboardGenericID
   {
      BestTotalScoreForWholeGame,
      MostWipeouts,
      MostGoldChests
   }
}

