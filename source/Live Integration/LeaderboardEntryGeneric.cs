using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.GamerServices;
using System.IO;
using System.Globalization;
using System.Diagnostics;

namespace GP.Live_Integration
{
   /// <summary>
   /// A platform-independent definition of a single row of a leaderboard, holding all the information
   /// a specific gamer has uploaded to the board.
   /// </summary>
   [DebuggerDisplay("{GamerTag} {Rating} {Rank}")]
   public sealed class LeaderboardEntryGeneric
   {
      /// <summary>
      /// The gamer's name
      /// </summary>
      public string GamerTag;

      /// <summary>
      /// Flag if the gamer's GamerTag is a proper gamertag and not a placeholder
      /// </summary>
      public bool GamerTagKnown;

      /// <summary>
      /// The gamer's image
      /// </summary>
      public Texture2D Image;

      /// <summary>
      /// Flag if the gamer's image is a proper image and not a placeholder
      /// </summary>
      public bool ImageKnown;

      /// <summary>
      /// The rating associated with this leaderboard entry, could represent a time or a score
      /// </summary>
      public long Rating;

      /// <summary>
      /// Rank, naturally numbered so first rank is 1
      /// </summary>
      public int Rank;

      public LeaderboardEntryGeneric()
      {
         GamerTag = "Retrieving Entry...";
         GamerTagKnown = false;
         ImageKnown = false;
         Rating = 0;
      }

      /// <summary>
      /// Create the generic leaderboard entry from a platform-specific live leaderboard entry
      /// </summary>
      public LeaderboardEntryGeneric(LeaderboardEntry leaderboardEntryLive)
      {
         GamerTag = leaderboardEntryLive.Gamer.Gamertag;
         GamerTagKnown = true;
         ImageKnown = false;
         Rating = leaderboardEntryLive.Rating;
      }
   }
}

