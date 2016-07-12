# .NET Samples

## Sample 1) Pluggable Achievements Handler
The diagram below shows the design used to implemented a pluggable achievments handler for a game.  The diagram reads right to left.

![](PluggableAchievementMgr.png?raw=true)

The cross platform parts monitor game state and if an Award has been detected, they raise this with the Award Manager.  The Award Manager checks to see if this Award has been given before.  If it is a new Award, then a call is made to whatever Achievement Manager has been implemented.  Because each Achievement Manager implements the same Interface IAchievementManager, they are truly pluggable, and implementing a new one (OpenFeint, iOS Game Center) requires no code changes to the rest of the Award objects.

See code in the folders "Live Integration" and "Awards". Two Achievement Manager implementations went live, one for Xbox LIVE (XBL) and one for Android (Local).

## Sample 2) Using Lambdas to Define Behaviour
As part of the sample 1 code, 
Awards handler using lambdas - highlight award definition 
C:\Users\kevin\Documents\Pencel\GerbilPhysicsMobile\GerbilPhysicsMobile\GerbilPhysicsMobile\Game\Awards\AwardDefinition.cs

## Sample 3) Project Design for Multi-Platform Development
A longer version with more explanation is [here](http://www.pencelgames.com/blog/porting-android-solution-and-project-structure).

Below shows how C#, XNA, MonoGame and Xamarin fit togther.

![](XamarinAndMonoGame.png?raw=true)

Below shows how interfaces are used (another view of sample 1)

![](UseOfInterfaces.png?raw=true)

```csharp
private void CreatePoWithItems()
{
  //-------------------------------------------------------------------------
  // Instatiate PO gameobject
  //-------------------------------------------------------------------------
  GameObject gParent = GameObject.FindWithTag("PoBucket");
  Vector3 spawnPosition = _posToCreate[_posCreatedSoFar].Pos; 
  Quaternion spawnRotation = _posToCreate[_posCreatedSoFar].Rot; 
  etc etc
```
