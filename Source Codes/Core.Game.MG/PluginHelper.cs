using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Core.Data.Collections;
using Core.Data.Entity;
using System.Reflection;
using System.IO;
using Core.Data.Plugins;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using Core.Game.MG;
using Core.Game.MG.Simulation;
using Core.Data.Interfaces;
using Core.Data;
using Storage;

namespace Core.Game.MG.Plugins
{
    //this is ,aybe  now meant only for Toolbut Path is in netstandard.. if not shared TODO remove ifdefs SILVERLIGHT etc..  if needed move to .netstandard but instantation and securityh are not standard forf compile plugin 
    /// <summary>
    /// Plugin Helper - Since Plugin is now Spirit related, now it's ok to put any Spirit inside this helper
    /// </summary>
    public class PluginHelper
    {
        #region MemVars

        public static string PluginNamespace = "Core.Game.MG.Plugins";

        public static Func<string, IPlugin> CompilePluginScript = null;
     
        #endregion




        public static void PrepareLevelPlugin(Level level, IPlugin<Level> plugin)
        {

            try
            {

                // If the plugin is failed to instantiated, then get out
                if (plugin == null)
                    return;

         
                if (level.Plugin != null)
                {
                    // If spirit already has plugin, then unload it
                    level.Plugin.UnLoaded();
                }

       
                plugin.Parent = level;

                level.Plugin = plugin;
                plugin.Loaded();

            }
            catch (Exception exc)
            {
                //dh got this loading a level.. need to handle this or cant fix the level.
                System.Diagnostics.Debug.WriteLine("Error in PrepareLevelPlugin" + exc.Message);
                System.Diagnostics.Debug.WriteLine("Stack in PrepareLeveltPlugin" + exc.StackTrace);
            }
        }


        public static void PrepareSpiritPlugin(Spirit spirit, IPlugin<Spirit> plugin)
        {
     
            try
            {

                // If the plugin is failed to instantiated, then get out
                if (plugin == null)
                    return;


                if (spirit.Plugin != null)
                {
                    // If spirit already has plugin, then unload it
                    spirit.Plugin.UnLoaded();
                }

             //TODO MG_GRAPHICS remove and use the sington    (plugin as PluginBase).SimWorld = world;

                plugin.Parent = spirit;    
           
                spirit.Plugin = plugin;             
                plugin.Loaded();

            }
            catch (Exception exc)
            {
                //dh got this loading a level.. need to handle this or cant fix the level.
                System.Diagnostics.Debug.WriteLine("Error in PrepareSpiritPlugin" + exc.Message);
                System.Diagnostics.Debug.WriteLine("Stack in PrepareSpiritPlugin" + exc.StackTrace);      
            }
        }

        public static string GenerateAncestorClassName(string script)
        {
            // Let's find our script's class name
            Match classmatch = Regex.Match(script, "class(\\s+)(.*?)(\\s+):(\\s+)(.*?)(\\s+)");

            string classname = "";
            if (classmatch.Success)
            {
                //Base class expected on 5th group
                Group classgrp = classmatch.Groups[5];
                // Get the value
                classname = classgrp.Value;
            }

            classname =classname.TrimEnd(',');
            

            return classname;
        }

        /// <summary>
        /// Generate Namespace Name from a given Script
        /// </summary>
        /// <param name="script">script</param>
        /// <returns>namespace name</returns>
        public static string GenerateClassName(string script)
        {
            // Let's find our script's class name
            Match classmatch = Regex.Match(script, "class(\\s+)(.*?)(\\s+)");

            string classname = "";
            if (classmatch.Success)
            {
                // It'll be on 2nd group
                Group classgrp = classmatch.Groups[2];

                // Get the value
                classname = classgrp.Value;
            }

            return classname;
        }

        /// <summary>
        /// Generate Namespace Name from a given Script
        /// </summary>
        /// <param name="script">script</param>
        /// <returns>namespace name</returns>
        public static string GenerateNamespaceName(string script)
        {
            Match nsmatch = Regex.Match(script, "namespace(\\s+)(.*?)(\\s+)");

            // It'll be on 2nd group    TODO issue can have comments above the code, why to we assume group third (2)
            Group nsgrp = nsmatch.Groups[2];

            // Get the value
            string nsname = nsgrp.Value;
            return nsname;
        }

        public static string GenerateClassFullName(string script)
        {
            string nsname = GenerateNamespaceName(script);
            string classname = GenerateClassName(script);
            return string.Format("{0}.{1}", nsname, classname);
        }

        public static string GetPluginClassFullName(string pluginName)
        {
            return string.Format("{0}.{1}", PluginNamespace, pluginName);
        }


        //TODO MG_GRaphcis   netstandard way?
        /// <summary>
        /// Generate Assembly Qualified Full Name for type instantiation
        /// </summary>
        /// <param name="pluginName">plugin name</param>
        /// <returns>Instantiated Plugin3</returns>
        public static string GetAQNFromNameAndModule(string pluginName, bool addNameSpace)
        {
            //used sn - p  Candide.snk candiePublic.snk, to get public key ,  then Sn -p CandidePublic to get the token

            string aqn;


            if (addNameSpace)
            {
                pluginName += PluginNamespace = ".";
            }


            if (addNameSpace)
            {
                pluginName += PluginNamespace = ".";
                return GetAQNFromNameAndModule(pluginName, false);
            }

            //Corexx.game module must be signed

            string suffix = ""; 

             aqn = string.Format("Core.Game.MG.Plugins.{0}, Core.Game.MG.Plugins{1}, Version=1.0.0.0, Culture=neutral", pluginName, suffix);



            return aqn;

        }

     

        /// <summary>
        /// Instantiate a Plugin based on the plugin name, used in game code when plugins are in project file and precompiled
        /// </summary>
        /// <param name="pluginName"></param>
        /// <returns></returns>
        public static IPlugin<Spirit> InstantiatePlugin(string pluginName)
        {

     
            string aqn = GetAQNFromNameAndModule(pluginName, false);

            if (aqn == null)
            {
               aqn= GetAQNFromNameAndModule(pluginName, true);

                if (aqn != null) {
                    Debug.WriteLine("success only with addedNamespace to GetAQNFromNameAndModule");
                 }

            }


            IPlugin<Spirit> plugin = null;

            try
            {
                Type pluginType = Type.GetType(aqn, true);      
                plugin = (IPlugin<Spirit>)(Activator.CreateInstance(pluginType));
            }
            catch (Exception exc)
            {
 
                Debug.WriteLine("Error in InstantiatePlugin" + exc.ToString());
            }

            return plugin;
        }



        //TODOmaybe move to tool, not sure if can be used by exe, def not any apps for security reasons.
        /// <summary>
        /// Folder for  loose plugin 
        /// </summary>
        public static string PluginsDirectory
        {
            get
            {
                string pluginPath = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                pluginPath = Path.GetFullPath(Path.Combine(pluginPath, "..\\Source Codes\\Core.Game.Plugins\\PLUGINS")); // This usually start from Bin.Debug or Bin.Release, should work either

                try
                {
                    // If we are in not dev mode, then we won't have valid plugins directory, so create one on the current directory
                    if (Directory.Exists(pluginPath) == false)
                    {
                      //  pluginPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Plugins");
                        Directory.CreateDirectory(pluginPath);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError(ex.Message);
                }


                return pluginPath;
            }
        }

        public static void SavePluginScript(object parent, string pluginName, string script)
        {
    
            string pluginPath = GetPluginPath(parent);

            try
            {
                StreamWriter sw = new StreamWriter(System.IO.Path.Combine(pluginPath, string.Format("{0}.cs", pluginName)));
                sw.Write(script);
                sw.Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(ex.Message);
            }
        }


        public static string GetPluginPath( object extensible)
        {

            string typeName = extensible.GetType().Name;

            return PluginsDirectory + "\\" + typeName + "\\";
        }

        public static string LoadPluginScript(string pluginPath, string pluginName)
        {
             string script = "";

         //TODO  .. must be space after comma on base classes..
    
            try
            {
                StreamReader sr = new StreamReader(System.IO.Path.Combine(pluginPath, string.Format("{0}.cs", pluginName)));
                script = sr.ReadToEnd();
                sr.Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Error in LoadPluginScript: " + ex.Message);
            }
            return script;
        }

        public static void ErasePluginScript(object parent, string pluginName)
        {
            string  pluginPath = GetPluginPath(parent);


            File.Delete(System.IO.Path.Combine(pluginPath, string.Format("{0}.cs", pluginName)));
        }


    }
}
