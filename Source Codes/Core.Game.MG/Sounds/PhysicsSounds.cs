using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


using FarseerPhysics.Dynamics;
using FarseerPhysics.Common;
using FarseerPhysics.Collision;
using Farseer.Xna.Framework;
using FarseerPhysics.Dynamics.Contacts;
using Core.Data.Entity;


using FarseerPhysics.Controllers;
using System.Diagnostics;


namespace Core.Game.MG
{


    /// <summary>
    /// 
    /// Class that makes sounds based on physical objects colliding and moving.
    /// TODO add sound for fast objects, maybe with doppler effects based on main spirit location 
    ///  
    /// </summary>
    public class PhysicsSounds : FarseerPhysics.Controllers.Controller
    {

        public  Spirit _listenerSpirit;
        public  Body _listenerHead;

        public  Spirit ListenerSpirit
        {
            set
            {
                _listenerSpirit = value;

                if (_listenerSpirit != null)
                {
                    _listenerHead = _listenerSpirit.Head;
                }
            }
        }



        //Attach a physics sound controller to this world.
        public PhysicsSounds(World physicsWorld) :
            base(ControllerType.PhysicsSoundsController)
        {
            physicsWorld.ContactManager.PostSolve = OnContacted;
            physicsWorld.AddController(this);
        }


        //on update?  adjust pitch and volume for objects
        //add map to object?

        const float SpeedofSound = 200;   // consider differnent speeds medium later  sense medium around Body. for now a slow  100/m/s for amplified doppler  motion effect


        void OnContacted(Contact contact, ContactConstraint impulse)
        {

            //TODO 
            //first check  how many effects are play...
            // if > 3 dont bother..
            PlayCollisionSoundEffect(contact.FixtureA.Body, contact.FixtureB.Body, contact, impulse);
            PlayCollisionSoundEffect(contact.FixtureB.Body, contact.FixtureA.Body, contact, impulse);
            return;
        }


        //due to serialization, defaults of new fields are zero and cant be set to another value
        //change to something audible or max to infinite
        //so we can hear something to start with
        private void FixDefaultValues(BodySoundEffectParams param)
        {

            if (param.ImpulseTangentialMax == 0)
            {
                param.ImpulseTangentialMax = float.MaxValue;
            }

            if (param.ImpulseTangentialMin == 0)
            {
                param.ImpulseTangentialMin = float.MinValue;
            }

            if (param.RelVelocityNormalMin == 0)
            {
                param.RelVelocityNormalMin = float.MinValue;
            }
            if (param.RelVelocityTangentialMin == 0)
            {
                param.RelVelocityTangentialMin = float.MinValue;

                if (param.MinVelocityBody != 0 )  //for legacy files this was misused should be only for continous sound.. .. TODO erase this after all levels saved
                {
                    param.RelVelocityNormalMin = param.MinVelocityBody;      
                }
            }

            if (param.Volume == 0)
            {   
                param.Volume = 0.6f;
            }

        }



        /// <summary>
        ///  AdjustPanAndVolume based on distance to listener ( active spirit ) 
        /// </summary>
        /// <param name="sound">input source resource</param>
        /// <param name="volume">input volume</param>
        /// <param name="maxPan">Dist at wich its fully panned</param>
        /// <param name="maxDist">Dist at which volume is zero, linear</param>
        public void AdjustPanAndVolume(Vector2 soundSource, string sound, float volume,  float maxPan, float maxDist)
        {
            float pan = 0;
            float volumeFactor = 1;
            GetPanAndVolume(soundSource, out pan, out volumeFactor, 0, 1 / maxPan, 1 / maxDist);  //set a default as 0.05f to have some indication, more important that realism. flux in the planiverse would not allow volume to become zero..TODO  a non linear volume decrease..in the planiverse sound carriers.
            AudioManager.Instance.SetVolume(sound, volume * volumeFactor);
            AudioManager.Instance.Pan(sound, pan);
        }



        /// <summary>
        ///  AdjustPanAndVolume based on distance to listener ( active spirit ) 
        /// </summary>
        /// <param name="sound">input source resource</param>
        /// <param name="volume">input volume</param>
        /// <param name="minVolume">min volume , can be heard at any distance </param>
        /// <param name="maxPan">Dist at wich its fully panned</param>
        /// <param name="maxDist">Dist at which volume is zero, linear</param>
        public void AdjustPanAndVolume(Vector2 soundSource, string sound, float volume, float minVolume, float maxPan, float maxDist)
        {     
            float pan = 0;
            float volumeFactor = 1;
            GetPanAndVolume(soundSource, out pan, out volumeFactor, minVolume, 1 / maxPan, 1 / maxDist);
            AudioManager.Instance.SetVolume(sound, volume * volumeFactor);
            AudioManager.Instance.Pan(sound, pan);
        }

        //todo break out doppler effect too,.
        public void  GetPanAndVolume( Vector2 soundSource, out float  pan, out float  volume, float minVolume, float panXfactor, float volumeDistFactor)
        {
            pan = 0;
            volume = 1;
            Vector2 toEar = GetDisplacementFromEar(soundSource);
            float dist = toEar.Length();       
            // pan sounds left of right based on position.
            pan = toEar.X * panXfactor;
            volume = Math.Max(minVolume, volume - (dist * volumeDistFactor));    
        }

        public void GetPanAndVolume(Vector2 soundSource, out float pan, out float volume)
        {
            GetPanAndVolume( soundSource, out pan, out volume, 0, 1/5f, 1/18f);  // 5 meters to pan fully.., 18 meters to be volume zero 
        }


        public float GetVolumeFactor(Vector2 soundSource) 
        {
            float pan = 0;
            float volume = 1;
            GetPanAndVolume(soundSource, out pan, out volume,0, 1/5f, 1 / 18f);   // 5 meters to pan fully.., 18 meters to be volume zero 
            return volume;
        }

        public float GetVolumeFactor(Vector2 soundSource, float volumeDistFactor)
        {
            float pan = 0;
            float volume = 1;
            GetPanAndVolume(soundSource, out pan, out volume,0, 0.2f, volumeDistFactor);  // 10 meters to pan fully.. 
            return volume;
        }
  
        public void PlayCollisionSoundEffect(Body body, Body otherBody, Contact contact, ContactConstraint impulse)
        {
            BodySoundEffectParams param = null;

            if (!CheckParams(body, ref param))
                return;

            if (param.ContinuousPlay )// not meant for collision
                return;

            // make sure its striking it.. not resting on it?   make sure vel is accurate.. turn up gravity to see..
            //NOTE objects resting and awake on ground will show a LinearVelocity.Y , its it not averaged.
            Vector2 relativeVelocity = body.LinearVelocity - otherBody.LinearVelocity;

            Vector2 dir = new Vector2(-impulse.Normal.Y, impulse.Normal.X);
            float speedInImpulseDirection = Math.Abs(Vector2.Dot(relativeVelocity, dir));
            float speedInTangentDirection = Math.Abs(Vector2.Dot(relativeVelocity, impulse.Normal));

            if (speedInImpulseDirection > 0.2) //use  relative vel. swords on boats were noisy..
            {
                speedInImpulseDirection *= 1.001f;  //??? what is this for .
            }
            
            if (speedInImpulseDirection < param.MinVelocityBody || 
               speedInImpulseDirection < param.RelVelocityNormalMin  ||
                speedInTangentDirection < param.RelVelocityTangentialMin
                )  // use relative vel for now..  example swords  resting on boats
                return;
      
            //TODO projection of vel  in direction of normal  ..doo this look at wind and cehck rocks.
            //  &&
            //  (param.MinVelocityImpactingBody == 0 ||  otherBody.LinearVelocity.Length() > param.MinVelocityImpactingBody)
            //) //this is for static body like ground or wood being walked on..
            // TangentImpulse doesn't exist on Farseer 3.2
            //if (impulse.TangentImpulses[0] > .5)
            {
             //TODO   //make slinding  noise base on friction of objects
                //sssss for smooth.
                // rrrr for rough
            }
            //taken from Farseer Breakable Body // find max impulse from manifold.
            float maxImpulse = 0.0f;
            int count = contact.Manifold.PointCount;
            float maxTangentialImpulse = 0.0f;
  
            for (int i = 0; i < count; ++i)
            {
                maxImpulse = Math.Max(maxImpulse, impulse.Points[i].NormalImpulse);
                maxTangentialImpulse = Math.Max(maxTangentialImpulse, impulse.Points[i].TangentImpulse);
            }

            //ContactSover.Solve TODO  double check debug view might give a better contact Vector2 start location, this seems like one.
            //there are up  two Vector2s of contract on polygon face.
            Vector2 ContactPoint = impulse.BodyA.GetWorldPoint(ref impulse.LocalPoint);
          //  Debug.WriteLine("world Vector2a" + ContactPoint);
       //     Debug.WriteLine("body bos" + body.WorldCenter);   // for ground we dont want static body.. want contact Vector2
         //   ContactPoint = body.WorldCenter;  
           // impulse.Normal;
       
            double vibrationStrikeFactor = 1.0;

            if (body.PartType == PartType.Weapon)
            {
                CalcRodVibrationStrikeFactor(body, impulse.Normal, ref  vibrationStrikeFactor);

                if (vibrationStrikeFactor < 0.8f
                   //don't sound on soft object like body parts.. unless hit with very flat of sword
                    &&otherBody.PartType != PartType.Weapon
                    && otherBody.PartType != PartType.Stone
                    && otherBody.PartType != PartType.Container
                    && otherBody.BodyType != BodyType.Static  //ground
                    )//TODO   fix so that if weapon hit groudn at certain angle at certain impulse , make noise.
                    return;
            }


            maxImpulse *= (float)(vibrationStrikeFactor * vibrationStrikeFactor);

            //      if (body.PartType == PartType.Weapon && otherBody.PartType != PartType.Weapon)//TODO   fix so that if weapon hit groudn at certain angle at certain impulse , make noise.
            //        return;

            //TODO sword scrape on stone?  ImpulseTangentialMax
            if (maxImpulse > param.ImpulseNormalLimit && maxTangentialImpulse < param.ImpulseTangentialMax
                && maxTangentialImpulse > param.ImpulseTangentialMin)
            {
                //TODO do a scrape/ run sound, using ImpulseTangentialMax.. might be a separte key.
                string soundKey = param.StreamName;
                Vector2 toEar = GetDisplacementFromEar(ContactPoint);
                float dist = toEar.Length();

                if (param.VolumeDistanceLimit == 0 || dist < param.VolumeDistanceLimit)// 0 is the default
                {
                    // pan sounds left of right based on position.
                    //pan dist fact 
                    //pan is from   -1 to 1
                    float panFactor = toEar.X * param.PanXFactor;                
                //    Debug.WriteLine("PanFactor: " + PanFactor );
                       {
                        //        Debug.WriteLine("maxNimpls: " + maxImpulse +  "body vel" +  body.LinearVelocity.Length() +  " vel other  " + otherBody.LinearVelocity.Length()
                        //        + "maxTanimpls: " + maxTangentialImpulse);
                        //TODO  repeat code, consolidate..
                        float volume = param.Volume;
                        volume = Math.Max(0, volume - (dist * param.VolumeDistanceFactor));
                        float impulseVolFactor = 1;
                        //     if (param.ImpulseNormalFactor != 0)  //TODO try a linear factor to change slope.
                        {
                            float cut = 0;
                            if (maxImpulse < param.ImpulseNormalForMax)
                                cut = ((param.ImpulseNormalForMax - maxImpulse) / param.ImpulseNormalForMax);

                            volume -= cut;                  
                        }

                        float pitchShiftFactor = (float)param.PitchShiftFactor;

                        if (param.SpeedTangentialPitchFactor != 0) 
                        {
                            pitchShiftFactor += speedInTangentDirection / param.SpeedTangentialForMaxPitch;
                            pitchShiftFactor *= param.SpeedTangentialPitchFactor;
                        }

                        float totalVolume = impulseVolFactor * volume;

                        if (totalVolume < 0.05f)//TODO tune this..
                            return;
                 
                        if (param.BodyAnglePitchFactor != 0)
                        {
                            pitchShiftFactor += param.BodyAnglePitchFactor * (float)Math.Sin(body.Angle);  //TODO add a random for rock?
                        //    Debug.WriteLine("pitchShiftFactor: " + pitchShiftFactor );
                        }

                        if (param.PitchFactorDeviation != 0)
                        {
                            pitchShiftFactor = MathUtils.RandomNumber(-1, 1);
                            pitchShiftFactor *= param.PitchFactorDeviation;
                                //replay? 
                        }
                                        
                           
                        int bodyID = param.OneEffectPerBody ? body.GetHashCode() : 0;
                        AudioManager.Instance.PlaySoundEffect(soundKey, totalVolume, panFactor, pitchShiftFactor, false, bodyID);

#if MP3
                        AudioManager.Instance.Pan(soundKey, panFactor);
                        AudioManager.Instance.PitchShift(soundKey, pitchShiftFactor);                      
                        if (!AudioManager.Instance.IsPlaying(soundKey))  // is this needed?
                        {
                            AudioManager.Instance.PlaySound(soundKey, totalVolume);//TODO possible breakup here.. maybe need to ramp volume..
                        }

#endif
                    }

                    //    param.ImpulseNormalFactor * maxImpulse
                }
            }
            //  TODO frictino noises ? depend on coff?  do this.. try sliding friction noise..
            //angular vel factor?
            //Ground should have a noise too..  
            //BulletHit
            //due to density.. and magniture of ContactImpulse

        }

        /// <summary>

        /// Determine the direction of sword.    Used  projection along target .   so a direct strike along lenght will make less noise.
        /// For when a sword strikes ground or body... makes noise unless stabbed directly..

        /// </summary>
        /// <param name="body"></param>
        /// <param name="impulse"></param>
        /// <param name="vibrationStrikeFactor"></param>
        protected void CalcRodVibrationStrikeFactor(Body body, Vector2 impulseNormal, ref double vibrationStrikeFactor)
        {
            if (body.AttachPoints.Count != 1 || body.SharpPoints.Count != 1)//only valid for swords at the moment. 
                return;

            Vector2 dirVec = body.AttachPoints[0].WorldPosition - body.SharpPoints[0].WorldPosition;

            //     Debug.WriteLine("contact Normal:" + impulseNormal.ToString());

            double strikeAngle = MathUtils.VectorAngle(ref impulseNormal, ref dirVec);  //TODO use dot products, faster?

            //     Debug.WriteLine(" strikeAngle:" + strikeAngle.ToString());

            vibrationStrikeFactor = Math.Abs(Math.Sin(strikeAngle));

            //    Debug.WriteLine("contact vibrationStrikeFactor:" + vibrationStrikeFactor.ToString());

        }

        private Vector2 GetListenerVelocity()
        {
              return   (_listenerHead != null)  ? _listenerHead.LinearVelocity : _listenerSpirit.MainBody.LinearVelocity;
        }

         private Vector2 GetDisplacementFromEar(Vector2 soundSourceLoc)
        {
             Vector2 vectorToEar = Vector2.Zero;

            if (_listenerSpirit == null)
                return vectorToEar;

            Vector2 earPosition;

            if (_listenerHead != null)
                earPosition = _listenerHead.WorldCenter;
            else
                earPosition = _listenerSpirit.WorldCenter;

            //TODO 
            //sound  distort during  or impact   to head  Seizure?  yes...
            //    Vector2 disp = earPosition - body.Position;

            vectorToEar = soundSourceLoc - earPosition;

            //todo this will pan back and forth during spin 
            //since head and go left  and forth.. use main body position for now..
            //TODO   when  head body  rolling upside down.. change angle.. noise source should be pannning sin like as head roll
            //   vectorToEar=  _listenerSpirit.MainBody.GetLocalVector(ref vectorToEar);  //donest sound good since camera is not moving..
        
            return vectorToEar;
         }

        private Vector2 GetDisplacementFromEar(Body body)
        {
            return GetDisplacementFromEar( body.WorldCenter);
        }


        //intensity increase  creating illusory rise in pitch  as approach.. 

        float GetDopplerEffectPitchShiftFactor(Body body, out float dir)
        {

            dir = 0;

            Vector2 vecBodyFromEar = GetDisplacementFromEar(body);
            if (vecBodyFromEar.Length() == 0)
                return 1;


            //TODO not sure if this is correct... i should get a negative value when moving away..
           //frequencyobserved/ frequencysource = ( velsound/ ( velsound - velsource cos (th)))  this is approach angle.
            //cos th =    A  . B / |A||B|    where th is  angle between two vectors.  A is velsource..
            //below is simplified since body.LinearVelocity.Length() cancels out
            
            //If only B  is a unit vector, then the dot product A dot B  gives , i.e., the magnitude of the projection of A  in the direction of B, with a minus sign if the direction is opposite. This is called the scalar projection of  onto , or scalar component of  in the direction of  (see figure). 

            Vector2 B = vecBodyFromEar;          
            B.Normalize();
            dir = Vector2.Dot(body.LinearVelocity, B);           


            float factor = Vector2.Dot(body.LinearVelocity, vecBodyFromEar) / vecBodyFromEar.Length();

#if PRODUCTION
            if ( body.LinearVelocity.Length()> SpeedofSound)
            {
                   Debug.WriteLine("body is supersonic... doppler effect is maybe wroing"+ body.LinearVelocity.Length().ToString());
            }
         
#endif

            return ((SpeedofSound - factor) / SpeedofSound);

        }


        bool CheckParams(Body body, ref BodySoundEffectParams param)
        {

            param = body.SoundEffect;

            if (param == null || param.StreamName == null)
                return false;

            FixDefaultValues(param);

            return true;

        }


        void PlayObjectContinuousSound(Body body)
        {
            //TODO consolidate with contact sound maybe.
            BodySoundEffectParams param = null;

            if (!CheckParams(body, ref param))
                return;

            if (param.ContinuousPlay == false)
                return;

            Vector2 distFromListener = GetDisplacementFromEar(body);
            string soundKey = param.StreamName;

            //TODO angular and linear vel factor or phaser pitch shift since of angular position or something

            Vector2 toEar = GetDisplacementFromEar(body);
            float dist = toEar.Length();

            float pitchShift = 0;

            if (dist < param.VolumeDistanceLimit && (body.LinearVelocity - GetListenerVelocity()).Length() > param.MinVelocityBody)
            {

                if (param.DopplerFactor != 0)
                {
                    float dir = 0;
                    pitchShift = GetDopplerEffectPitchShiftFactor(body, out  dir) * param.DopplerFactor;


                    pitchShift *= Math.Sign(dir); //  dont know why //TODO recheck GetDopplerEffectPitchShiftFactor.. formaul.. this is close enough.
                                      
                                

                    //   System.Diagnostics.Debug.WriteLine("pitchshift" + pitchShift);
                    //  AudioManager.Instance.PitchShift(soundKey, pitchShift * param.DopplerFactor);
                    //TODO this should be a resample , change Tempo and pitch but dont have that method.
       
          
                    //NOTE ..SPECIAL CASE this is a special treatment just  for bullet and nothing else.. don't make the sound if its moving away from us  ( just fired from gun).. unless with silencer.. would be covered up by noise of gun
                    //should actually make gun loudner.. and whiz softer.. but.. not possible with the dynamic range we have.
                    if ((body.Info & BodyInfo.Bullet) != 0)
                    {
                        if (dir > 0 && !AudioManager.Instance.IsPlaying(soundKey))  // if already playing means might have just passed our head.. dir > 0 means moving away from
                            return;
                    }
                }


                float PanFactor = toEar.X * param.PanXFactor;
                float volume = param.Volume - (dist * param.VolumeDistanceFactor);
                //TODO need to taper off sounds ( was true with ogg) .. this causes breakup.  I fade in and our each continuous sound now, it helps
                //or  use seamless playlist.. 
                if (!AudioManager.Instance.IsPlaying(soundKey))
                {
                    AudioManager.Instance.PlaySound(soundKey, volume);
                    // System.Diagnostics.Debug.WriteLine("restarted sound");
                }
                else
                {
                    AudioManager.Instance.SetVolume(soundKey, volume);
                }////TODO may need to ramp this down or there is breakup.   not implement for bullet 

                AudioManager.Instance.Pan(soundKey, PanFactor);
     			if (param.DopplerFactor != 0)
                {
                              
                    AudioManager.Instance.PitchShift(soundKey, pitchShift);
                }
          

         
            }
            else
            {
                AudioManager.Instance.SetVolume(soundKey, 0f);
            }

        }

        public override void Update(float dt)
        {
            if (_listenerSpirit == null)
                return;

            //TODO sounds all  distort during  or impact   to head Seizure or bad state? 
            //TODO consider make  more general sounds based on material , flux, air medium..

            foreach (Body body in World.BodyList)
            {
                if (!body.Enabled || body.SoundEffect == null)
                    continue;

                try
                {
                    PlayObjectContinuousSound(body);
                }

                catch (Exception exc)
                {
                    Debug.WriteLine(" exception in PlayObjectContinuousSound on bodyinfo" + body.Info.ToString() + exc.Message);
                } 
            }
        }


    }

}








