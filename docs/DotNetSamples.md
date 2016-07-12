# .NET Samples

## Sample 1) Pluggable Achievements Handler
The diagram below shows the design used to implemented a pluggable achievments handler for a game.  The diagram reads right to left.

![](PluggableAchievementMgr.png?raw=true)

The cross platform parts monitor game state and if an Award has been detected, they raise this with the Award Manager.  The Award Manager checks to see if this Award has been given before.  If it is a new Award, then a call is made to whatever Achievement Manager has been implemented.  Because each Achievement Manager implements the same [Interface IAchievementManager](https://github.com/KevinSmall/DotNetSamples/blob/master/source/Live%20Integration/IAchievementManager.cs), they are truly pluggable, and implementing a new one (OpenFeint, iOS Game Center) requires no code changes to the rest of the Award objects.

See code in the folders "Live Integration" and "Awards". Two Achievement Manager implementations went live, one for [Xbox LIVE](https://github.com/KevinSmall/DotNetSamples/blob/master/source/Live%20Integration/AchievementManagerXBL.cs) and one for [Android](https://github.com/KevinSmall/DotNetSamples/blob/master/source/Live%20Integration/AchievementManagerLocal.cs).

## Sample 2) Using Lambdas to Define Behaviour
As part of the sample 1 code, each Award has to be defined.  An Award has to contain some logic, eg "make 10 explosions" or "score 100 points in 10 seconds".  This logic can be defined using C Sharp Lambdas, which makes for much cleaner code.  This code is found in the "Awards" folder.  How it works is as follows.

First see the [AwardDefinition](https://github.com/KevinSmall/DotNetSamples/blob/master/source/Awards/AwardDefinition.cs) where we declare a public delegate to hold the Award logic:

```csharp
   /// <summary>
   /// Method signature of a piece of logic that determines if this award should be awarded
   /// Returns true if award should be awarded, false otherwise.
   /// </summary>
   public delegate bool AwardLogic();
```

Then in the AwardDefinition itself, a public variable holds the AwardLogic:

```csharp
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
```

So that's where the logic will be stored.  To actually do the storing look in the [AwardRepository](https://github.com/KevinSmall/DotNetSamples/blob/master/source/Awards/AwardRepository_LoadContent.cs)

As an example, here is an Award being defined, notice the AwardLogic is a lambda with the => operator and contains logic saying "weapons used must equal one and pickups must be more than seven for level called Sink":

```csharp
         //---------------------------------------------------------------------------------------------
         // 14) Einstein - get all pickup items with a single gerbil and a single bomb on level Sink (a mid-gameplay award)
         //---------------------------------------------------------------------------------------------
         _awardDefinitions.Add(new AwardDefinition()
         {
            AchievementName = AchievementNames.AchievementKeyEinstein,
            AwardLogic = () =>
               (_keyFigures[KeyFigureID.WeaponsUsedCount].Count == 1 &&
               _keyFigures[KeyFigureID.PickupsMaxForSingleGerbilCount].Count >= 7 &&
               _keyFigures[KeyFigureID.LevelPlayingName].LevelName == LevelRepository.LevelNameSink),
         });
```

Then to execute this logic during Award detection, see the AwardRepository CheckAwardsAndAwardThem() called by the Update() method.  This bethod, shown below, executes Award detection for EVERY Award record.

```csharp
      /// <summary>
      /// Examine all awards to see if any should be awarded
      /// </summary>
      public void CheckAwardsAndAwardThem()
      {
         foreach (AwardDefinition ad in _awardDefinitions)
         {
            if (!ad.HasBeenAwardedThisSession && ad.AwardLogic())
            {
               _screenManager.AchievementManager.AwardAchievement(ad.AchievementName);
               ad.HasBeenAwardedThisSession = true;
            }
         }
      }
```

The benefit of this design, is that adding a new Award, and having it detected, is simply a case of defining a new Award record, with some logic held in that record.  All other code remains unchanged, in particular the Award detection code remains unchanged.

## Sample 3) Project Design for Multi-Platform Development
A longer version with more explanation is [here](http://www.pencelgames.com/blog/porting-android-solution-and-project-structure).

Below shows how C#, XNA, MonoGame and Xamarin fit togther.

![](XamarinAndMonoGame.png?raw=true)

Below shows how interfaces are used (another view of sample 1)

![](UseOfInterfaces.png?raw=true)
