using System;
using System.Collections.Generic;
using System.Linq;
#if WINDOWS || WINDOWS_PHONE || ANDROID
using System.Xml.Linq;
#endif
using System.Text;

namespace GP.Live_Integration
{
   /// <summary>
   /// For a given gamer tag, can read and return the URL to its images.
   /// Reads the file XboxLIVESettings.xml to decide the URL to use. 
   /// </summary>
   public static class AvatarImageURL
   {
      private static string _imageURLFromConfigFile = null;

      static AvatarImageURL()
      {
         if (_imageURLFromConfigFile == null)
         {
            ReadConfigFile();
         }

         if (_imageURLFromConfigFile == null)
         {
            Logging.WriteLine("ERROR: Cannot read XboxLIVESettings.xml file");
         }
      }

      /// <summary>
      /// Coding from Environment_Specific_Settings_in_WP7_Titles document
      /// </summary>
      private static void ReadConfigFile()
      {
#if ANDROID
			_imageURLFromConfigFile = "http://avatar.part.xboxlive.com/avatar/{GAMERTAG}/avatarpic-l.png";
#elif WINDOWS
         return;
#else
         Dictionary<string, string> XboxLIVESettings = new Dictionary<string, string>();

         XElement root = XElement.Load("XboxLIVESettings.xml");

         var settingList =
           from element in root.Descendants("Item")
           select new KeyValuePair<string, string>(element.Element("Key").Value, element.Element("Value").Value);

         foreach (var v in settingList)
         {
            XboxLIVESettings.Add(v.Key, v.Value);
         }

         //string avatarBody = XboxLIVESettings["2DAvatarBodyURL"];
         _imageURLFromConfigFile = XboxLIVESettings["2DAvatar64URL"];
#endif

         Logging.WriteLine("LOG: AvatarImageURL.ReadConfigFile() is using path: {0}", _imageURLFromConfigFile);         
      }

      public static string GetURL(string gamerTag)
      {
         if (_imageURLFromConfigFile == null)
         {
            Logging.WriteLine("ERROR: GetURL failed because _imageURLFromConfigFile is null");
            return null;
         }

         string avatarURL = _imageURLFromConfigFile.Replace("{GAMERTAG}", gamerTag);
         return avatarURL;
      }
   }
}
