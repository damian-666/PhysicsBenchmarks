using System;
using System.Collections.Generic;
using System.Text;

using Core.Data.Interfaces;



using Core.Data;
using System.Diagnostics;


using Core.Data.Input;

using System.Threading.Tasks;
using System.Reflection;
using Farseer.Xna.Framework;

using Core.Game.MG.Simulation;
using Core.Game.MG;


namespace _2DWorldCore
{
    /// <summary>
    /// This is legacy ui. .proted from silverlight.. TODO move in new UIObject
    /// </summary>

    //LOCALIZATION:  try google xlate on the fly.
    public class GameUI 
    {

        //    private static UIPageBinding _uiBinding;

        //  string _backGroundMusic = "gamelangood1var.mp3";
        string _backGroundMusic = "gamelangood16kbitvar";
        public static bool IsBackgroundMusicOn = true;

        public GameUI()
        {
            Init();
        /*TODO MG_GRAPHICS    Window.Current.SizeChanged += (s, e) =>
            {
                UIBinding.SaveSettings();

                Debug.WriteLine("size");
            };


            Window.Current.Closed += (ss, ee) =>
            {

                Debug.WriteLine("close");

                UIBinding.SaveSettings();          //todo is this called   
            };

            */
        }




        //TODO load for warnings, cleanup, missing fonts, make a sprit
        const string SettingsFileName = "Settings.xml";

        public bool IsTrial { get; internal set; }

        private void Init()
        {
            AudioManager.Instance.BackGroundMusicFile = _backGroundMusic;

            /*MG_GRAPHICS  _orignalBrush = _musicToggle.Background;

              _uiBinding = LoadSettings();

              if (_uiBinding == null)
              {
                  _uiBinding = new UIPageBinding(); //this is our ViewModel class
              }



              _musicToggle.IsChecked = _uiBinding.IsBackgroundMusicOn;


             */

         
        }

        public void CheckToStartBackgroundMusic()
        {
            if (!IsBackgroundMusicOn)
            {
                //TODO   AudioManager.Instance.StopSound(_backGroundMusic);
            }
            else
                if (!AudioManager.Instance.IsPlaying(_backGroundMusic))
            {
                AudioManager.Instance.PlaySound(_backGroundMusic, 0.3f);
            }
        }


        /* MG_GRAPHICS




                public void HideFPSInfo()
                {
                    _debugInfo.Visibility = Visibility.Collapsed;

                }


                //On windows phoneSL it measures about  20% slower with this on but that is beleved due to render stats panel update PHONE..
                //we can use   Application.Current.Host.Settings.EnableFrameRateCounter = true to see this happen. its rendering the stats..taking time

                public void ShowFPSInfo()
                {
                    _debugInfo.Visibility = Visibility.Visible;

                }


                public void ToggleFPSInfo()
                {
                    _debugInfo.Visibility = (_debugInfo.Visibility == Visibility.Visible) ? Visibility.Collapsed : Visibility.Visible;
                }


                public bool IsDebugPanelVisible()
                {
                    return (_debugPanel.Visibility == Visibility.Visible);
                }


                public void HideDebugPanel()
                {
                    _debugPanel.Visibility = Visibility.Collapsed;
                }


                public void ToggleDebugPanel()
                {
                    _debugPanel.Visibility = (_debugPanel.Visibility == Visibility.Visible) ? Visibility.Collapsed : Visibility.Visible;
                }





                public bool IsFPSInfoVisible() => _debugInfo.Visibility == Visibility.Visible;

                public TextBlock TxtRePlays => _txtPlays;

                public TextBlock TxtTitle => _txtTitle;

                public TextBlock TxtBuildNum => _txtBuildNum;

                public bool IsTrial { get; set; }

                public StackPanel PanelPlays => _panelPlays;




                private IHelpDialog _helpDlg;
                public IHelpDialog HelpBox
                {
                    get
                    {
                        if (_helpDlg == null)
                        {
                            if (IsSpanish())
                            {
                                _helpDlg = new HelpDialog_ES();
                            }
                            else
                            {
                                _helpDlg = new HelpDialog();
                            }
                        }

                        return _helpDlg;
                    }

                    set
                    {
                        _helpDlg = value;
                    }
                }


        #if !WINDOWS_UWP
                private EULADialog _eulaDlg;
                public EULADialog EULABox
                {
                    get
                    {
                        if (_eulaDlg == null)
                        {
                            _eulaDlg = new EULADialog();
                        }

                        return _eulaDlg;
                    }
                }
        #endif


                #region  Helper accessor for internal ui controls


                public Slider ZoomSlider => _zoomSlider;

                public Button BtnResetLevel => _btnResetLevel;

                public Button BtnChooseLevel => _btnChooseLevel;

                public Button BtnLoadLevel => _btnLoadLevel;

                public Button BtnSaveLevel => _btnSaveLevel;


                public ProgressBar BarActiveSpiritEnergy => _barActiveSpiritEnergy;

                public TextBlock TxtRenderFPS => _txtRenderFPS;

                public TextBlock TxtAvgRenderUpdate => _txtAvgRenderUpdate;

                public TextBlock TxtPhysicsFPS => _txtPhysicsFPS;

                public TextBlock TxtAvgPhysicsUpdate => _txtAvgPhysicsUpdate;

                public TextBlock TxtBodies => _txtBodies;


                public TextBlock TxtJoints => _txtJoints;



                public TextBlock TxtController
                {
                    get { return _txtController; }
                }


                public TextBlock TxtGravity
                {
                    get { return _txtGravity; }
                }


                public TextBlock TxtPosIteration
                {
                    get { return _txtPosIteration; }
                }


                public TextBlock TxtVelIteration
                {
                    get { return _txtVelIteration; }
                }




                public TextBlock TxtAverageSpeed
                {
                    get { return _txtAverageSpeed; }
                }

                public TextBlock TxtSpeedUnits
                {
                    get { return _txtSpeedUnits; }
                }

                #endregion
                // display help
                private void btnHelp_Click(object sender)
                {
                    HelpBox.ToggleVisibility();
                }

                protected void btnAboutBox_Click(object sender)
                {

                    _aboutDlg.ToggleVisibility();  //TODO_UWP


                }

                private AboutBox _aboutDlg;
                public AboutBox AboutBox
                {
                    get
                    {
                        if (_aboutDlg == null)
                        {
                            _aboutDlg = new AboutBox();
                        }

                        return _aboutDlg;
                    }
                }


                private void MusicToggle_Click(object sender)
                {
                    //TODO this should be dowloaded during play.. might interfere with heartbeat on slow connection tho..
                    // could skip heartbeat tille its done.. also.. could download levels one at a time.. during play for faster startup.
                    _uiBinding.IsBackgroundMusicOn = !_uiBinding.IsBackgroundMusicOn;
                    CheckToStartBackgroundMusic();
                    _musicToggle.IsChecked = _uiBinding.IsBackgroundMusicOn;  //doestn change look..

                    SetMusicButtonColor();
                    SaveSettings();
                }


                Brush _orignalBrush;
                //ugly but works.
                private void SetMusicButtonColor()
                {
                    if (_uiBinding.IsBackgroundMusicOn)
                    {
                        _musicToggle.Background = _orignalBrush;
                        _musicToggle.Opacity = 0.9f;
                    }
                    else
                    {
                        _musicToggle.Background = new SolidColorBrush(Colors.White);
                        _musicToggle.Opacity = 0.5f;

                    }
                }


                static UIPageBinding LoadSettings()
                {
                    return Storage.Serialization.LoadDataFromIsoStorage<UIPageBinding>(SettingsFileName);
                }

                static public void SaveSettings()
                {
                    Storage.Serialization.SaveDataToIsoStorage<UIPageBinding>(SettingsFileName, _uiBinding);
                }
           */

        private void btnPause_Click(object sender)
        {
            SimWorld.Instance.PhysicsThread.IsRunning = !SimWorld.Instance.PhysicsThread.IsRunning; //TODO concurrency check is this standard kind of pause pattern.  we do this when we have a lokck
        }


        private void btnStep_Click(object sender)
        {
            ShadowFactory.Engine.SingleStepPhysicsUpdate();
        }



        /* MG_GRAPHICS
        public void UpdateSaveLoadKeys()
        { 
            _btnLoadPos.IsEnabled = Storage.LevelKey.HasSavedPos(Level.RecentInstance.LevelNumber, Level.RecentInstance.LevelDepth) ? true : false;
        }*/

        public void btnSavePosition_Click(object sender)    
        {
            try
            {
                Level level = Level.Instance;
                Storage.LevelKey.SavePositionInLevel(level.LevelNumber, level.LevelDepth, level.ActiveSpirit.Position.X, level.ActiveSpirit.Position.Y, level.ActiveSpirit.EnergyLevel);
             
                
                //MG_GRAPHICS UpdateSaveLoadKeys();

            }
            catch (Exception exc)
            {
                Debug.WriteLine("error in SavePos" + exc.Message);
            }
        }



        public void btnReloadPosition_Click(object sender)
        {
            try
            {
                Level level = Level.Instance;

                Storage.LevelKey.LoadSavedPos(level.LevelNumber, level.LevelDepth, out var X, out var Y, out var Energy);
                //TODO .. should really serialize the whole spirit in its  currrent state, on save.. then reload it..  as we do on travellers.
                //but bruisues  and state dont matter much .. just you got there and how much energy... will regen anyways.
                //this is good enough.
                SimWorld.Instance.ReloadNewCreatureAtPosition(X, Y);
                Level.Instance.ActiveSpirit.EnergyLevel = Energy;  //TODO this is broken , gets set to 200 somewhere.

            }
            catch (Exception exc)
            {
                Debug.WriteLine("error in Reload Pos" + exc.Message);
            }
        }

        //TODO LATER localize more.. remove this ... replace with Case ES, DE, etc..
        public static bool IsSpanish()
        {
            return false;
            //  return CultureInfo.CurrentCulture.TwoLetterISOLanguageName.ToLower().Contains("es");  //TODO parse it out.. make a case.
        }



        #region uihandlers
        private void Button_LClick(object sender)
        {
            InputCommand.Instance.KeyState = GameKey.None;
            InputCommand.Instance.KeyDown(GameKey.Left);

        }


        private void Button_RClick(object sender)
        {
            InputCommand.Instance.KeyState = GameKey.None;
            InputCommand.Instance.KeyDown(GameKey.Right);

        }

        private void Button_UClick(object sender)
        {
            InputCommand.Instance.KeyState = GameKey.None;
            InputCommand.Instance.KeyDown(GameKey.Up);
        }

        private void Button_DClick(object sender)
        {
            InputCommand.Instance.KeyState = GameKey.None;
            InputCommand.Instance.KeyDown(GameKey.Down);

        }

        private void Button_XClick(object sender)
        {
            InputCommand.Instance.KeyState = GameKey.None;
            InputCommand.Instance.KeyState = GameKey.X;
        }

        private void Button_YClick(object sender)
        {
            InputCommand.Instance.KeyState = GameKey.None;
            InputCommand.Instance.KeyState = GameKey.Y;
        }
        private void Button_AClick(object sender)
        {
            InputCommand.Instance.KeyState = GameKey.None;
            InputCommand.Instance.KeyDown(GameKey.A);
        }
        private void Button_BClick(object sender )
        {
            InputCommand.Instance.KeyState = GameKey.None;
            InputCommand.Instance.KeyDown(GameKey.B);
        }





        #endregion
        /* MG_GRAPHICS
        private void _panSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {

            Vector2 center = Graphics2.Instance.Presentation.Camera.Transform.WindowCenter;
            Graphics2.Instance.Presentation.Camera.Transform.WindowCenter = new Vector2((float)e.NewValue, center.Y);

        }


        private void _panSlider_ValueYChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {

            Vector2 center = Graphics2.Instance.Presentation.Camera.Transform.WindowCenter;
            Graphics2.Instance.Presentation.Camera.Transform.WindowCenter = new Vector2(center.X, (float)e.NewValue);

        }*/

        private void BtnResetFPS(object sender)
        {

            //TODO MG_GRAPHICS  _uiBinding.PhysicsFPS = 60;
          //  _uiBinding.PhysicsTimerFPS = 200;


        }


     
    }



}

