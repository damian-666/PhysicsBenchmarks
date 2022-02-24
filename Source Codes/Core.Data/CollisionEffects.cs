using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Dynamics.Contacts;
using FarseerPhysics.Dynamics.Particles;
using Farseer.Xna.Framework;
using FarseerPhysics.Dynamics.Joints;
using Core.Data.Entity;
using Core.Data.Geometry;
using System.Diagnostics;
using FarseerPhysics.Common;
using FarseerPhysics.Common.PolygonManipulation;
using FarseerPhysics;

namespace Core.Data
{
     public class CollisionEffects
    {

        static public bool OnCollisionEventHandler(Fixture fixtureA, Fixture fixtureB, Contact contact)
        {
            return OnCollisionEventHandler(fixtureA, fixtureB, contact, null);
        }

        const float minRelVelForHurtingOrganSq = 1.4f * 1.4f;  // meters per sec so that bullet falling lightly in chest doesnt kill?


        //TODO consider.. using AfterCollision for this.    for bullets is more accurate.   

        //but poking a board on the ground should penetrate. more than poking onE handing from a string.YES .. POST SOLVE..   TODO 

        //TODO .. through up some dirt when bullet hits ground.  //TODO POST SOLVE..
        static public bool OnCollisionEventHandler(Fixture fixtureA, Fixture fixtureB, Contact contact, Spirit sp)
        {

            //TODO should do AfterCollision , will have the impulses, hit pt and normasl all calc, not so much need to ray cast..
            try
            {
                // fixtureB is always from external object striking the body we are listening to A.  according to Farseer documentation on OnCollision          
                Body struckBody = fixtureA.Body;

                if ((struckBody.Info & BodyInfo.Bullet) != 0)  //bullet already lodged in was hit.  //TODO maybe push it further or break or cut.    //TODO FILTER THOSE OUT.. OR USE A VIEW.. AT LEAST FOR SOME INSIDE LIEK MAIN BODY..
                    return true;


                //TODO mark or cut snow/ ice.
                //    if (struckBody.IsStatic && struckBody.IsFlagged(BodyInfo.Building))  //if airship hits building hard enough move it.
                //    {
                //  make it dynamic..//test accel.. like wind drag.
                //    }

                if (struckBody.IsStatic || struckBody.Density >= 400    //todo .. allow stick in ground or snow..  
                    || struckBody.PartType == PartType.Weapon || //TODO put a hardness on Body.  load up with this heuristics.
                    (struckBody.Info & BodyInfo.ShootsProjectile) != 0)  // gun blocks bullet
                    return true;

             
                //TOOD test hitting board , should make noise..

                Body strikingBody = fixtureB.Body;

                //TODO consider best general cut formula is in here:
             //   https://www.quora.com/What-would-happen-if-a-grain-of-sand-hit-me-in-the-chest-at-200-000-miles-per-hour
              

                if ( strikingBody.IsInfoFlagged(BodyInfo.Bullet))
                {

                    if (strikingBody.JointList != null) /// should fix bug in video... bullet stuck to part of neck..   then it collides with other section of neck  on action.. can sever head.  so any stuck bullet ( joined to body)  cannot cut anything..
                        return true;
                    //TODO  .. kick up some direct particles.
                    //TODO make a noise based on hitting ground.. 
                    //static bodies like ground do not listen for collisions.. so do this here.ccc              
                }


                if (strikingBody.IsInfoFlagged(BodyInfo.Bullet) && ( strikingBody.SharpPoints == null || strikingBody.SharpPoints.Count == 0))
                {
                    SharpPoint sharpt = new SharpPoint(strikingBody, strikingBody.LocalCenter);
                    sharpt.Direction = strikingBody.GetLocalVector(strikingBody.LinearVelocity);
                }


                 if (strikingBody.SharpPoints != null)
                {
                    // Find the sharp points of the colliding body 
                    foreach (SharpPoint sharpPoint in strikingBody.SharpPoints)
                    {
                        Vector2 contactNormal = struckBody.GetWorldVector(contact.Manifold.LocalNormal);
                        Vector2 contactPointWorld = struckBody.GetWorldPoint(contact.Manifold.LocalPoint);              //there are one or two points on each collision.. on a bullet there are so close pick the first.              
                        Vector2 intersection = contactPointWorld;  //NOTE  this is not correct at various target test with simple board  gives the middle.. so we use the ray improveContactAccuracy
                        Vector2 normalAtCollision = contactNormal;
                        Vector2 sharpPointWorld = sharpPoint.WorldPosition;

                        Vector2 relativeVel = strikingBody.LinearVelocity - struckBody.LinearVelocity;
                        if ((strikingBody.Info & BodyInfo.Bullet) != 0)
                        {
                            //TODO TODO using AfterCollision gives the impulse after the solver does its work.
                            //for bullets it barely makes a difference tho.

                            // but hitting a trapped object can smash it or penetrate more easily than a floating one,
                            //and this doenst not take this into account.
                            //After the contracts might be more accurate.

                            strikingBody.CollisionGroup = 0;// this is so bullets will not pass through each other.. they have the gun collide ID - for avoiding collide with gun
                            // this is a farseer issue.. NormalImpulse is always zero.. has to do with warmstarting, i think fixed an workaroudn on forum.  
                            //ill just do this for now.
                            //NOTE tried the Store Impulses.. did not work.   does not give the impulse untill after solivng i think.
                            //Both these points are very close.
                            //however contact point is not the same as given by ray.                  

                            #region improveContactAccuracy
                            //both contact and normal might be off.. use a ray
                            //hard to factor out this region.. auto factor doesnt work due to lamba

                            float speedTowardBone = relativeVel.Length(); //NOTE  approximate.. find dont need aroudn for rapidly spinning targets                   

                            Vector2 normalFlipped180 = new Vector2(-contactNormal.X, -contactNormal.Y); //normalAtCollision   // normalAtCollision is not correct in test after neck fill

                            float contactImpulse = Vector2.Dot(relativeVel, normalFlipped180
                                ) * strikingBody.Mass;  //TODO compare with stored impulse  ( NOTE due to bug in farseer , stored impulse in this is zero.  strange because its filled if warmstaritn, TOI clears it though..  Debug.WriteLine("impulse" +  contact.Manifold.Points[0].NormalImpulse);


                            //Note when body is struck mid torso with 9mm machine gun .. the impulse is -4..


                            if ( strikingBody is Particle)
                            {
                                contactImpulse *= 2;  //just tuning shot gun quickly..  string to use air to disperse pellets..  //TODO REMOVE and  RETUNR.. 

                            }


                            if (contactImpulse > PoweredJoint.MinImpulseForBoneBreak ||   //  this would account for angle of incidence.. need storing impulses tho..
                              Math.Abs(contactImpulse) > Body.MinBulletImpulseForBoneShellPenetration)
                            {

                                const float rayExtensionFactor = 1.5f;  //make x factor longer..  in case of acceleration
                                Vector2 displacementPerFrame = strikingBody.LinearVelocity * World.DT * rayExtensionFactor;
                                const float minDistPerFrame = 1f;//  60 meters/ sec  (TODO less for scars on sharps or bruisues if we do that..)

                                if (displacementPerFrame.LengthSquared() > minDistPerFrame * minDistPerFrame)// don't do ray if length == zero, cause tree exception.    or too slow dont bother
                                {
                                    Vector2 startWorldPosition = sharpPointWorld;
                                    
                                    Body bodyIntersected = null;

                                    //TODO fix marks for sword and maybe bruises here. mabye  rid of post solve   
                                    //first check contact points I think they are wrong.
                                    //TODO Draw entrance hole

                                    // the collide point is not exact?   so we do a ray cast.. ( TODO check.. it might be) 
                                    World.Instance.RayCast((fixture, point, normal, fraction) =>
                                    {
                                        if (!fixture.IsSensor
                                            //  && fixture.Body.BodyType != BodyType.Static     // ignore static ( ground ) bodies for now..
                                            && !fixture.Body.IsNotCollideable  //in case of tool. these are in the tree
                                            && (fixture.Body.Info & BodyInfo.Cloud) == 0  // ignore clouds they detect objects and break on their own, arent cut  (TODO future.. could be.. or do an iField on bullet) to blow pieces around
                                               && (fixture.Body.Info & BodyInfo.Bullet) == 0  // ignore bullets we might have shot into it already,  they detect objects and break on their own, arent cut  (TODO future.. could be.. or do an iField on bullet) to blow pieces around
                                            )
                                        {
                                            bodyIntersected = fixture.Body;
                                            intersection = point;
                                            normalAtCollision = normal;
                                            return fraction;
                                        }
                                        return -1.0f;     // ignore this fixture, next please

                                    }, sharpPointWorld, sharpPointWorld + displacementPerFrame);


                                  
                                    // this could be inside the fixture .. don want that since we will make an entrance wound. 
                                    if (struckBody != bodyIntersected)
                                        continue;

                                    contactPointWorld = intersection;
                                    // CreateMarkPointAndView(ourBody, strikingBody, contactCollisionPointWorldAdjusted, contactWorldNormal, contactImpulse, closestSharp != null, true);

                                    normalFlipped180 = new Vector2(-normalAtCollision.X, -normalAtCollision.Y); //normalAtCollision   // normalAtCollision is not correct in test after neck fill
                                    contactImpulse = Vector2.Dot(relativeVel, normalFlipped180 ) * strikingBody.Mass;  //TODO compare with stored impulse
                            #endregion

                                //TODO use hardness
                                float minBulletBreakDistance = 0.06f;//this cuts him up pretty easily.. more so than joint sensor which it can easily bounce off undetected
                                    
                                if (Is50mmBullet(strikingBody))
                                {
                                        minBulletBreakDistance = 0.32f; //this is a very inaccurate 
                                    }
                                else
                                if (Is9mmBullet(strikingBody))  // less than 100g bullet is the 9mm.. its highest vel , smaller so, cuts better ., less stoping power tho.  //TODO implememnt cutting.   Now wounding the arm does not help, protects the body..
                                {
                                    minBulletBreakDistance = 0.12f; //this is a very inaccurate  machine gun make it chop him up more easily tha handgun,  for now.
                                }else
                                if ( strikingBody is Particle)
                                {
                                    minBulletBreakDistance = 0.09f;  // there are so many pellets flying around.. dont want this too high; TODO check.
                                }
                                    //  else //slug..marked  by mass

                          
                                 if (contactImpulse > PoweredJoint.MinImpulseForBoneBreak)
                                    {

                                        Joint nearbyJoint = Spirit.GetFirstJointWithinDistance(struckBody, ref contactPointWorld, minBulletBreakDistance * minBulletBreakDistance, true, BodyInfo.Bullet);
                                        if (nearbyJoint != null)
                                        {
                                            nearbyJoint.Break(); 

                                       //     if (   !(struckBody.PartType == PartType.Head || struckBody.PartType == PartType.MainBody)    )// allow to cut and break the joint.. TODO check this.
                                        //        continue;      //TODO check with cutting..keep going an penetrate if not a large body... possibly one bullet continues and cuts through 2 necks. TODO.. that could be cool.. needs to be on POST SOLVE.
                                        }
                                    }

                                    if (Math.Abs(contactImpulse) > Body.MinBulletImpulseForBoneShellPenetration)  //TODO apply hardness..
                                    {                 
                                        //TODO SHOTGUN OR ALL GUNS if near other embedded bullet then cut further.  ( also to ignore bullet  fixture on cutting)

                                        float cutDist = 0.04f;  //just the wrist distance.. big bullet cannot cut well.


                                        Joint jointToBullet = Spirit.GetFirstBulletJointWithinDistance(struckBody, ref contactPointWorld, minBulletBreakDistance* minBulletBreakDistance,  BodyInfo.Bullet);


                                        float cutFactorHit = 1f;
                       

                                        if (Is50mmBullet( strikingBody))
                                        {
                                            cutDist = struckBody.Density > 400 ? 0.3f : 0.6f;  //  dont cut stone so easily TODO a  linear function based on this. 
                                        }
                                        else
                                            if (struckBody.IsInfoFlagged(BodyInfo.SeeThrough)) //TODO use hardness
                                            {
                                                cutDist =0.4f;     //can shoot through rocketplane glass
                                            }
                                            else
                                            if (Is9mmBullet(strikingBody))
                                            {
                                                 cutDist = MathUtils.RandomNumber(0.10f, 0.17f);      //10 cm for now can cut a leg at thigh.. TODo random.. or when near other bullets... TOdo.. also alllow cut to ignore bullets types.
                                            }else
                                            if (strikingBody is Particle)  //TODO if particles near others embedded,  extend the cut dist..   so at point blank it will cut.. to bits..
                                            {

                                            Debug.WriteLine("contactImpulse" + contactImpulse  + strikingBody.Mass + " " + strikingBody.LinearVelocity);


                                            //simple GetFirstJointWithinDistance  ///but for the mark

                                            if ( strikingBody.LinearVelocityPreviousFrame.Length() > 300f )// && relativeVel > ?  for now determine if at pt blank range..
                                                     cutDist = MathUtils.RandomNumber(0.40f, 0.50f);   //TODO check for strik marks.... or softness body a bit..but thats not ok for large main body..
                                                else
                                                    cutDist = MathUtils.RandomNumber(0.20f, 0.40f);
                                                
                                                     
                                            }else
                                            if (IsSlug(strikingBody))
                                            {
                                            cutDist = MathUtils.RandomNumber(0.15f, 0.24f);
                                            Debug.WriteLine("contactImpulseSlug" + contactImpulse + strikingBody.Mass + " " + strikingBody.LinearVelocity);
                                            }

                                         
                                        //TODO slug

                                        //    cutDist = 5f; //testing


                                        if (jointToBullet != null)  //TODO should use the penetration pt.. but we will redo this is marks..for now just weakening..
                                        {
                                            if (strikingBody is Particle)  //TODO when these are cut marks and we can see them  .. fix this.. the more emdedd on the line, the weaker.  .. two shoots of bird shot shold cut it to bits..
                                                cutFactorHit *= 4;

                                        }


                                        cutDist *= cutFactorHit;

                                        //TODO if dist to line of existing embedded bullet then extend cut *# bodies using  disttoline .. can shoot apart toso..
                                        //Or.. wait till gen3..

                                        //TODO IMPORTANT fix GeneralVerteces on main body for cut at spine to work.   ( consider this) 

                                        //  or.. better if main body and hitting X deep then entry point to intersection with line from sides.   that way lungs show..
                                        //   or intent a bid .. will keep blood out of lungs.. but how to line up with dress.. , side by side♠s

                                        //TODO if hit bullet.. keep going but allow to cut further.. maybe move joint inward.   if outside break and cut.
                                        //allow shooting a few to cut bodies.

                                        //if body contains x bullets near each other increase cut dist..

                                        if  (  
                                             #if SIMPLECUT   ///TODO erase and COMMIT
                                              struckBody.FixtureList.Count() ==1  ||  IsEnemyNewBiped(struckBody)    
#else
                                              true
#endif
                                                // only cut simple bodies for now.    will break other fixtures apart .                                                                    
                                                //    &&
                                       //     struckBody.JointList == null         //this is due to the stay fixture issue ... i think its fixed now that invalid bodies are not created.          
                                        //TODO i think blood on stray fixture comes from regrowing hidden thing, after hit again, think through this..
                                        //shoot an arm, wait for regrow , shoot again.
                                        )
                                        {
                                            Vector2 relVelNormal = relativeVel;  //by the time we get this the bullet has bouced off.. thsi is WAY tooo complex a method.. are we
                                           //event using the right event?   should be soon before it bounces of.. might be a ton easier and less rays needed

                                            relVelNormal.Normalize();

                                            Vector2 cutLine= relVelNormal * cutDist*2;

                                            Vector2 cutStart = contactPointWorld + normalAtCollision * 0.02f; //to avoid the test inside.. in the target object.  would be very rare for a bullet to be right at the object being hit via CCD, but TODO ignore bullet body on cut.                                        
                                                                      
                                            //contactPointWorld might be in the bullet or the struck body..                        
                                            //TODO only cut bones or body with certain info?
                                            //TODO cut bones lying on ground.. could fail due to exit poitn not ignoring static body.

                                            //CUT is not very complete.
                                         

#if SIMPLECUT
                                            if (CuttingTools.Cut(World.Instance, cutStart, cutStart + cutLine))
                                            {
                                                JointEdge je = struckBody.JointList;

                                                while (je != null)  //break all the attached joints
                                                {
                                                    je.Joint.Break();
                                                    je = je.Next;
                                                }
                                                return true;
                                            }  
                                                                                  
#else
                                                                               
                                              //todo bleed better , put mark on shot.                                          
                                             if (CuttingTools.CutComplex(World.Instance, cutStart, cutStart + cutLine, struckBody)) //todo get splitter here.
                                             {
                                                 //TOOD test remove marks and points
                                                 //TODO add nourishment, 
                                                 //TODO make edible bits of flesh.. add grap points... maybe.. if shape small and has blood emitter or marked flesh.      
                                                return true;
                                            }                                              
#endif
                                          }

                                        //TODO improve damage..  add cut mark.  for cut with multiple shots.

                                        //TODO bleed out more from wound.. maybe collidable with arm.. and dripping..

                                        Vector2 bleedLocationWorld;
                                        short collideID = (sp == null ? (short)0 : sp.CollisionGroupId);
                                        if (struckBody.AttachProjectileInsideBody(contactPointWorld, normalAtCollision, contactImpulse, strikingBody
                                            , collideID, out bleedLocationWorld))
                                        {
                                            if (sp != null && sp.IsMinded)
                                            {
                                                Vector2 contactPointLocal = struckBody.GetLocalPoint(bleedLocationWorld);
                                                BleedFromWound(sp, struckBody, ref normalAtCollision, contactPointLocal, false);
                                                sp.HandleSharpPenetrationDamage(struckBody, contactPointWorld, null, (strikingBody.Info & BodyInfo.Bullet) != 0);
                                                //TODO tissue damage hole
                                            }
                                        }
                                    }
                                    //TODO cutting.. exit wound .. skin and bone particles..etc.
                                    //rock particles .. snow  .. depends on angle and static body struct.. important for seeing bullets.. scale to view
                                }
                            }
                        }
                        float minRelVelForHurtingOrganSq = ((strikingBody.Info & BodyInfo.Bullet) != 0) ? 3f * 3f : 0.5f * 0.5f;//bullet falling gently to lung shoud not cause death. NOTE .. after bullet shot, the sharp is removed after 1 sec or so, less worries here
                        if (relativeVel.LengthSquared() > minRelVelForHurtingOrganSq)
                        {
                            //any sharp including  sword
                            HandleSharpPenetrationToNearRefPoints(sp, struckBody, strikingBody, sharpPoint, ref contactPointWorld, ref normalAtCollision);
                        }
                    }
                }
            }

            catch (Exception exc)
            {
                Debug.WriteLine(" exc in joint collide " + exc.Message);
            }

            return true;
        }

   //lets allo this type to have its main body cut.. have to shoot right accross spine with machien gun or with 50mm
        private static bool IsEnemyNewBiped(Body struckBody)
        {
            return struckBody.FixtureList.Count() == 5
                                                           && struckBody.PartType == PartType.MainBody &&

                                                            Level.Instance.MapBodyToSpirits[struckBody].PluginName.ToLower().Contains("swordsman");
        }

        //better way would be verts 1 and 3, 2, and 4 pairs have y values that look like the bullet width
        //a general formula for dist based on cross width, vel, mass as per equation
        private static  bool Is9mmBullet(Body strikingBody)//TODO more on this later.. only small bullet cuts now.
        {
            return (strikingBody.Mass < 0.1f)  ;// less than 100g or 0.1Kg bullet is the 9mm.. its highest vel , smaller so, cuts better ., less stoping power tho.  //TODO implememnt cutting.   Now wounding the arm does not help, protects the body..
        }


        private static bool IsSlug(Body strikingBody)//TODO more on this later.. only small bullet cuts now.
        {
            return (strikingBody.Hardness == 1.5f);  /// the first to use this .. it just lead;
        }


        private static bool Is50mmBullet(Body strikingBody)
        {//better way would be verts 1 and 3, 2, and 4 pairs have y values that look like the bullet width
            return (strikingBody.Mass > 0.4f);// this is a cannon with armor piece, cuts and or blows away everything..
        }
        private static void HandleSharpPenetrationToNearRefPoints(Spirit sp, Body struckBody, Body strikingBody, SharpPoint sharpPoint, ref Vector2 contactPointWorld, ref Vector2 normalAtCollision)
        {

            if (!(struckBody.PartType == PartType.MainBody || struckBody.PartType == PartType.Head))
                return;


            //TODO fix Zorder.. 
            if (!strikingBody.IsStatic && strikingBody.ZIndex <= struckBody.ZIndex)  //some sharp part of terrain is static like stalagmite "mine Level" and behind some other ground..  dont want to move it to foreground
            {
                strikingBody.ZIndex = struckBody.ZIndex + 1;// TODO this doesn't work when since the dress is above.. need to fix it carefully . maybe body above dress thing
                Level.Instance.CacheUpdateEntityView(strikingBody);
            }

            foreach (AttachPoint atc in struckBody.AttachPoints.Where(x => x.IsTarget))//these are attach points so they bullet or knife can get stuck in 
            {
                HandleOrganDamageNearRefPoint(sp, struckBody, strikingBody, sharpPoint, ref contactPointWorld, ref normalAtCollision, atc);
            }

        }

        private static void HandleOrganDamageNearRefPoint(Spirit sp, Body struckBody, Body strikingBody, SharpPoint sharpPoint, ref Vector2 contactPointWorld, ref Vector2 normalAtCollision, AttachPoint atc)
        {

            //todo SHOTGUN.. A FEW BBS IN BRAIN DOES NOT KILL..
            if (sp == null)
                return;

            bool isHeart = (atc.LocalPosition.Y < 0.8f * sp.SizeFactor);// above waist ( TODO .. test scale factor)  .. or put flags

            if (isHeart)
            {
                atc.Flags |= AttachPointFlags.IsHeart;  //TODO should be set in designer 
            }

            float organRadius = ((strikingBody.Info & BodyInfo.Bullet) != 0) ? 0.052f : 0.032f;  //bigger if bullet tearing through// TODO put bullet path mark and set if intersection

            if ((atc.WorldPosition - sharpPoint.WorldPosition).LengthSquared() < organRadius * organRadius)
            {
                AttachPoint atcStuckPoint = new AttachPoint(strikingBody, sharpPoint.LocalPosition);
                strikingBody.AttachPoints.Add(atcStuckPoint);
                atcStuckPoint.Flags |= (AttachPointFlags.IsTemporary | AttachPointFlags.CollideConnected);// they are  in body empy space to hold blade.. so as not to allow object like a sword to rotate into the fixture to which it is attached.

                if (isHeart)
                {
                    //TODO possible delay, break after a time.  add it to powered joint or a Effectcollection to level.
                    atcStuckPoint.StretchBreakpoint = MathUtils.RandomNumber(1000, 7000);  // can pull out or stays in.. could  be a prismatic joint.. but the fixture slot does that..  this help guide it into slot.    hand breakpoint is 8000, so 9000 shoudl guarntee stuck in 
                }
                else
                {//much less likely to stick in anus, its gross and can get a M rating if tester sees this anal stab and sword stuck in there, its funny tho. TODO revisit , test..  should happen rarely just for kicks or shock value
                    atcStuckPoint.StretchBreakpoint = MathUtils.RandomNumber(0, 4000);
                    normalAtCollision = atc.Direction;  // this is pointing down..   
                }

                atc.Attach(atcStuckPoint);

                Vector2 localBloodSource = atc.LocalPosition;
                BleedFromWound(sp, struckBody, ref normalAtCollision, localBloodSource, isHeart);
                ///TODO check the death and after..
                ///more blood .. ant only once.. strong  breakpoint & Ai let go maybe
                ///TODO draw order for sword with no dress
                sp.HandleSharpPenetrationDamage(struckBody, contactPointWorld, atc, false);

            }
        }

        private static void BleedFromWound(Spirit sp, Body struckBody, ref Vector2 normalAtCollision, Vector2 bleedPointLocal, bool atHeart)
        {

            if (sp.IsDeadAndCold())
                return;

            const float minDist = 0.01f;

            int numNearEmitters = struckBody.EmitterPoints.Count(x => ((x.LocalPosition - bleedPointLocal).LengthSquared()) < minDist * minDist && x.Active);

            if (numNearEmitters >= 2)  //dont put more than two blood marks (  two.. because bullet puts a hit wound aready, then this..
                return;

            if (atHeart)  //TODo adjust directions at heart in namiad then use that design direction..  right now it points down a bit..
            {
               
                //NOTE these  could have be visually designed , just find the emitter
                BodyEmitter emHeart = sp.AddNewBloodEmitter(struckBody, bleedPointLocal, Body.BulletTemporaryTag);
                emHeart.Active = true;
                emHeart.DeviationAngle = 0.03f;// not a severed lim bleed straight out from lung.. shoud spit out from gil opening really
                emHeart.Direction = new Vector2(-1, 0);
                emHeart.EmissionForce += 3.5f;  //make sure goes up when lying down to right.
                emHeart.ParticleCountPerEmission += 1;
                emHeart.Direction = new Vector2(-1, 0);
                Spirit.ApplySizeFactor(emHeart, sp.SizeFactor);
                return;
            }

            AddBloodEmittersFromBulletWound(sp, struckBody, ref normalAtCollision, ref bleedPointLocal,2);
        }

        private static void AddBloodEmittersFromBulletWound(Spirit sp, Body struckBody, ref Vector2 normalAtCollision, ref Vector2 bleedPointLocal, int count)
        {
            for (int i = 0; i < count; i++)
            {
                BodyEmitter em = sp.AddNewBloodEmitter(struckBody, bleedPointLocal, Body.BulletTemporaryTag);
                em.Direction = normalAtCollision;
                SetPropForBulletWoundEmiiter(sp, em, false);  
            }
        }

         //TODO it it hiting bullet too much.. add offset or skip bullet
        private static void SetPropForBulletWoundEmiiter(Spirit sp, BodyEmitter em, bool slow)
        {
            em.Active = true;
            em.CheckInsideOnCollision = false;
            em.CheckRayOnEmit = true;
            em.Size = slow ? 0.018f : 0.022f;
            em.DeviationSize = 0.003f;
 
            em.EmissionForce = slow ? 5.5f : 8f; //needs to at least beat gravity
            em.ZIndex = em.Parent.ZIndex + 99;
            em.ParticleCountPerEmission = slow ? 2 : 3;
            em.Frequency = slow ? 8 : 14;
            em.ProbabilityCollidable = 0.1f;
            em.AutoDeactivateAfterTime = slow ? MathUtils.RandomNumber(0.5f, 1.2f): MathUtils.RandomNumber(10, 20f)  ;  //TODO drip or fadeout..
            Spirit.ApplySizeFactor(em, sp.SizeFactor);
        }

    }

}

