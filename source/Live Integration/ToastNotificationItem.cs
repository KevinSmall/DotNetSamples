using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Graphics;

namespace GP
{
   public class ToastNotificationItem
   {
      public string Title;
      public string Message;
      public Texture2D Texture;
      //public float TimeStampStart;
      //public float Timer;
      
      /// <summary>
      /// This is used whilst toast is waiting in the queue
      /// </summary>
      public ToastQueueStatus ToastQueueStatus;
      
      /// <summary>
      /// This is used whilst toast is being processed (transitioning on, off etc)
      /// </summary>
      public ToastDisplayStatus ToastDisplayStatus;

      public ToastNotificationItem()
      {
      }
   }

   public enum ToastQueueStatus
   {
      Waiting,
      Processed,
   }

   public enum ToastDisplayStatus
   {
      None,
      TransitionOn,
      On,
      TransitionOff,
      Complete
   }
}
