using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Runtime.Serialization;

using Farseer.Xna.Framework;
using FarseerPhysics.Common;
using FarseerPhysics.Factories;
using System.Threading.Tasks;

namespace FarseerPhysics.Dynamics.Particles
{
    [DataContract]
    public class LaserEmitter : Emitter
    {
        #region MemVars & Props

        //TODO should not be static.. may not be thread safe multpile lasers..
        public delegate Fixture SpawnLaserDelegate(LaserEmitter e, Vector2 endPoint, string laserId, float laserThickness, out Vector2 hitPoint);

        public static event Action<LaserEmitter, string> OnLaserOff = null;
        public static event SpawnLaserDelegate OnSpawnLaser = null;

        public static event Action<LaserEmitter, Fixture, Vector2> OnLaserHit = null;

        public event Action OnEmitLaser = null;

        [DataMember]
        public float LaserLength { get; set; }


        [DataMember]
        public float Thickness { get; set; }  //laser light beam thickness


        [DataMember]
        public float PulseDuration { get; set; }// for pulsed laser, the duration of each pulse.. should be < 1/ Frequency 


        /// <summary>
        /// Power of laser..not in watts, its in the lenght it can cut of a body of normal 200 Density.  the cut lenght will be scaled linearly with the Density.  So cutting a dense rock of  400 Kg/sq m,  with a laser of power 1 will result in a cut lenght of 1/2 meter.
        /// </summary>
        [DataMember]
        public float Power { get; set; }  //laser light cutting power in Watts  .  for now if cut joints thats ok.. later breakable body.



        [DataMember]
        public float MaxEnergy { get; set; } // total energy on full charge in Watt Hours..



        public float Energy { get; set; } //current energy;



        private bool _active = false;

        [DataMember]
        public override bool Active
        {
            get { return _active; }
            set
            {
                _active = value;
                _laserDuration = 0;
                FirePropertyChanged();
            }
        }


        Mat22 mat;
        Random _randomizer;
        private string _laserId = "";
        double _laserDuration = 0;
        bool _laserOn = true;

        #endregion


        #region Ctor


        public LaserEmitter(Body parent, Vector2 localPos)
            : base(parent, localPos)
        {
            LaserLength = 1.0f;

            Offset = Vector2.Zero;
            Active = true;
            Thickness = 1.0f;   // Default (will be scaled by zoom factor)

            Frequency = 0;  //means continous, Frequency can be used for pulse laser

            // This is id for Ray use, only accessible via callback
            _laserId = string.Format("Laser{0}:{1}", this.GetHashCode().ToString(), _parent.GetHashCode().ToString());

            _randomizer = new Random();

            Color = new BodyColor(255, 0, 0, 255);

            Power = 1;   //for now means how many meters to cut.. TODO .. make it more meaningful.. as in energy per dt.. ( intensity beam) ..whatever.. so long as dense objects are harder to cut.
            //TODO reflect off of mirrors, or metal armor..
            //TODO heat the item.. at this time make it lense dense so you can cut it by heating it.. make it glow.

            //TODO more important.. add mirrors and make this part of the game.  give some swords reflexivity.


            MaxEnergy = float.PositiveInfinity;

            Energy = MaxEnergy;
        }

        #endregion


        #region Methods

        public Vector2 CalcLaserBeamVector()
        {
            Vector2 direction = Direction;
            direction.Normalize(); //TODO remove if optimize and keep this in base class....will need regression tho..

            Vector2 laserBeamVector = WorldDirection * LaserLength;

            // Only process deviation angle != 0, to save computations
            if (DeviationAngle != 0)
            {
                Debug.WriteLine("cut not implemented for deviation");
                // angle = -deviationAngle/2 ... rnd ... +deviationAngle/2
                float angle = ((float)_randomizer.NextDouble() * DeviationAngle) - (DeviationAngle * 0.5f);
                mat.Set(angle);
                laserBeamVector = new Vector2(mat.Col1.X * laserBeamVector.X + mat.Col2.X * laserBeamVector.Y,
                                         mat.Col1.Y * laserBeamVector.X + mat.Col2.Y * laserBeamVector.Y);
            }

            return laserBeamVector;  //TODO cache this if using the deviation.. see cut..
        }



        public override void Update(double dt)
        {
            if (Active)
            {
                // If frequency 0, then this laser is continuous
                if (Frequency == 0)
                {
                    _laserOn = true;
                }

                // If laser is on
                if (_laserOn && Energy > 0)
                {
                    // Count the pulse duration
                    _laserDuration += dt;

                    if (MaxEnergy != float.PositiveInfinity)
                    {
                        Energy -= Power;
                    }


                    if (_laserDuration > PulseDuration)
                    {
                        _laserOn = false;
                        _laserDuration = 0;
                    }

                    Vector2 vecBeam = CalcLaserBeamVector();

                    Vector2 endPoint = Vector2.Add(WorldPosition, vecBeam);

                    Fixture collidedFixture = null;

                    // Set hit point to end point, just in case laser hit no fixture
                    Vector2 hitPoint = endPoint;

                    if (OnEmitLaser != null)
                    {
                        OnEmitLaser();
                    }

                    // We can't spawn ray here because in model, delegate it to the presentation                    if (OnSpawnLaser != null)
                    {
                        collidedFixture = OnSpawnLaser(this, endPoint, _laserId, Thickness, out hitPoint);
                    }

                    // If we have a fixture collided with laser then inform the client that something is hitting laser ray
                    if (collidedFixture != null && OnLaserHit != null)
                    {
                        OnLaserHit(this, collidedFixture, hitPoint);
                    }
                }
                else
                {
                    // Count the delay timer
                    _laserDuration += dt;
                    if (_laserDuration > (1 / Frequency))
                    {
                        _laserOn = true;
                        _laserDuration = 0;
                    }

                    // TUrn off the laser
                    if (OnLaserOff != null)
                    {
                        OnLaserOff(this, _laserId);
                    }
                }
            }
            else
            {
                if (OnLaserOff != null)
                {
                    OnLaserOff(this, _laserId);
                }
            }
        }

        [OnDeserialized]
        public new void OnDeserialized(StreamingContext sc)
        {
            _randomizer = new Random();

            _laserId = string.Format("Laser{0}:{1}", this.GetHashCode().ToString(), _parent.GetHashCode().ToString());

            if (Thickness == 0)
            {
                Thickness = 1.0f;
            }


            if (MaxEnergy == 0)  //legacy files
            {
                MaxEnergy = float.PositiveInfinity;
            }

            Energy = MaxEnergy;

        }

        #endregion
    }
}
