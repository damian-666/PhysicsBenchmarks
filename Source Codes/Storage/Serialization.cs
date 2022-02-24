using System;
using System.Xml;
using System.Runtime.Serialization;
using System.Reflection;
using System.Diagnostics;
using System.Text;
using System.IO;

using System.IO.IsolatedStorage;



using Core.Trace;

using System.Threading.Tasks;
using static Core.Trace.TimeExec;
using System.Collections.Generic;
using System.Linq;
using System.CodeDom;

namespace Storage
{

    /// <summary>
    /// Handle low level  functionality needed for game state saving and loading, serialization  for clipboard, memory, isolated sandbox , and or disk
    /// this module has no dependencies.. can make a console ap link to it , convert and back from encrypt or not.
    /// Since it has platfrom specific needs it its a linked file to be used by the app level project .
    /// </summary>
    public class Serialization
    {
        /// <summary>
        /// Determine default reading mode on some methods here when loading resource.
        /// Default is false. For Game project this should be set to TRUE. Set this on app Startup.
        /// </summary>
        public static bool UseBinaryXMLFormat = false;
        public const string ErrorMessageAppStorage = "Warning, the system detects you may have application local storage shut off";



        public const string WORLDDATAFILEKEY = "2DWorldData";

        public const string WORLDDATAFILE = WORLDDATAFILEKEY+ ".xml";

        public const string WorkingLevelNameKey = "WorkingLevelName";
        /// <summary>
        /// Check resource name if end in x then its binary resource. Else it's an XML string resource.
        /// </summary>
        /// <param name="resourceName"></param>
        /// <returns>TRUE if binary. FALSE if XML.</returns>
        public static bool IsResourceNameBinary(string resourceName)
        {
            return resourceName.EndsWith("x");
        }




        public static bool IsNet6Folder = false;


        public static string GetMediaPath()
        {

            string path = AppDomain.CurrentDomain.BaseDirectory;

            path = path.Remove(path.LastIndexOf('\\'));
            path = path.Remove(path.LastIndexOf('\\'));


            if (IsNet6Folder)
            {
                path = path.Remove(path.LastIndexOf('\\'));
                path = path.Remove(path.LastIndexOf('\\'));
            }

            path = path + "\\Media";
            return path;
        }



        public static string GetMediaLevelPath()
        {

            string path = GetMediaPath();
            path  += "\\Production";
            return path;

        }



        public static string GetSpiritPath(string spiritFile)
        {
            if (string.IsNullOrEmpty(spiritFile))
                return "";

            string path = GetMediaLevelPath() + "\\Spirits";
            return System.IO.Path.Combine(path, spiritFile);
        }

        // public static T LoadDataFromStream<T>(Stream stream)
        // {
        //     return LoadDataFromStream<T>(stream, UseBinaryXMLFormat);
        // }

        /// <summary>
        /// Load xml-based data from open stream and instantiate it as T object. 
        /// Input stream will be closed by this method.
        /// </summary>
        /// <param name="useBinaryFormat">If true, will treat data stream as .NET Binary XML.</param>
        public static T LoadDataFromStream<T>(Stream stream, bool useBinaryFormat)
        {
            try
            {
                XmlReaderSettings xset = new XmlReaderSettings();

                xset.ConformanceLevel = ConformanceLevel.Auto;

#if SILVERLIGHT
                xset.DtdProcessing = DtdProcessing.Parse;
#endif
         
                DataContractSerializer dcs = new DataContractSerializer(typeof(T));
                T data;

                if (useBinaryFormat)
                {
                    XmlDictionaryReader xr = XmlDictionaryReader.CreateBinaryReader(stream, XmlDictionaryReaderQuotas.Max);
                    data = (T)dcs.ReadObject(xr, true); // ReadObject for XmlDictionaryReader
                }
                else
                {
                    XmlReader xr = XmlReader.Create(stream, xset);
                    data = (T)dcs.ReadObject(xr, true); // ReadObject for XmlReader
                }

                return data;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                return default(T);
            }

            finally
            {
                if (stream != null)
                    stream.Dispose();
            }
        }


        public  static  byte[] LoadThumbnailDataFromFileInfo<T> (FileInfo file, ref byte[] thumbnail)
        {
            return LoadThumbnailDataFromStream<T>(file.OpenRead(), false, ref thumbnail);
        }




        public static byte[] LoadThumbnailDataFromAppResource<T>( string fileKey, ref byte[] thumbnail)
        {


            Tuple<Stream, bool> streamInfo = GetStreamFromAppResourceDll(GetGameAssembyNamepace(), fileKey);
            if (streamInfo.Item1 == null)
            {
                throw new ArgumentNullException("LoadDataFromAppResourceDLL: cannot find xml or binary content " + fileKey);
            }

     
            return LoadThumbnailDataFromStream<T>(streamInfo.Item1, streamInfo.Item2, ref thumbnail);
        }


        public static byte[] LoadThumbnailDataFromStream<T>(Stream stream, bool useBinaryFormat, ref byte[] thumbnail)
        {
            try
            {
                XmlReaderSettings xset = new XmlReaderSettings();

                xset.ConformanceLevel = ConformanceLevel.Auto;


                DataContractSerializer dcs = new DataContractSerializer(typeof(T));

                if (useBinaryFormat)
                {
                    XmlDictionaryReader xr = XmlDictionaryReader.CreateBinaryReader(stream, XmlDictionaryReaderQuotas.Max);
                    return ParseOutThumbnail(xr, ref thumbnail);
                }
                else
                {
                    XmlReader xr = XmlReader.Create(stream, xset);

                    return ParseOutThumbnail(xr, ref  thumbnail);
                }
            }


            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
               
            }
           
            finally
            {
                if (stream != null)
                    stream.Dispose();
            }

            return null;

        }



        private static byte[] ParseOutThumbnail (XmlReader xr, ref byte[] thumbnail)
        {
 
            while (xr.Read())
            {
                XmlNodeType nodetype = xr.NodeType;

                if (nodetype != XmlNodeType.Element && nodetype != XmlNodeType.EndElement)
                    continue;
                else if (xr.Name == "Thumnail")
                {

                    Type type = xr.ValueType;
                    xr.Read();

                    type = xr.ValueType;

                   
                    int buffsize = 200000;
                    if (thumbnail.Length < buffsize)
                        Array.Resize(ref thumbnail, buffsize);//make sure the whole thing loads or it will chunk to the size of array
                                                                //making us call in a loop
                    int chunkRead = -1;
                    int startpos = 0;
                    int totalLength = 0;

                    int chunkLen = thumbnail.Length;

                    while (chunkRead < thumbnail.Length && chunkRead != 0 )
                    {
                        int maxChunk = Math.Min(chunkLen, thumbnail.Length - startpos);

                        chunkRead = xr.ReadContentAsBase64(thumbnail, startpos, maxChunk);
                        if (chunkRead == 0)
                            break;
                   
                        totalLength += chunkRead;
                        startpos = totalLength;

                    }

                    Array.Resize(ref thumbnail, totalLength);
                    return thumbnail;



                }

            }

            return null;

        }

        public static void SaveDataToStream<T>(Stream stream, T data)
        {
            SaveDataToStream<T>(stream, data, UseBinaryXMLFormat);
        }

        /// <summary>
        /// Serialize object T into open stream, write using xml-based format. 
        /// Input stream will be closed by this method.
        /// </summary>
        /// <param name="useBinaryFormat">If true, will write using .NET Binary XML format.</param>
        public static void SaveDataToStream<T>(Stream stream, T data, bool useBinaryFormat)
        {


            // if data to be saved is null, should not proceed, but still inform
            if (data == null)
            {
                throw new ArgumentException("Input Data is null");
            }
            try
            {
                XmlWriterSettings xset = new XmlWriterSettings();
                xset.ConformanceLevel = ConformanceLevel.Auto;
                DataContractSerializer dcs = new DataContractSerializer(typeof(T));
                if (useBinaryFormat)
                {
                    XmlDictionaryWriter xw = XmlDictionaryWriter.CreateBinaryWriter(stream);
                    dcs.WriteObject(xw, data); // WriteObject for XmlDictionaryWriter

                    xw.Flush();
                }
                else
                {
                    XmlWriter xw = XmlWriter.Create(stream, xset);
                    dcs.WriteObject(xw, data); // WriteObject for XmlWriter

                    xw.Flush();
                }

             
            }
            catch (Exception ex)
            {
                Core.Trace.TimeExec.Logger.Trace(" Error in SaveData to stream " + ex.Message);
                throw ex;
            }
            finally
            {
                if (stream != null)
                    stream.Dispose();
            }



        }




        public static T LoadDataFromFileInfo<T>(FileInfo file)
        {
            return LoadDataFromStream<T>(file.OpenRead(), UseBinaryXMLFormat);
        }

        /// <summary>
        /// Helper to load data from file dialog.
        /// Note: For Level, set Level.FileName = FileInfo.Name after this return.
        /// </summary>
        public static T LoadDataFromFileInfo<T>(FileInfo file, bool useBinaryFormat)
        {
            return LoadDataFromStream<T>(file.OpenRead(), useBinaryFormat);
        }

        //TODO MG_GRAPHICS implement in netstandard using special folders

        public static T LoadDataFromIsoStorage<T>(string filename)
        {
            return LoadDataFromIsoStorage<T>(filename, UseBinaryXMLFormat);
        }



        /// <summary>
        /// Load data from isolated storage.
        /// </summary>
        public static T LoadDataFromIsoStorage<T>(string filename, bool useBinaryFormat)
        {
            T data = default(T);
            try
            {

                IsolatedStorageFile storage = IsolatedStorageFile.GetUserStoreForApplication();
                System.IO.Stream stream = new IsolatedStorageFileStream(filename, FileMode.Open, storage);


                data = LoadDataFromStream<T>(stream, useBinaryFormat);
//TODO netstandard iso storage
         //       IsolatedStorage 
            }

            catch( Exception exc)
            {
                Logger.Trace("LoadDataFromIsoStorage" + filename + " " + exc.Message);
            }

            return data;
          
        }





        public static void SaveDataToIsoStorage<T>(string filename, T data)
        {
            SaveDataToIsoStorage<T>(filename, data, UseBinaryXMLFormat);
        }

        /// <summary>
        /// Save data into isolated storage.
        /// </summary>
        public static void SaveDataToIsoStorage<T>(string filename, T data, bool useBinaryFormat)
        {
            IsolatedStorageFile storage = IsolatedStorageFile.GetUserStoreForApplication();
            System.IO.Stream stream = new IsolatedStorageFileStream(filename, FileMode.Create, storage);
            SaveDataToStream<T>(stream, data, useBinaryFormat);



        }

        private static bool useGameAssemby = true;         

        private static Assembly _gameAssembly;

        public static bool UseGameAssemby { get => useGameAssemby; set => useGameAssemby = value; }

        /// <summary>
        /// Assembly where the resources are located
        /// </summary>
        public static  Assembly GetGameAssembly()
        {
            if (!UseGameAssemby)
                return null; ;

            if (_gameAssembly == null)
            {
                string levelAssemblyName = GetGameAssembyName();
                _gameAssembly = AppDomain.CurrentDomain.GetAssemblies().Where(x => x.FullName.Contains(levelAssemblyName)).FirstOrDefault();

                if (_gameAssembly== null)
                {
                    Debug.WriteLine("could not load " + levelAssemblyName);
                }
            }
       
            return _gameAssembly;
        }


        public static Assembly GetPluginsAssembly()
        {
     
          
            var var = GePluginAssembyName();
            var assem= AppDomain.CurrentDomain.GetAssemblies().Where(x => x.FullName.Contains(var)).FirstOrDefault();

                if (var == null)
                {
                    Debug.WriteLine("could not load " + var);
                }
            

            return assem;
        }





   
        public static string GetGameAssembyNamepace()
        {

            return "_" + GetGameAssembyName();
        }


        //Assembly that might have the levelx.wyg files embedded inside, if EmbeddedResource is chosen
        public static string GetGameAssembyName()
        {
            string levelAssembly = "2DWorldCore";
            return levelAssembly;
        }

        //Assembly that might have the levelx.wyg files embedded inside, if EmbeddedResource is chosen
        public static string GePluginAssembyName()
        {
            string var = "Core.Game.MG.Plugins";

            return var;
        }


        /// <summary>
        /// Open stream on resource in .dll  that has the specified assemblyNamespace
        /// This method will search original resource name first, and lower case one.
        /// If still not found, then will try search binary version, with added "x" suffix, for both original and lower case.
        /// </summary>
        /// <param name="isBinary"> TRUE means resource is a binary. FALSE means resource is in XML. </param>
        /// <returns></returns>
        public static Tuple<Stream, bool> GetStreamFromAppResourceDll(string assemNamespace, string resourceName)
        {
            bool isBinary = IsResourceNameBinary(resourceName); // default
            string folder = ".Assets.";

            Assembly assem = GetGameAssembly();

            string pathOriginal = assemNamespace + folder + resourceName;
            Stream stream = assem.GetManifestResourceStream(pathOriginal); 

            if (stream == null)
            {
                string pathLower = assemNamespace + folder + resourceName.ToLower();

                stream = assem.GetManifestResourceStream(pathLower);

                if (stream == null)
                {
                    string pathBinaryOriginal = assemNamespace + folder + resourceName + "x";


                    stream = assem.GetManifestResourceStream(pathBinaryOriginal);

                    if (stream == null)
                    {
                        string pathBinaryLower = assemNamespace + folder + resourceName.ToLower() + "x";
                        stream = assem.GetManifestResourceStream(pathBinaryLower);
                    }

                    isBinary = (stream != null);
                }
            }
            if (stream == null)
            {
                Debug.WriteLine("GetStreamFromAppResourceDll: Resource not found: " + resourceName);
            }

            return new Tuple<Stream, bool>(stream, isBinary);

        }



        public static  Stream GetStreamFromAppResource(string resourceName)
        {
            Tuple<Stream, bool> strmAndIsBinary =  GetStreamInfoFromAppResource(resourceName);
            return strmAndIsBinary.Item1;
        }



    

        /// <summary>
        /// Open stream on app resource file.
        /// </summary>
        public static Tuple<Stream, bool> GetStreamInfoFromAppResource(string resourceName)
        {
            bool isBinary = IsResourceNameBinary(resourceName); // default
       
            string pathOriginal =  resourceName;


            //NOTE binary version deserealize is not  faster exept the google protocolbuffer serializer
            // https://stackoverflow.com/questions/3904494/why-is-binary-serialization-faster-than-xml-serialization


            Stream stream = TryGetStreamFromAppResource(pathOriginal);

            if (stream == null)
            {
                string pathLower =  resourceName.ToLower();


                stream =  TryGetStreamFromAppResource(pathLower);
                if (stream == null)
                {
                    string pathBinaryOriginal = resourceName + "x";
                    stream = TryGetStreamFromAppResource(pathBinaryOriginal);
                     
                    if (stream == null)
                    {
                        string pathBinaryLower = resourceName.ToLower() + "x";
                        stream =TryGetStreamFromAppResource(pathBinaryLower);
                    }

                    isBinary = (stream != null);
                }
            }


            return new Tuple<Stream, bool>(stream, isBinary);
        }
        
        private static Stream TryGetStreamFromAppResource(string resourceName)
        {
            try
            {
                var tuple = GetStreamFromAppResourceDll(GetGameAssembyNamepace(), resourceName);
                return tuple.Item1;
            }

            catch (Exception exc)
            {
                Logger.Trace("GetStreamFromAppResource " + resourceName + " " + exc.Message);
            }
            return null;


 }

        /// <summary>
        /// Load data from app resource embedded in dll, and instantiate it as T object. 
        /// This method should be used when resource file is set as Embedded Resource in silverlight.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="defaultNamespace">NOTE MUST MATH DEFUATL NAMESPACE IN ASSEMBY</param>
        /// <param name="resourceName"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static T LoadDataFromAppResourceDLL<T>(string defaultNamespace, string resourceName)
        {
            Tuple<Stream, bool> streamInfo =  GetStreamFromAppResourceDll(defaultNamespace, resourceName);
            if (streamInfo.Item1 == null)
            {
                throw new ArgumentNullException("LoadDataFromAppResourceDLL: Stream is null");
          //     return default(T);
            }

            return LoadDataFromStream<T>(streamInfo.Item1, streamInfo.Item2);
        }



       public static T LoadDataFromAppResource<T>(string filename)
        {   
             return  LoadDataFromAppResourceDLL<T>(GetGameAssembyNamepace(), filename);
        }





        //TODO try SYNC

        //try canvas.
        //make wind persist
        //time.. hurry..

        //update plugins.
        //check tool patterns wpf latest ( job)

        //funding try..
        //update interations.

        //bitmap write

        //stiffen the joints, must try.. remove the stutter step..or tune..

        //try debug texture visually.. comprae wtih fx other.




        /// <summary>
        /// Get data from open stream as string.
        /// Input stream will be closed by this method.
        /// </summary>
        public static string GetStringFromStream(Stream stream, bool useBinaryFormat)
        {
            string xmlString = "";
            try
            {
                // if in binary format, convert to text xml.
                if (useBinaryFormat)
                {
                    XmlDictionaryReader reader = XmlDictionaryReader.CreateBinaryReader(stream, XmlDictionaryReaderQuotas.Max);
                    StringBuilder sb = new StringBuilder();
                    XmlWriter writer = XmlWriter.Create(sb);
                    writer.WriteNode(reader, false);
                    writer.Flush();
                    xmlString = sb.ToString();
                }
                else
                {
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        xmlString = reader.ReadToEnd();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return "";
            }
            finally
            {
                if (stream != null)
                    stream.Dispose();
            }
            return xmlString;
        }



        /// <summary>
        /// Load data from app resource embedded in dll, and instantiate it as T object. 
        /// This method should be used when resource file is set as Embedded Resource in silverlight.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="defaultNamespace">NOTE MUST MATH DEFUATL NAMESPACE IN ASSEMBY</param>
        /// <param name="resourceName"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static  string LoadResourceAsStringFromDll(string defaultNameSpace, string resourceName)
        {
            Tuple<Stream, bool> streamInfo =  GetStreamFromAppResourceDll(defaultNameSpace, resourceName);

            if (streamInfo.Item1 == null)
            {
                return "";
            }
            return  GetStringFromStream(streamInfo.Item1, streamInfo.Item2);
        }




        /// <summary>
        /// Load data from app resource, but return its content as XML string. 
        ///  Embedded Resource (used on Game), with Dll name set to "2DWorldCore" as default.
        /// </summary>
        public  static string LoadResourceAsString(string resourceName)
        {         
            Tuple<Stream, bool> stream = GetStreamInfoFromAppResource(resourceName);

            if (stream.Item1 == null)
            {
                return  LoadResourceAsStringFromDll(GetGameAssembyName(), resourceName);
            }

            return  GetStringFromStream(stream.Item1, stream.Item2);

        }

            public static string SaveObjectToString<T>(T data)
        {
            try
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                XmlWriterSettings xset = new XmlWriterSettings();
                xset.ConformanceLevel = ConformanceLevel.Auto;
                XmlWriter xw = XmlWriter.Create(sb, xset);
                DataContractSerializer dcs = new DataContractSerializer(typeof(T));
                dcs.WriteObject(xw, data);
                xw.Flush();
                return sb.ToString();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public static T LoadObjectFromString<T>(string xmlstring /*, bool useBinaryFormat*/)
        {
            T instance = default(T);
            try
            {
                if (string.IsNullOrEmpty(xmlstring))
                    return instance;

                DataContractSerializer dcs = new DataContractSerializer(typeof(T));
                StringReader reader = new StringReader(xmlstring);
                XmlReader xr = XmlReader.Create(reader);
                instance = (T)dcs.ReadObject(xr, true);
                return instance;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
            return instance;
        }

        public static void SaveDataToFile<T>(string filename, T obj)
        {
            XmlWriter xw = null;

            try
            {
                XmlWriterSettings xset = new XmlWriterSettings();
                xset.Indent = true;
                xset.NewLineOnAttributes = true;
                xw = XmlWriter.Create(filename, xset);

                DataContractSerializer dcs =
                    new DataContractSerializer(typeof(T));
                dcs.WriteObject(xw, obj);
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                if (xw != null)
                {
                    xw.Flush();
                    xw.Close();
                }
            }
        }







        public static string LoadStringFromFile(string filename)
        {
            string data = "";
            try
            {

                if (!File.Exists(filename))
                    throw new FileNotFoundException(filename);

                using (StreamReader reader = new StreamReader(File.OpenRead(filename)))

                {
                    data =  reader.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
            return data;
        }


        public static  bool SaveStringToFile(string filename, string data)
        {
      
            try
            {

                using (StreamWriter writer = new StreamWriter(File.OpenWrite(filename)))
                {
                    writer.Write(data);
                }
                return true;
                
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                return false;
            }
 
        }

        public static async Task<string> LoadStringFromFileAsync(string filename)
        {
            string data = "";
            try
            {

                if (!File.Exists(filename))
                    throw new FileNotFoundException(filename);

                using (StreamReader reader = new StreamReader(File.OpenRead(filename)))

                {
                    data = await reader.ReadToEndAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
            return data;
        }


        public static async Task<string> LoadStringFromIsoFileAsync(string filename)
        {
            string data = "";
            try
            {

                using (StreamReader reader = new StreamReader(System.IO.IsolatedStorage.IsolatedStorageFile.GetUserStoreForApplication().OpenFile(filename, FileMode.Open)))
                {
                    data = await reader.ReadToEndAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
            return data;
        }

    }

}
