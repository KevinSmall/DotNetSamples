using System;
using System.Collections.Generic;
using System.Text;
using GP.Localisation.Live;
using GP.Storage;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using GP.Audio;
using TimerInterpolator;

namespace GP
{
   public class ToastNotificationSystem : DrawableGameComponent, ILoadContentDeferrable
   {
      // Local refs
      private ScreenManager _screenManager;
      private Camera _camera;

      private SpriteBatch _spriteBatch;
      private SpriteFont _spriteFont;

      /// <summary>
      /// Current timestamp, used to stamp incoming toast requests
      /// </summary>
      private float _timeStamp;

      private bool _contentLoaded = false;

      /// <summary>
      /// Incoming list of toasts to process. We process one at a time until queue empty
      /// </summary>
      private List<ToastNotificationItem> _toastIncomingQueue;

      /// <summary>
      /// Queue gets frozen whilst a toast is being displayed
      /// </summary>
      private QueueStatus _queueStatus;

      /// <summary>
      /// The toast currently being displayed (transitioned on, off).  When this is null we are displaying nothing.
      /// </summary>
      private ToastNotificationItem _activeToast;

      private TimerCollection _timerCollection;
      private float _timer = 0f;
      private Texture2D _backgroundTexture;

      List<string> _wrappedMessageList;
      const float _lineHeight = 40f;
      const float _lineSpacing = 8f;
      const float _fontScale = 1.0f;
      int _rectHeight;
      int _iconOffsetToCenterItVertically;

      public ToastNotificationSystem(Game game)
         : base(game)
      {
         Logging.WriteLine("LOG: ToastNotifier Constructor");

         // Add ourselves as service
         game.Services.AddService(typeof(ToastNotificationSystem), this);

      }

      public override void Initialize()
      {
         // Our local refs
         _screenManager = (ScreenManager)GerbilPhysicsGame.GameServices.GetService(typeof(ScreenManager));
         _camera = (Camera)GerbilPhysicsGame.GameServices.GetService(typeof(Camera));

         _toastIncomingQueue = new List<ToastNotificationItem>(4);

         base.Initialize();
      }

      protected override void LoadContent()
      {
         base.LoadContent();
      }

      public void LoadContentDeferred()
      {
         Logging.WriteLine("LOG: ToastNotifier.LoadContentDeferred");

         _spriteBatch = new SpriteBatch(GraphicsDevice);
         _spriteFont = _screenManager.SpriteFonts.MenuSpriteFont;

         _backgroundTexture = _screenManager.OverlayTextureShort;

         // Timer prep
         _timerCollection = new TimerCollection();

         // TESTING ONLY
         //_screenManager.AchievementManager.DeleteAchievementsSaveDataFile();

         _contentLoaded = true;
      }

      public void CreateToastRequest(string title, string message, Texture2D texture)
      {
         // Add this toast to our queue
         ToastNotificationItem toast = new ToastNotificationItem()
            {
               Title = title,
               Message = message,
               Texture = texture,
               ToastQueueStatus = ToastQueueStatus.Waiting,
            };
         _toastIncomingQueue.Add(toast);
      }

      public override void Update(GameTime gameTime)
      {
         // Mostly, we'll have nothing to do
         if (_activeToast == null && _toastIncomingQueue.Count == 0)
            return;

         if (!_contentLoaded)
            return;

         float elapsedTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
         _timeStamp = (float)gameTime.TotalGameTime.TotalSeconds;

         // Timers, which control appearance of things
         if (_timerCollection != null)
         {
            _timerCollection.Update(elapsedTime);
         }

         //---------------------------------------------------
         // PROCESS - QUEUE
         //---------------------------------------------------
         // Remove queue items that have been kicked off
         _toastIncomingQueue.RemoveAll(item => item.ToastQueueStatus == ToastQueueStatus.Processed);

         if (_toastIncomingQueue.Count > 0 && _queueStatus == QueueStatus.Active)
         {
            // Kick off a new active toast
            _activeToast = _toastIncomingQueue[0];
            _activeToast.ToastDisplayStatus = ToastDisplayStatus.TransitionOn;

            // Do calculation needed for drawing toast
            _wrappedMessageList = _activeToast.Message.PixelPerfectWordWrap(_spriteFont, _fontScale, 460);
            _rectHeight = 36 + (int)((1 + _wrappedMessageList.Count) * (_lineHeight + _lineSpacing)); // 20 is border, the +1 is for title line
            _iconOffsetToCenterItVertically = (int)(0.5f * ((float)_rectHeight - 100f)) - 12;

            // Some sound
            _screenManager.AudioManager.PlaySound(SoundItemID.BlockScoreEffectHigh, 1.0f, 0f, 0f);

            // This means item will get removed from queue, but we keep our _activeToast handle on it
            _activeToast.ToastQueueStatus = ToastQueueStatus.Processed;

            // Freeze queue till this toast is completed
            _queueStatus = QueueStatus.Frozen;
         }

         //---------------------------------------------------
         // PROCESS - ACTIVE TOAST
         //---------------------------------------------------       
         #region process active toast
         if (_activeToast != null)
         {
            switch (_activeToast.ToastDisplayStatus)
            {
               case ToastDisplayStatus.TransitionOn:
                  _timer += elapsedTime;
                  if (_timer > Dashboard.ToastsDurationTransitionOn)
                  {
                     _timer = 0f;
                     _activeToast.ToastDisplayStatus = ToastDisplayStatus.On;

                     // Some sound                     
                     _screenManager.AudioManager.PlaySound(SoundItemID.Wipeout, 0.7f, 0f, 0f);
                  }
                  break;

               case ToastDisplayStatus.On:
                  _timer += elapsedTime;
                  if (_timer > Dashboard.ToastsDurationOn)
                  {
                     _timer = 0f;
                     _activeToast.ToastDisplayStatus = ToastDisplayStatus.TransitionOff;
                  }
                  break;

               case ToastDisplayStatus.TransitionOff:
                  _timer += elapsedTime;
                  if (_timer > Dashboard.ToastsDurationTransitionOff)
                  {
                     _timer = 0f;
                     _activeToast.ToastDisplayStatus = ToastDisplayStatus.Complete;
                  }
                  break;

               case ToastDisplayStatus.Complete:
                  _timer = 0f;
                  _activeToast = null;
                  // This timer waits a bit then sets queue status back to ACTIVE
                  _timerCollection.Create(Dashboard.ToastsDelayBetweenStarting, false, (timer) => { _queueStatus = QueueStatus.Active; timer = null; });
                  break;

               default:
                  break;
            }
         }
         #endregion

         base.Update(gameTime);
      }

      public override void Draw(GameTime gameTime)
      {
         base.Draw(gameTime);

         if (!_contentLoaded)
            return;

         if (_activeToast == null)
            return;

         float transitionRaw = 0f; // always 0..1
         float transitionAdjusted = 0f; // power curved, and 0..1 for on, 1..0 for off

         // In HUD space
         switch (_activeToast.ToastDisplayStatus)
         {
            case ToastDisplayStatus.None:
               break;

            case ToastDisplayStatus.TransitionOn:
               transitionRaw = _timer / Dashboard.ToastsDurationTransitionOn;
               transitionAdjusted = (float)Math.Pow(transitionRaw, 3);
               //Logging.WriteLine("Transition On: {0}", transitionAdjusted);
               break;

            case ToastDisplayStatus.On:
               transitionAdjusted = 1f;
               break;

            case ToastDisplayStatus.TransitionOff:
               transitionRaw = _timer / Dashboard.ToastsDurationTransitionOff;
               transitionAdjusted = 1f - (float)Math.Pow(transitionRaw, 3);
               //Logging.WriteLine("Transition Off: {0}", transitionAdjusted);
               break;

            case ToastDisplayStatus.Complete:
               transitionAdjusted = 0f;
               break;

            default:
               break;
         }

         // Calculate base location coords
         float xCoordBase, yCoordBase;
         xCoordBase = 264f;
         yCoordBase = -50f;

         float yAdjustment = transitionAdjusted * 130;
         yCoordBase += yAdjustment;
         yCoordBase += GerbilPhysicsGame.PushVectorToGetTopCentreMovement.Y;

         _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, null, null, _camera.CameraMatrixHUD);

         Color colWhite = Color.White * transitionAdjusted;
         Color colTitle = Color.Wheat * transitionAdjusted;
         Color colMessage = Color.LightBlue * transitionAdjusted;
         Color colShadow = Color.Black * transitionAdjusted;
         Vector2 shadowOffset = new Vector2(-1, -1);

         // during testing lets fix the location
         //xCoordBase = 264f;
         //yCoordBase = 200f;

         // Background texture
         Rectangle rectTarget = new Rectangle((int)(xCoordBase - 10f), (int)(yCoordBase - 10f), 680, _rectHeight);
         _spriteBatch.Draw(_backgroundTexture, rectTarget, null, colWhite, 0f, Vector2.Zero, SpriteEffects.None, 0f);

         // Toast texture icon
         _spriteBatch.Draw(_activeToast.Texture, new Rectangle((int)xCoordBase - 2 + 7, (int)yCoordBase - 2 + _iconOffsetToCenterItVertically, 105, 105), colShadow);
         _spriteBatch.Draw(_activeToast.Texture, new Rectangle((int)xCoordBase + 7, (int)yCoordBase + _iconOffsetToCenterItVertically, 100, 100), colWhite);

         // Title text
         Vector2 posTitle = new Vector2(xCoordBase + 120f, yCoordBase);
         Vector2 posTitleShadow = posTitle + shadowOffset;
         _spriteBatch.DrawString(_spriteFont, _activeToast.Title, posTitleShadow, colShadow);
         _spriteBatch.DrawString(_spriteFont, _activeToast.Title, posTitle, colTitle);

         // Message text         
         Vector2 lineIncrement = new Vector2(0f, 0f);
         Vector2 posMessageStart = new Vector2(xCoordBase + 122f, yCoordBase + _lineHeight + _lineSpacing);
         Vector2 posMessageShadowStart = posMessageStart + shadowOffset;

         foreach (var line in _wrappedMessageList)
         {
            _spriteBatch.DrawString(_spriteFont, line, posMessageShadowStart + lineIncrement, colShadow);
            _spriteBatch.DrawString(_spriteFont, line, posMessageStart + lineIncrement, colMessage);
            lineIncrement += new Vector2(0f, _lineHeight + _lineSpacing);
         }

         _spriteBatch.End();
      }

      protected override void Dispose(bool disposing)
      {
         base.Dispose(disposing);
      }

      private enum QueueStatus
      {
         Active,
         Frozen
      }
   }
}