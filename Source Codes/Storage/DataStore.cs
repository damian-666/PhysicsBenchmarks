using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;

namespace Storage
{

 //   [KnownType(typeof(Dictionary<string, object>))]
 /// <summary>
 /// Data about the 2D World shared between the tool and the game.  Examples are the currently file under study for rapid debuging / testing cycles, 
 /// the tool can add notes, optimization progress, tags or whatever since it is a general dictionary keyed by 
 /// 
 /// 
 /// </summary>
    [CollectionDataContract(Name = "DataStore", Namespace = "http://ShadowPlay")]
    public class DataStore: Dictionary<string, object>
    {

        [DataMember]
        private static DataStore instance;

        // Explicit static constructor to tell C# compiler
        // not to mark type as beforefieldinit
        static DataStore()
        {
          
        }


        public static void SaveToDisk()
        {

            try
            {
                FileInfo file = new FileInfo(WorldDataFilePath);
                Stream str = file.OpenWrite();

                Serialization.SaveDataToStream<DataStore>(str, instance);

            }

            catch ( Exception exc)
            {
                Debug.Write("error Saving Datastore" + exc);
            }
        }
        private DataStore() { }

        public static DataStore Instance
        {
            get
            {
                if (instance == null)
                {
                    FileInfo file = new FileInfo(WorldDataFilePath);


                   
                        if (!file.Exists)
                        {
                            Debug.WriteLine("missing datastore,  making new one at " + WorldDataFilePath);
                            instance = new DataStore();
                            return instance;

                        }
                      try
                    {

                        instance = Serialization.LoadDataFromFileInfo<DataStore>(file, false);

                    }

                    catch (Exception exc)
                    {
                        Debug.WriteLine("error loading datastore " + exc);
                        Debug.WriteLine("making new datastore at " + WorldDataFilePath);

                        instance = new DataStore();
                    }

                
                 

                }


                return instance;        
            }
        }


        public new string this[string key]
        {
         set
            {
                if (ContainsKey(key)) 
                {
                    base[key] = value;
                }else
                {
                  Add(key, value);
                }

            }

            get
            {
                if (ContainsKey(key))
                {
                    return base[key] as string;
                }
                else
                {
                    return null;
                }
            }

           
        }


        public static bool LooseFiles = true;

        private static string worlddataPath;
        public static string WorldDataFilePath
        {
            get
            {

                if (worlddataPath == null)
                {
                    //first try our media folder we share with tool, we want to edit last tooled file ref..
                    string runtimeFolder = LooseFiles? Serialization.GetMediaLevelPath(): AppDomain.CurrentDomain.BaseDirectory;             
                    worlddataPath = runtimeFolder + "\\" + Serialization.WORLDDATAFILE;
                    return worlddataPath;

                }
                else
                    return worlddataPath;
            }
        }


    }    
}
