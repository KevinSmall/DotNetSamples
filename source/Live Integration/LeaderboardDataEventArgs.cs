using System;

namespace GP
{
   /// <summary>
   /// Custom args to let us pass page up/down information
   /// </summary>
   public class LeaderboardDataEventArgs : EventArgs
   {
      public bool CanPageUp { get; set; }
      public bool CanPageDown { get; set; }
   }
}
