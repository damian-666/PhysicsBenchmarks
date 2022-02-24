using System;
using System.Diagnostics;
using System.IO;

namespace Storage
{

   public class LevelKey
   {
      public static string LevelLockStateFilename = "LevelLockState";
      public static string LastPlayedLevelID_XFileName = "LastPlayedLevelID_X";
      public static string LastPlayedLevelID_YFileName = "LastPlayedLevelID_Y";
      public static string TravelerHeldBodiesFileName = "TravelerHeldBodies";
      static public int Version; //prepend keys for this..  changes on major shift to levels, invalidates saved levels or positions.


      public static bool HasSavedPos(int levelIDx, int levelIDy)
      {
         //don't worry about multiple users  as in levelLockState, user storage for app.  its usually for  one sessio anyways, one PC , one user.  
         string posKey = GenerateKeyStringFromLevelID(levelIDx, levelIDy);
      
        //TODO MG_GRAPHICS    return Windows.Storage.ApplicationData.Current.LocalSettings.Values.ContainsKey(posKey);
            return false;
      }

      const char sep = ':';

      public static bool SavePositionInLevel(int levelIDx, int levelIDy, float X, float Y, float Energy)
      { //TODO MG_GRAPHICS 
/*
         Windows.Storage.ApplicationDataContainer iso = Windows.Storage.ApplicationData.Current.LocalSettings;
         string posKey = GenerateKeyStringFromLevelID(levelIDx, levelIDy);
         if ( iso.Values.ContainsKey(posKey) )
         {
            iso.Values.Remove(posKey);
         }
         string value = X.ToString() + sep + Y.ToString() + sep + Energy.ToString();
         iso.Values.Add(posKey, value);
*/
         return true;
      }

      public static bool LoadSavedPos(int levelIDx, int levelIDy, out float X, out float Y, out float Energy)
      {

          
         X = 0;
         Y = 0;
         Energy = 0;  //TODO MG_GRAPHICS
            /**
         string posKey = GenerateKeyStringFromLevelID(levelIDx, levelIDy);


         var path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
          
            
            //File.WriteAllText(Path.Combine(path, fileName), JsonConvert.SerializeObject(obj));

          //  if ( !Windows.Storage.ApplicationData.Current.LocalSettings.Values.ContainsKey(posKey) )


         //   return false;
      //   object posInfo;

       // Windows.Storage.ApplicationData.Current.LocalSettings.Values.TryGetValue(posKey,  out posInfo);
        string position = posInfo as string;
             
         string[] characterState = position.Split(sep);
         X = float.Parse(characterState[0]);
         Y = float.Parse(characterState[1]);
         Energy = float.Parse(characterState[2]);
            */
         return true;
      }

      public static string GenerateKeyStringFromLevelID(int levelIDx, int levelIDy)
      {
         // the key go across updates ( minor) 0 + l-e-v@l+ levelnumber.tostring + leveldepth..
         string key = Version.ToString() + "l-e-v@l" + levelIDx.ToString() + levelIDy.ToString(); // made less hackable

         return key;
      }

      // get list of unlocked level key from isostorage. this is called by level selector on startup.
      public static FarseerPhysics.Common.HashSet<string> GetUnlockedKeyList()
      {
         FarseerPhysics.Common.HashSet<string> levelLockState = null;

         try
         {
             //TODO MG_GRAPHICS
                //  Serialization.LoadDataFromFileInfo()
                //   var path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);


                //     levelLockState = Serialization.LoadDataFromIsoStorage<FarseerPhysics.Common.HashSet<string>>(LevelLockStateFilename);


            }
            catch ( Exception exc )
         {
            Debug.WriteLine("Exception in LevelKey.GetUnlockedKeyList(): " + exc.Message);
         }
         // if no list found on isostorage, just return empty list
         if ( levelLockState == null )
            levelLockState = new FarseerPhysics.Common.HashSet<string>();
         return levelLockState;
      }
        /*  //TODO MG_GRAPHICS 
      public static async void UnlockAllLevels()
      {
         // todo: these values should be set by external module later, or as parameter. 
         try
         {
            int maxLevelIDx = 10;
            int maxLevelIDy = 2;
            // level keys are just a simple list in isostorage.
            FarseerPhysics.Common.HashSet<string> levelLockState = new FarseerPhysics.Common.HashSet<string>();
            for ( int x = 1; x <= maxLevelIDx; x++ )
            {
               for ( int y = 1; y <= maxLevelIDy; y++ )
               {
                  string key = GenerateKeyStringFromLevelID(x, y);
                  levelLockState.Add(key);
               }
            }
             Serialization.SaveDataToIsoStorage<FarseerPhysics.Common.HashSet<string>>(LevelLockStateFilename, levelLockState);
         }
         catch ( Exception exc )
         {
            //if storage is not enabled..
            System.Diagnostics.Debug.WriteLine(exc.ToString());
            await (new Windows.UI.Popups.MessageDialog(Serialization.ErrorMessageAppStorage, "Kontrol Warning")).ShowAsync();
         // just let them play
         }
      }

      public static  void RelockAllLevel()
      {
         // just save empty list into isostorage
         FarseerPhysics.Common.HashSet<string> levelLockState = new FarseerPhysics.Common.HashSet<string>();
         Serialization.SaveDataToIsoStorage<FarseerPhysics.Common.HashSet<string>>(LevelLockStateFilename, levelLockState);
      }

      public static  void UnlockLevel(int levelIDx, int levelIDy)
      {
         // read from iso file, add to collection, and rewrite back to iso file
         FarseerPhysics.Common.HashSet<string> levelLockState = null;
         try
         {
            levelLockState = Serialization.LoadDataFromIsoStorage<FarseerPhysics.Common.HashSet<string>>(LevelLockStateFilename);
            if ( levelLockState == null )
               levelLockState = new FarseerPhysics.Common.HashSet<string>();
            string key = GenerateKeyStringFromLevelID(levelIDx, levelIDy);
            if ( levelLockState.Contains(key) == false )
            {
               levelLockState.Add(key);
                Serialization.SaveDataToIsoStorage<FarseerPhysics.Common.HashSet<string>>(LevelLockStateFilename, levelLockState);
            }
            // this LastPlayedLevelID is easier for testing.
             Serialization.SaveDataToIsoStorage<int>(LastPlayedLevelID_XFileName, levelIDx);
             Serialization.SaveDataToIsoStorage<int>(LastPlayedLevelID_YFileName, levelIDy);
         }
         catch ( Exception exc )
         {
            Debug.WriteLine("Exception in LevelKey.UnlockLevel(): " + exc.Message);
         }
      }

      // using this game can continue from last played level. easier to test.
      public static void GetLastPlayedLevel(out int levelIDx, out int levelIDy)
      {
         try
         {
            levelIDx = Serialization.LoadDataFromIsoStorage<int>(LastPlayedLevelID_XFileName);
            levelIDy = Serialization.LoadDataFromIsoStorage<int>(LastPlayedLevelID_YFileName);
         }
         catch ( Exception )
         {
            // on error or savedata not yet available, just return first level ?
            levelIDx = 1;
            levelIDy = 1;
         }
      }

        */

   /*
   public static void RecordTravelersItemToIso(List<Body> heldBodies, int levelIDx, int levelIDy)
   {
   if (heldBodies == null || heldBodies.Count <= 0)
   return;
   BodiesCarriedInLevel data = new BodiesCarriedInLevel();
   data.Bodies = heldBodies;
   data.levelIDx = levelIDx;
   data.levelIDy = levelIDy;
   // save held bodies into isolated storage
   string key = GenerateKeyStringFromLevelID(levelIDx, levelIDy);  //for each level.
   //for now we just want ot see if i can kick a sword down to 1b.. or carry one.. and have it there on reload level.
   //FUTURE  ... I dont think we even will use this feature, if we have this:  
   //better to have a "Save Game" " Load Game" button.   you have to remeber to Save Game wehn you got the 2 swords.
   //this will let people try stunts also, repeat jumps from certain place.. practice stuff
   Serialization.SaveDataToIsoStorage<BodiesCarriedInLevel>(TravelerHeldBodiesFileName + key, data);
   }
   /// <summary>
   /// This method only return held bodies if requested level id matches stored level id.
   /// </summary>
   public static List<Body> GetTravelersItemFromIso(int levelIDx, int levelIDy)
   {
   try
   {
   BodiesCarriedInLevel data = Serialization.LoadDataFromIsoStorage<BodiesCarriedInLevel>(TravelerHeldBodiesFileName);
   if (data.levelIDx == levelIDx && data.levelIDy == levelIDy)
   return data.Bodies;
   //else
   //    return new List<Body>();
   }
   catch (Exception exc)
   {
   //TODO FUTURE code review.. not sure if exception should be used  for normal occurance like item  missing, just check return value.
   //definite not in inner loops, its slow according in Ian Qvist
   // return empty list when held bodies not available on isolated storage 
   //return new List<Body>();
   Debug.WriteLine("Exception in LevelKey.GetTravelersItemFromIso(): " + exc.Message);
   }
   return new List<Body>();
   }   */
    }

    /*
    /// <summary>
    /// Simple data structure to store traveler held bodies with its level id.  //TODO .. if if kick a sword to level 2.. that should count as Carring  it there i think.
    /// </summary>
    [DataContract(Name = "BodiesCarriedInLevel", Namespace = "http://ShadowPlay")]
    public struct BodiesCarriedInLevel
    {
    [DataMember]
    public List<Body> Bodies;
    [DataMember]
    public int levelIDx;
    [DataMember]
    public int levelIDy;
    }*/
}