using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.GamerServices;
using System.Net;

namespace GP.Live_Integration
{
   /// <summary>
   /// Tracker object to pass back and forth to asynch calls to the HTTP methods
   /// The GUID is there to guarantee uniqueness, the other fields are needed in some of the callbacks.
   /// </summary>
   public class TrackerObjectHttp
   {
      public HttpWebRequest Request;
      public string Tag;
      public Guid Guid;

      public TrackerObjectHttp(HttpWebRequest request, string tag)
      {
         Request = request;
         Tag = tag;
         Guid = Guid.NewGuid();
      }
   }
}
