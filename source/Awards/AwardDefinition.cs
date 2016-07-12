using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GP.Live_Integration;
using System.Diagnostics;

namespace GP
{
   /// <summary>
   /// Method signature of a piece of logic that determines if this award should be awarded
   /// Returns true if award should be awarded, false otherwise.
   /// </summary>
   public delegate bool AwardLogic();
   
   /// <summary>
   /// Platform-independent definition of an in-game award with the gameplay logic to decide if the award
   /// should be awarded.  Also contains a link to the platform-specific implementation of that award.
   /// </summary>
   [DebuggerDisplay("{AchievementName} {HasBeenAwardedThisSession}")]
   public sealed class AwardDefinition
   {
      /// <summary>
      /// Platform-independent award name
      /// </summary>
      public string AwardName;

      /// <summary>
      /// Platform-specific award name
      /// </summary>
      public string AchievementName;
      
      /// <summary>
      /// Logic to determine if award should be awarded, which is done by examining KeyFigures
      /// </summary>
      public AwardLogic AwardLogic;

      /// <summary>
      /// Flag if the award has been awarded in this 'session' of the award repository.  A session
      /// might be a single level execution.
      /// </summary>
      public bool HasBeenAwardedThisSession;
   }
}
