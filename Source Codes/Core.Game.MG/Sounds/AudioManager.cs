using System;

using System.Net;
using System.Windows;

using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Diagnostics;
using System.ComponentModel;


using System.Linq;


using UndoRedoFramework;

using System.Threading.Tasks;
using Storage;

using Path = System.IO.Path;
using FarseerPhysics.Common;
//using Storage;


#if SILVERLIGHT || WINDOWS_PHONE || MONOGAME
using Microsoft.Xna.Framework.Audio;
#endif


#if SILVERLIGHT
using System.Media
#endif

namespace Core.Game.MG
{


    //NOTE todo.  this will use monogame for all platforms  including uwp and wpf, not windows. 
    //for silverlight it has a branched copy in Core.Game.Win, it migth never be compiled and updated again but its still there.
    //some mistakes were made early in thie effort that might have broken silverlight buid.


    /// <summary>
    /// Wrapper for all sound effects, in Tool and silverlight.   
    /// for silverlight and Monogame, sound effects such as pitch change and short sounds are implemented, not in WPF 
    /// (TODO UWP PORT, use Monogame sounds in tool)
    /// </summary>
    public class AudioManager : INotifyPropertyChanged   // TODO Clean use the NotifyPropertyBase to eliminate repeat prop names on notify.
    {

        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private const string errorVolumeRange = "min vol is 0, max volume is 1";
        public bool _isSoundOn = true;


        private static bool looseFiles = false;

        public static bool LooseFiles { get => looseFiles; set => looseFiles = value; }



        /// <summary>
        /// Toggles sound state
        /// If IsSoundOn is false, sound is mute, system won't do any sound processing
        /// </summary>
        public bool IsSoundOn
        {
            get
            {
                return _isSoundOn;
            }
            set
            {
                if (_isSoundOn != value)
                {
                    _isSoundOn = value;
                    NotifyPropertyChanged("IsSoundOn");
                }
            }
        }


        public float MasterVolume { get; set; }

        private static AudioManager _instance = new AudioManager();
        public static AudioManager Instance { get { return _instance; } }

        public string BackGroundMusicFile { get; set; }

        // Lazy-loading "cache" of sounds. Filenames are unique within a path.


        /// <summary>
        /// Effect to play wav files, no support for low latency mp3 sound effect, tried it by creating prototype by loading mp3 using sound effect, failed.
        /// </summary>
        private Dictionary<string, SoundEffect> _wavSoundEffects = new Dictionary<string, SoundEffect>();
        public Dictionary<string, SoundEffect> WavSoundEffects
        {
            get { return _wavSoundEffects; }
        }

        /// <summary>
        /// This will handle pitch and pan of the currently playing sound effect
        /// </summary>
        private Dictionary<string, SoundEffectInstance> _wavSoundEffectInstances = new Dictionary<string, SoundEffectInstance>();
        public Dictionary<string, SoundEffectInstance> WavSoundEffectInstances
        {
            get { return _wavSoundEffectInstances; }
        }





#if mp3
        private Dictionary<string, float> _mp3SoundVolumes = new Dictionary<string, float>();  //since there is no master volume, retain this to implement ours.

#endif



        public Dictionary<string, Stream> Streams { get; } = new Dictionary<string, Stream>();

        private AudioManager()
        {
            MasterVolume = 1;
        }




        private SoundEffectInstance FindOrCreateSoundEffectInstance(string filename)
        {
            return FindOrCreateSoundEffectInstance(filename, "");
        }

        /// <summary>
        /// Find a sound effect instance from a loaded sound effect, if none is found, the create a new instance
        /// allows two or more volume, pitch, or pan varied sounds to be played simultaneously
        /// </summary>
        /// <param name="filename">the sound effect file name</param>
        /// <returns>Sound effect instance</returns>
        private SoundEffectInstance FindOrCreateSoundEffectInstance(string filename, string variation)
        {

            if (!IsSoundOn)
                return null;

            string soundKey = GetKeyName(filename);


            string variantKey = soundKey + variation;
            SoundEffectInstance effectInstance;

            try
            {
                if (_wavSoundEffects.ContainsKey(soundKey) == false)
                {
                    if (!LoadSound(filename))
                        return null;
                }

                if (_wavSoundEffectInstances.ContainsKey(variantKey))
                {
                    effectInstance = _wavSoundEffectInstances[variantKey];
                }
                else
                {
                    effectInstance = _wavSoundEffects[soundKey].CreateInstance();
                    _wavSoundEffectInstances.Add(variantKey, effectInstance);
                }

                return effectInstance;

            }

            catch (Exception exc)
            {
                Debug.WriteLine("error in FindOrCreateSoundEffectInstance" + variantKey + "\n " + exc.Message);

            }

            return null;
        }




        public string GetMediaSoundFolder()
        {        
            string path = Storage.Serialization.GetMediaPath();
            path = path + "\\Sounds";
            return path;
        }

        public string GetEmbeddedMediaSoundFolder()
        {
            return "Assets.Sounds";
        }



        /// <summary>
        /// PreLoad all the sounds in a  folder.
        /// </summary>
        /// <param name="directory">The path of the directory, eg. Content/Sounds</param>
        public void LoadSounds(string directory)
        {


            if (!looseFiles)
            {
                LoadSounds(directory, Serialization.GetGameAssembly());
                return;
            }


            var filePaths = Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories)
              .Where(s =>
#if (MP3)
             s.ToLower().EndsWith(".mp3")
            
            ||
#endif
            s.EndsWith(".wav")

            );


            foreach (string soundFile in filePaths)
            {
                LoadSound(soundFile, null, false);
            }

        }

        public void PreLoadSounds(string subfolder = "")
        {
            if (IsSoundOn)
            {
                LoadSounds(subfolder);
            }
        }





        public void LoadSounds(string subFolder, Assembly assembly)
        {

            try
            {


                string cleanDirectory = EmbeddedResourceHelper.CheckAndSanitizeEmbeddedPath(subFolder);
                string[] resources = assembly.GetManifestResourceNames();

                IList<string> soundFiles = new List<string>();

                foreach (string resource in resources)
                {
                    if (resource.EndsWith("wav"))
                    //  if (resource.StartsWith(cleanDirectory))
                    {
                        soundFiles.Add(resource);
                    }
                }

                foreach (string soundFile in soundFiles)
                {
                    LoadSound(soundFile, assembly, false);
                }

            }
            catch (Exception exc)
            {
                //if storage is not enabled..
                System.Diagnostics.Debug.WriteLine(exc.ToString());
                // just let them play
            }
        }


        public bool LoadSound(string fileName)
        {
            // Wrap and send in the assembly of the application who's calling us. Otherwise,
            // GetCallingAssembly will give us the Tools assembly.

            return LoadSound(fileName, Serialization.GetGameAssembly());  //uwp_todo
        }




        public bool IsPlaying(string filename)
        {

            if (!IsSoundOn)
                return false;

            //TODO this cant be called from physics thread, as in Eat sound.
            //either derive a class from MediaElement implement  _isPlaying
            // of move to sound effect..  Eat sound delay is too slow anyways..

            if (filename == null)
                return false;

            string fileNameKey = GetKeyName(filename);


            if (_wavSoundEffectInstances.ContainsKey(fileNameKey))
            {
                //TODO this is now keyed by variants..  should do its play effect..parms..
                return _wavSoundEffectInstances[fileNameKey].State == SoundState.Playing;
            }
            else
            {
                return false;
            }

        }


        /// <summary>
        /// preload the sound to be played later, set the  #channels and latency
        /// Takes an assembly because is not often called from the executing assembly.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="callingAssembly"></param>

        private bool LoadSound(string path, Assembly callingAssembly, bool addPath = true)
        {

            //      TODO if callingAssemby is null do loose files, for wpf.. this is shared module

            string filename = path;   //.Replace(EmbeddedResourceHelper.ProjectNamespace + ".", "").ToLower();


            string fullFileName = path;



            Debug.Assert(looseFiles == true || callingAssembly != null);
            

            bool fileFound = false;
            if (looseFiles)
            {

                if (addPath)
                {
                    fullFileName = EmbeddedResourceHelper.CheckAndSanitizePath(path);
                }

                if (File.Exists(fullFileName))
                    fileFound = true;

            }



            if (callingAssembly != null)
            {
                if (!fileFound && addPath)
                {

                    fullFileName = EmbeddedResourceHelper.CheckAndSanitizeEmbeddedPath(path);

                }
            }




            Stream stream = null;

#if MP3

//TODO try MOnoo

            if (filename.EndsWith(".mp3"))
            {
                MediaElement mp3Element;

                if (_mp3SoundEffects.ContainsKey(filename))
                {
                    mp3Element = _mp3SoundEffects[filename];
                }
                else
                {
                    mp3Element = new MediaElement();
                    _mp3SoundEffects.Add(filename, mp3Element);
                }

                if (Graphics.Instance.RootCanvas.Children.Contains(mp3Element) == false)
                {
                    Graphics.Instance.RootCanvas.Children.Add(mp3Element);
                }


                if (stream != null)
                {
                    if (_streams.ContainsKey(filename) == false)
                    {
                        _streams.Add(filename, stream);
                    }
                }
                else
                {
                    throw new NullReferenceException("Couldn't open " + path + ". Make sure the file exists in that directory, and has Build Action set to Embedded Resource.");
                }
            }

            else
#endif
            if (filename.EndsWith(".wav"))
            {

                if (Streams.ContainsKey(filename) == false)
                {

                    if (callingAssembly != null)
                    {

                        stream = callingAssembly.GetManifestResourceStream(fullFileName);

                    }
                    else
                    {
                        StreamReader reader = new StreamReader(File.OpenRead(fullFileName));

                        stream = reader.BaseStream;

                    }

                    //TOOD MG_GRAPHICS close these streams?   sounds per level?


                    if (stream == null)
                    {
                        Debug.WriteLine("could not load sound" + fullFileName);
                        return false;
                    }


                    Streams.Add(filename, stream);
                }


                string soundkey = GetKeyName(filename);


                if (_wavSoundEffects.ContainsKey(soundkey) == false)
                {
                    SoundEffect effect = SoundEffect.FromStream(stream);

                    if (effect != null)
					{

                        _wavSoundEffects.Add(soundkey, effect);
	                    return true;				
					}
                }

            }


#if MP3
            filename = System.IO.Path.GetFileName(path);

               if (mediaSounds.ContainsKey(filename.ToLower()))
                   return;

               MediaPlayer  //inf MOnogame only one MediaPlayer is allowed

               //for the Tool in wpf we just use loose files..  
               //Use MediaPlayer instead of MediaElement, because MediaElement is different in WPF , it wont load a stream either, gives no error
               //but makes no sound in wpf..

               if (!File.Exists(path))
               {
                   Debug.WriteLine( "Error in Load Sound. File does not exist: " + path);
                   return;
               }

               Uri uri = new Uri(path);
               media.Volume = 0;
               media.Open(uri);
               _sounds.Add(filename, media);
#endif

            return false;
        }







        //note background sound is weird.. on reset it wont start again.. might be too slow to stop.
        /// <summary>
        /// On level unload , stop all  preloaded sounds and stop and dispose  all variants, clear instances of effects.
        /// </summary>
        public void StopAllSounds(bool stopBackGround)
        {
#if MP3
            if (_mp3SoundEffects != null)
            {
                _mp3SoundEffects.ToList().ForEach(x =>
                {
                    if (stopBackGround || x.Key != AudioManager.Instance.BackGroundMusicFile)
                    {
                        x.Value.Stop();
                    }
                });
            }
#endif
            StopAndClearSoundEffectInstances();
        }


        public void StopAndClearSoundEffectInstances()
        {

            if (_wavSoundEffectInstances != null)
            {
                _wavSoundEffectInstances.ToList().ForEach(x => { if (!x.Value.IsDisposed) { x.Value.Stop(); x.Value.Dispose(); } });
                _wavSoundEffectInstances.Clear();

            }
        }



        public void SetVolumeOnAllSounds(bool toBackGround, float volume)
        {
#if MP3
            if (_mp3SoundEffects != null)
            {

                double origVolume = 1d;
                _mp3SoundEffects.ToList().ForEach(x =>
                {
                    if (toBackGround || x.Key != AudioManager.Instance.BackGroundMusicFile)
                    {

                        origVolume = 1d;
                        if (_mp3SoundVolumes.ContainsKey(x.Key))
                        {
                            origVolume = _mp3SoundVolumes[x.Key];
                        }

                        x.Value.Volume = volume * origVolume; ;
                    }

                });
            }
#endif


            if (volume != 0 && WavSoundEffects != null)
            {
                SoundEffect.MasterVolume = volume;
            }
            else
            {
                StopAndClearSoundEffectInstances();
            }

        }



        /*  private void DisposeAllSoundEffects()
          {
              if (_wavSoundEffects != null)
              {
                  _wavSoundEffects.ForEach(x => x.Value.Dispose());
              }
          }

          /// <summary>
          /// unload everything.. believe this is done on close instance anyways.
          /// </summary>
          public void UnloadAllSounds()
          {   
              StopAndClearSoundEffectInstances();
              DisposeAllSoundEffects();

              if (_mp3SoundEffects != null)
              {
                  _mp3SoundEffects.Clear();
              } 
              if (_wavSoundEffects != null)
              {
                  _wavSoundEffects.ForEach(x => x.Value.Dispose());
              }          
          }*/


        /// <summary>
        /// Plays the specified sound. Will call LoadSound to load the sound if it's
        /// not already loaded. This may result in the sound not playing on the first invocation.
        /// </summary>
        /// <param name="fileName">The path and file of the sound, eg. Content/Sound/atom-placed.mp3</param>
        public void PlaySound(string fileName)
        {

            if (IsSoundOn)
                return;

            // Wrap and send in the assembly of the application who's calling us. Otherwise,
            // GetCallingAssembly will give us the Tools assembly.
            PlaySound(fileName, 0.5f);
        }



        /// <summary>
        /// Plays the specified sound. Will call LoadSound to load the sound if it's
        /// not already loaded.
        /// </summary>
        /// <param name="fileName">The path and file of the sound, eg. Content/Sound/atom-placed.mp3</param>
        /// <param name="volume">Volume between 0 Mute and 1 Max, default is 0.5</param>

        public void PlaySound(string fileName, double volume)
        {
            if (!IsSoundOn)
                return;

            if (volume > 1)
            {
                Debug.WriteLine("param error in PlaySound volume must be between 1 and 0");
            }

            try
            {

           

#if MP3

//we can play one song.. but not mix them in monogame unless using sound effect, mp3 from monogame content thig
                if (resourceKey.EndsWith(".mp3"))
                {


                    try
                    {


                        MediaElement mp3Element = _mp3SoundEffects[resourceKey];
                        Stream stream = null;
                        if (_streams.ContainsKey(resourceKey))
                        {
                            stream = _streams[resourceKey];
                        }

                        mp3Element.SetSource(stream);  //TODO windows phone error is that is the wrong type.. its unmanaged.. expects something else..
                        mp3Element.AutoPlay = true; // If we set this to false, sound will never be played in SL on browser
                        mp3Element.Play();

                    }

                    catch (Exception exc)
                    {
                        Debug.WriteLine("errorplaying " + resourceKey + " " + exc.Message);
                    }

#endif




                //    if (resourceKey.EndsWith(".wav")) //keys dont have extension..
                {
                    // We must use effect instance in order to gain control of the pitch and pan during play
                    SoundEffectInstance effectInstance = FindOrCreateSoundEffectInstance(fileName);


                    if (effectInstance != null)
                    {
                        effectInstance.Stop();  //rewind, interrupt current and  repeat noise
                        effectInstance.Volume = MathUtils.Clamp((float)volume * MasterVolume, 0f, 1f);
                        effectInstance.Play();
                    }
                }


            }

            catch (Exception exc)
            {
                Debug.WriteLine(" Error playing  Sound " + fileName + " " + exc.Message);
            }
        }




        /// <summary>
        /// PlaySoundEffect  a sound with the same source , that can be keyed by pitch and pan..so that two can be played simultaneously
        /// to creature a fuller sound.. such as rain..
        /// </summary>
        /// <param name="fileName">wav file name</param>
        /// <param name="volume">between 1 and 0 </param>
        /// <param name="panFactor">between -1 and 1, 0 is centered between stereo speakers</param>
        /// <param name="pitchShift">between -1 and 1,  0 is no effect  </param>
        /// <param name="rePlay">means replay on every call without checking if variant pan or pitch is playing</param>      
        /// <param name="bodyID">The bodies hashID , If non zero will not play simultaneous sound for one Body </param>
        public void PlaySoundEffect(string fileName, double volume, double panFactor, double pitchShift, bool rePlay, int bodyID)
        {

            if (!IsSoundOn)
                return;

            try
            {
                string resourceKey = GetKeyName(fileName);


                if (volume > 1.0f)
                {
                    throw new ArgumentOutOfRangeException(errorVolumeRange + volume);
                }



                //should allow to play mutple short shouts  ( ie  particles at different pans.. etc, rocks rolling at different pitch) 
                //    float vol = (float)Math.Round(volume , 1);


                float pan = (float)Math.Round(panFactor, 1);
                float pitch = (float)Math.Round(pitchShift, 1);


                pan = MathUtils.Clamp(pan, -1.0f, 1.0f);
                pitch = MathUtils.Clamp(pitch, -1.0f, 1.0f);



                //don't key on volume for now , its not considered a different sound..  was too much crunch (repeat)  on rocks..  for rain.. it will be more full based on pan.
                //NOTE.. in some places the pan or volume was adjusted after..  this would break that, first playing a loud, then a soft

                string variant = bodyID != 0 ? bodyID.ToString() : /*pan.ToString() +*/pitch !=0 ?  pitch.ToString():"";

                float vol = (float)(volume * MasterVolume); // 

                vol = MathUtils.Clamp(vol, 0f, 1.0f);


                if (!rePlay)
                {
                    SoundEffectInstance seInstance;
                    if (_wavSoundEffectInstances.TryGetValue(resourceKey + variant, out seInstance))
                    {
                        if (seInstance.State == SoundState.Playing)
                            return;
                    }
                }

                SoundEffectInstance effectInstance = FindOrCreateSoundEffectInstance(fileName, variant);

                if (effectInstance != null)
                {
                    effectInstance.Volume = vol;
                    effectInstance.Pan = pan;
                    effectInstance.Pitch = pitch;
                    effectInstance.Stop();  //rewind, interrupt current if same..  and  repeat noise
                    effectInstance.Play();

                }

            }

            catch (Exception exc)
            {
                Debug.WriteLine("error in PlaySoundEffect" + exc.Message);
            }
        }
        /*
        /// <summary>
        /// Takes an assembly because is not often called from the executing assembly.
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="callingAssembly"></param>
        /// <param name="volume">Volume betwee 0 Mute and 1 Max, default is 0.5</param>
        private 
        /// <summary>
        /// Takes an assembly because is not often called from the executing assembly.
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="callingAssembly"></param>
        /// <param name="volume">Volume betwee 0 Mute and 1 Max, default is 0.5</param>
        void PlaySound(string fileName, Assembly callingAssembly, double volume)
        {

            if (!IsSoundOn)
                return;


            if (volume > 1.0f)   //need this check .. way too often put 10.   for sounds must work in release build as well as debug is too slow
            {
                throw new ArgumentOutOfRangeException(errorVolumeRange + volume);
            }

            try
            {

    #if !MONOGAME
    #if SILVERLIGHT
                Dispatcher dispatcher = System.Windows.Deployment.Current.Dispatcher;
    #else
            Dispatcher   dispatcher = System.Windows.Application.Current.Dispatcher;
    #endif

                if (!dispatcher.CheckAccess())      // The calling thread has access, then it's safe to call it directly without invoke
                {
                    dispatcher.BeginInvoke(new Action(() =>
                    {
                         PlaySound(fileName, callingAssembly, volume);
                    }
                ));
                    return;
                }

    #endif
                string fullFileName = EmbeddedResourceHelper.CheckAndSanitizePath(fileName);
                float factor = (float)volume * MasterVolume;
                factor = MathUtils.Clamp(factor, 0f, 1f);
                //TODO maybe check for max concurrent sounds,  
                //if too many , bump some noises...
                //use sound.MediaEnded , to uncount them.
                //Rewind and play
    #if SILVERLIGHT || MONOGAME
    #if SILVERLIGHT
                if (fullFileName.EndsWith(".mp3"))
                {
                    if (!_mp3SoundEffects.ContainsKey(fullFileName))
                    {
                        LoadSound(fullFileName, callingAssembly);
                    }

                    MediaElement mp3Element = _mp3SoundEffects[fileName];

                    mp3Element.Volume = volume;
                    _mp3SoundVolumes[fileName] = (float)volume;
                    mp3Element.Play();
                }
    #endif
                else if (fullFileName.EndsWith(".wav"))
                {
                    SoundEffectInstance effectInstance =  FindOrCreateSoundEffectInstance(fullFileName);
                    effectInstance.Volume = (float)volume;
                    effectInstance.Play();
                }
    #else

                if (!this._sounds.ContainsKey(fileName))
            {
                    LoadSound(fullFileName, callingAssembly);
            }


            ToolMediaPlayer sound = this._sounds[fileName];
            sound.Volume = volume * MasterVolume;

            sound.Stop(); // Rewind
            sound.Play();          
    #endif
            }
            catch (Exception exc)
            {
                Debug.WriteLine("error in PlaySound " + exc.Message);
            }
        }

    #if !MONOGAME
        private Dispatcher GetDispatcher()
        {

    #if SILVERLIGHT
            Dispatcher dispatcher = System.Windows.Deployment.Current.Dispatcher;
    #else
            Dispatcher   dispatcher = System.Windows.Application.Current.Dispatcher;
    #endif
            return dispatcher;
        }

    #endif


        */
        //Effects

        /// <summary>
        /// Moves sound  from Left to Right speaker
        /// </summary>
        /// <param name="soundkey">  handle to the sound, for now just the stream name</param>
        /// <param name="factor">  from -1 left to 1  right, 0 is center</param>
        public void Pan(string filename, float factor)
        {

            if (!IsSoundOn)
                return;
            try
            {

                factor = MathUtils.Clamp(factor, -1, 1);
                string soundkey = GetKeyName(filename);


                {
                    SoundEffectInstance effectInstance = FindOrCreateSoundEffectInstance(soundkey);

                    if (effectInstance != null)
                    {
                        effectInstance.Pan = factor;
                    }
                }

            }
            catch (Exception exc)
            {
                Debug.WriteLine("error in Pan  Sound" + exc.Message);
            }
        }

        public string GetKeyName(string fileName)
        {

            // If we have a stream of wav of this resourceName, then use it  instead
            // 

            string[] pathSplit = fileName.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '.' });


            if (pathSplit.Length < 2)
                return fileName;
            //    string keyName = System.IO.Path.GetFileNameWithoutExtension(fileName);  does not work for embedded path;


            string keyName = pathSplit[pathSplit.Length - 2];


            return keyName.ToLower();

        }

        /// <summary>
        /// with wav will change tempo  ( playback speed) -1.0f (down one octave) to 1.0f (up one octave).  in Ogg .. soon to be removed.  Changes pitch without tempo using fft in ogg file  
        /// </summary>
        /// <param name="filename">handle to the sound, for now  the stream name</param>
        /// <param name="factor">For Wav,   Pitch adjustment, ranging from -1.0f (down one octave) to 1.0f (up one octave).</param>
        public void PitchShift(string filename, float factor)
        {
            if (!IsSoundOn)
                return;

            try
            {
                string soundkey = GetKeyName(filename);

#if SILVERLIGHT
         
                if (!GetDispatcher().CheckAccess())      // The calling thread has access, then it's safe to call it directly without invoke
                {
                    GetDispatcher().BeginInvoke(
                        new Action(() =>
                    {
                        PitchShift(filename, factor);
                    }
                    ));
                    return;
                }

#endif

                {
                    factor = MathUtils.Clamp(factor, -1f, 1f);
                    SoundEffectInstance effectInstance = FindOrCreateSoundEffectInstance(soundkey);

                    if (effectInstance == null)
                        return;

                    effectInstance.Pitch = factor;
                }

                //   System.Diagnostics.Trace.TraceInformation("doppler pitch factor" + factor.ToString());
            }
            catch (Exception exc)
            {
                Debug.WriteLine("error in Pitchshift Sound" + exc.Message);
            }
        }


        /// <summary>
        /// Set volume on a sound, allows  mixing
        /// </summary>
        /// <param name="soundkey">handle to the sound, the stream name</param>
        /// <param name="volume"> sound  volume factor,  valid from 0 to 1..    </param>
        public void SetVolume(string filename, float volume)
        {

            if (!IsSoundOn)
                return;

            try
            {
                string soundkey = GetKeyName(filename);


                if (!IsPlaying(soundkey))
                    return;

                float factor = volume * MasterVolume;
                factor = MathUtils.Clamp(factor, 0f, 1f);



                SoundEffectInstance effectInstance;
                if (_wavSoundEffectInstances.ContainsKey(soundkey))
                {
                    effectInstance = _wavSoundEffectInstances[soundkey];
                    effectInstance.Volume = factor;
                }




#if MP3

                if (!GetDispatcher().CheckAccess())      // The calling thread has access, then it's safe to call it directly without invoke
                {
                    GetDispatcher().BeginInvoke(new Action(() =>
                    {
                        SetVolume(filename, volume);
                    }));
                    return;
                }

         
                if (!IsPlaying(soundkey))
                    return;

                float factor = volume * MasterVolume;
                factor = MathUtils.Clamp(factor, 0f, 1f);


                if (soundkey.EndsWith(".mp3"))
                {
                    //TODO   change this to GameMediaElement if needed to wrap it..now usng a map to hold original volume.  implement set volume stepped if needed
                    MediaElement mp3Element = _mp3SoundEffects[soundkey];
                    mp3Element.Volume = factor;
                    _mp3SoundVolumes[soundkey] = volume;
                }  else

                    ToolMediaPlayer player = _sounds[soundkey.ToLower()];
                Application.Current.Dispatcher.BeginInvoke(
                     new Action(() => { player.Volume = factor; }));
#endif
            }

            catch (Exception exc)
            {

                Debug.WriteLine(exc.Message);

            }
        }


        /// <summary>
        /// Stop playing a sound..
        /// </summary>
        /// <param name="soundkey">  handle to the sound, the stream name, can end in mp3, wil be changed to ogg in play</param>

        public void StopSound(string filename)
        {
            try
            {
                string soundkey = GetKeyName(filename);


                SoundEffectInstance effectInstance;
                if (_wavSoundEffectInstances.ContainsKey(soundkey))
                {
                    effectInstance = _wavSoundEffectInstances[soundkey];
                    effectInstance.Stop(true);
                }

            }
            catch (Exception exc)
            {
                Debug.WriteLine(" error in stop sound" + exc.Message);
            }
        }
    }
}

