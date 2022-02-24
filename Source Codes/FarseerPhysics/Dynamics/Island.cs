/*
* Farseer Physics Engine based on Box2D.XNA port:
* Copyright (c) 2010 Ian Qvist
* 
* Box2D.XNA port of Box2D:
* Copyright (c) 2009 Brandon Furtwangler, Nathan Furtwangler
*
* Original source Box2D:
* Copyright (c) 2006-2009 Erin Catto http://www.gphysics.com 
* 
* This software is provided 'as-is', without any express or implied 
* warranty.  In no event will the authors be held liable for any damages 
* arising from the use of this software. 
* Permission is granted to anyone to use this software for any purpose, 
* including commercial applications, and to alter it and redistribute it 
* freely, subject to the following restrictions: 
* 1. The origin of this software must not be misrepresented; you must not 
* claim that you wrote the original software. If you use this software 
* in a product, an acknowledgment in the product documentation would be 
* appreciated but is not required. 
* 2. Altered source versions must be plainly marked as such, and must not be 
* misrepresented as being the original software. 
* 3. This notice may not be removed or altered from any source distribution. 
*/

using System;
using System.Diagnostics;
using FarseerPhysics.Common;
using FarseerPhysics.Dynamics.Contacts;
using FarseerPhysics.Dynamics.Joints;
using Farseer.Xna.Framework;

namespace FarseerPhysics.Dynamics
{
    /// <summary>
    /// This is an internal class.
    /// </summary>
    public class Island
    {
        public Body[] Bodies;
        public int BodyCount;
        public int ContactCount;
        public int JointCount;
        private int _bodyCapacity;
        private int _contactCapacity;
        private ContactManager _contactManager;
        private ContactSolver _contactSolver = new ContactSolver();
        private Contact[] _contacts;
        private int _jointCapacity;
        private Joint[] _joints;
        public float JointUpdateTime;

        private const float LinTolSqr = Settings.LinearSleepTolerance*Settings.LinearSleepTolerance;
        private const float AngTolSqr = Settings.AngularSleepTolerance*Settings.AngularSleepTolerance;


        private Stopwatch _watch = new Stopwatch();


        public void Reset(int bodyCapacity, int contactCapacity, int jointCapacity, ContactManager contactManager)
        {
            _bodyCapacity = bodyCapacity;
            _contactCapacity = contactCapacity;
            _jointCapacity = jointCapacity;

            BodyCount = 0;
            ContactCount = 0;
            JointCount = 0;

            _contactManager = contactManager;

            if (Bodies == null || Bodies.Length < bodyCapacity)
            {
                Bodies = new Body[bodyCapacity];
            }

            if (_contacts == null || _contacts.Length < contactCapacity)
            {
                _contacts = new Contact[contactCapacity*2];
            }

            if (_joints == null || _joints.Length < jointCapacity)
            {
                _joints = new Joint[jointCapacity*2];
            }
        }

        public void Clear()
        {
            BodyCount = 0;
            ContactCount = 0;
            JointCount = 0;
        }

        private float _tmpTime;

        public void Solve(ref TimeStep step, ref Vector2 gravity)
        {
           
            // Integrate velocities and apply damping.
            for (int i = 0; i < BodyCount; ++i)
            {                                                                                                               
                Body b = Bodies[i];                             

                if (b.BodyType != BodyType.Dynamic)
                {
                    continue;
                }

                // Integrate velocities.
                // FPE 3 only - Only apply gravity if the body wants it.
                if (b.IgnoreGravity)
                {
          
                    //shadowPlay mod.. to strengthenjoints    STRENGTHENJOINT strengthen joints, the only way we found was to add mass in relation to the position error, adn even mass ratios.. an example is a rope under stress, being pulled over a smooth rock..
                  //  b.LinearVelocityInternal.X += step.dt * (b.InvMassOrig * b.Force.X);   //if its a balloon in air, we want the air forces to act as though the panels were not massive... hope this works..
                 //   b.LinearVelocityInternal.Y += step.dt * (b.InvMassOrig * b.Force.Y);
       		       // b.AngularVelocityInternal += step.dt*b.InvIOrig*b.Torque;


   					b.LinearVelocityInternal.X += step.dt*(b.InvMass*b.Force.X);
                    b.LinearVelocityInternal.Y += step.dt*(b.InvMass*b.Force.Y);
                    b.AngularVelocityInternal += step.dt*b.InvI*b.Torque;
                }
                else
                {
                    b.LinearVelocityInternal.X += step.dt*(gravity.X + b.InvMass*b.Force.X);
                    b.LinearVelocityInternal.Y += step.dt*(gravity.Y + b.InvMass*b.Force.Y);
                    b.AngularVelocityInternal += step.dt*b.InvI*b.Torque;


     //shadowPlay mod.. to strengthenjoints    STRENGTHENJOINT strengthen joints, the only way we found was to add mass in relation to the position error, adn even mass ratios.. an example is a rope under stress, being pulled over a smooth rock..
                  //  b.LinearVelocityInternal.X += step.dt * (gravity.X +  b.MassOrig * b.Force.X);   //if its a balloon in air, we want the air forces to act as though the panels were not massive... hope this works..
                 //   b.LinearVelocityInternal.Y += step.dt * (gravity.Y + b.MassOrig * b.Force.Y);

  				//	b.AngularVelocityInternal += step.dt*b.InvIOrig*b.Torque; //TODO same.. should be outside if.. but we wnat to mimize farseer diffs
  
                }

                // Apply damping.
                // ODE: dv/dt + c * v = 0
                // Solution: v(t) = v0 * exp(-c * t)
                // Time step: v(t + dt) = v0 * exp(-c * (t + dt)) = v0 * exp(-c * t) * exp(-c * dt) = v * exp(-c * dt)
                // v2 = exp(-c * dt) * v1
                // Taylor expansion:
                // v2 = (1.0f - c * dt) * v1
                b.LinearVelocityInternal *= MathUtils.Clamp(1.0f - step.dt*b.LinearDamping, 0.0f, 1.0f);
                b.AngularVelocityInternal *= MathUtils.Clamp(1.0f - step.dt*b.AngularDamping, 0.0f, 1.0f);
            }

            // Partition contacts so that contacts with static bodies are solved last.
            int i1 = -1;
            for (int i2 = 0; i2 < ContactCount; ++i2)
            {
                Fixture fixtureA = _contacts[i2].FixtureA;
                Fixture fixtureB = _contacts[i2].FixtureB;
                Body bodyA = fixtureA.Body;
                Body bodyB = fixtureB.Body;
                bool nonStatic = bodyA.BodyType != BodyType.Static && bodyB.BodyType != BodyType.Static;
                if (nonStatic)
                {
                    ++i1;

                    //TODO: Only swap if they are not the same? see http://code.google.com/p/box2d/issues/detail?id=162
                    Contact tmp = _contacts[i1];
                    _contacts[i1] = _contacts[i2];
                    _contacts[i2] = tmp;
                }
            }

            // Initialize velocity constraints.
            _contactSolver.Reset(_contacts, ContactCount, step.dtRatio, Settings.EnableWarmstarting);
            _contactSolver.InitializeVelocityConstraints();

            if (Settings.EnableWarmstarting)
            {
                _contactSolver.WarmStart();
            }


            if (Settings.EnableDiagnostics)
            {
                _watch.Start();
                _tmpTime = 0;
            }


            for (int i = 0; i < JointCount; ++i)
            {
                if (_joints[i].Enabled)
                    _joints[i].InitVelocityConstraints(ref step);

                #region ShadowPlay Mods
                if (_joints[i] is PoweredJoint)
                {
                    (_joints[i] as PoweredJoint).Update(ref step);
                }
                #endregion

            }

            if (Settings.EnableDiagnostics)
            {
                _tmpTime += _watch.ElapsedTicks;
            }


            // Solve velocity constraints.
            for (int i = 0; i < Settings.VelocityIterations; ++i)
            {

                if (Settings.EnableDiagnostics)
                    _watch.Start();

                for (int j = 0; j < JointCount; ++j)
                {
                    Joint joint = _joints[j];

                    if (!joint.Enabled)
                        continue;

                       //this  allow us to relax  certain joint, cheaply becomes flexible ( not used tho)
                    #region ShadowPlay Mods
                 #if DEBUG
                        int maxiterations =  joint.MaxVelocityIterations;
                         if (maxiterations != 0 && i > maxiterations )
                            continue;
				 #endif
                    
                    #endregion
                     

                    joint.SolveVelocityConstraints(ref step);
                    joint.Validate(step.inv_dt);
                }

                if (Settings.EnableDiagnostics)
                {
                    _watch.Stop();
                    _tmpTime += _watch.ElapsedTicks;
                    _watch.Reset();
                }


                _contactSolver.SolveVelocityConstraints();  //shadowplay.. for velocity.. contacts are solved after all the joints, for position, before.. hmmm
            }


            //after the main loop processing constraints, do a another pass of just powered joints 
            //this extra pass , for joints in a system that have DoExtraVelocityIterations set to true with reduce the joint error without affecting much else
            // this is currently used during standing to fix the legs working apart, spreading out after a time
            #region ShadowPlay Mods  
            for (int i = 0; i <Settings.ExtraJointVelocityIterations ; ++i)
            {
                for (int j = 0; j < JointCount; ++j)
                {
                    Joint joint = _joints[j];

                    if (!joint.Enabled || !(joint is PoweredJoint))
                        continue;
                               
                    if (!(joint as PoweredJoint).DoExtraVelocityIterations)
                            continue;

                    joint.SolveVelocityConstraints(ref step);
                       // joint.Validate(step.inv_dt);
                }
            }
            #endregion


            for (int i = 0; i < Settings.ExtraContactVelocityIterations; ++i)
            {
                _contactSolver.SolveVelocityConstraints();
            }


            // Post-solve (store impulses for warm starting).
            _contactSolver.StoreImpulses();

            // Integrate positions.
            for (int i = 0; i < BodyCount; ++i)
            {
                Body b = Bodies[i];

                if (b.BodyType == BodyType.Static)
                {
                    continue;
                }

                // Check for large velocities.
                float translationX = step.dt*b.LinearVelocityInternal.X;
                float translationY = step.dt*b.LinearVelocityInternal.Y;
                float result = translationX*translationX + translationY*translationY;

                if (result > Settings.MaxTranslationSquared)
                {
                    float sq = (float) Math.Sqrt(result);

                    float ratio = Settings.MaxTranslation/sq;
                    b.LinearVelocityInternal.X *= ratio;
                    b.LinearVelocityInternal.Y *= ratio;
                }

                float rotation = step.dt*b.AngularVelocityInternal;
                if (rotation*rotation > Settings.MaxRotationSquared)
                {
                    float ratio = Settings.MaxRotation/Math.Abs(rotation);
                    b.AngularVelocityInternal *= ratio;
                }

                // Store positions for continuous collision.
                b.Sweep.C0.X = b.Sweep.C.X;
                b.Sweep.C0.Y = b.Sweep.C.Y;
                b.Sweep.A0 = b.Sweep.A;

                // Integrate
                b.Sweep.C.X += step.dt*b.LinearVelocityInternal.X;
                b.Sweep.C.Y += step.dt*b.LinearVelocityInternal.Y;
                b.Sweep.A += step.dt*b.AngularVelocityInternal;

                // Compute new transform
                b.SynchronizeTransform();

                // Note: shapes are synchronized later.

   
            }

            // Iterate over constraints.
            for (int i = 0; i < Settings.PositionIterations; ++i)
            {


                bool contactsOkay = false;
            if (Settings.IsContactPosSolveAfter== false)  //Shadowplay mod.. TODO self-collide attempt, see if ragdoll gets still stuck to itself on dropped.  joints
             {
                 contactsOkay = _contactSolver.SolvePositionConstraints(Settings.ContactBaumgarte);
             }    //the idea is , if contacts have'nt come in, joints wont fight them

bool jointsOkay = true;

#if !(SILVERLIGHT || UNIVERSAL)
                if (Settings.EnableDiagnostics)
                    _watch.Start();
#endif
                for (int j = 0; j < JointCount; ++j)
                {
                    Joint joint = _joints[j];
                    if (!joint.Enabled)
                        continue;

                    #region ShadowPlay Mods
#if DEBUG
                    int maxiterations = joint.MaxPositionIterations;
                    if (maxiterations != 0 && i > maxiterations)
                        continue;
#endif

                    #endregion

                    bool jointOkay = joint.SolvePositionConstraints();
                    jointsOkay = jointsOkay && jointOkay;
                }

                if (Settings.IsContactPosSolveAfter)  //Shadowplay mod.. TODO self-collide attempt, see if ragdoll gets still stuck to itself on dropped.  joints
                    //push against the contacts,  they fight  .. then collision can keep them stuck..  very painful and many hack incomplete fixes..( next try TOI) , also , try don't do it at top
                { 
                   contactsOkay = _contactSolver.SolvePositionConstraints(Settings.ContactBaumgarte);
                }


#if !(SILVERLIGHT || UNIVERSAL)
                if (Settings.EnableDiagnostics)
                {
                    _watch.Stop();
                    _tmpTime += _watch.ElapsedTicks;
                    _watch.Reset();
                }
#endif

                if (Settings.ExtraContactPositionIterations > 0)
                {
                    for (int j = 0; j < Settings.ExtraContactPositionIterations; ++j)
                    {
                        _contactSolver.SolvePositionConstraints(Settings.ContactBaumgarte);
                    }
                }


                if (contactsOkay && jointsOkay)
                {
                    // Exit early if the position errors are small.
                    break;
                }

  

            }


            for (int i = 0; i < Settings.ExtraContactPositionIterations; ++i)
            {
                _contactSolver.SolvePositionConstraints(Settings.ContactBaumgarte);
            }
  
#if (!SILVERLIGHT)
            if (Settings.EnableDiagnostics)
            {
                JointUpdateTime = _tmpTime;
            }
#endif


            //NOTE not sure if needed.. Shadowplay Mod
            for (int i = 0; i < BodyCount; i++)  //shadowplay mod... doing this island by island.. clueless if its ok
            {
                Body body = Bodies[i];
                //TotalContactForce is just used to determine Gain for solving joint using more even mass ratios .. could just use the error..

             //   if (body.TotalContactForce > 0) { }
          //      Debug.WriteLine("bodyforce" + body.TotalContactForce);
         //   }

                body.TotalContactForce = 0;  //TotalContactForce is just used to determine Gain for solving joint using more even mass ratios .. could just use the error..


            }//End ShadowPlayMod 
        

             Report(_contactSolver.Constraints);  //shadowplay Mod NOTE there are two post solve Reports, here and on TOI ReportCurrentContactSolver

            if (Settings.AllowSleep)
            {
                float minSleepTime = Settings.MaxFloat;

                for (int i = 0; i < BodyCount; ++i)
                {
                    Body b = Bodies[i];
                    if (b.BodyType == BodyType.Static)
                    {
                        continue;
                    }

                    if ((b.Flags & BodyFlags.AutoSleep) == 0)
                    {
                        b.SleepTime = 0.0f;
                        minSleepTime = 0.0f;
                    }

                    if ((b.Flags & BodyFlags.AutoSleep) == 0 ||

#region ShadowPlay Mods
                        //workaround.. on  a joint graph with 2 or more joints in gravity,  usually nothing sleeps.
                        //appears angular velocity is high though is not rotating, probably due to torque on system by gravity pull on body..
                        //my workaround is to ignore this check if body is joined.
                        //usually objects rotating will also me moving .. shouldn't cause a problem with stuff falling asleep while rotating 
                        (b.JointList == null &&
#endregion
                        b.AngularVelocityInternal*b.AngularVelocityInternal > AngTolSqr )
                        ||
                        Vector2.Dot(b.LinearVelocityInternal, b.LinearVelocityInternal) > LinTolSqr)
                    {
                        b.SleepTime = 0.0f;
                        minSleepTime = 0.0f;
                    }
                    else
                    {
                        b.SleepTime += step.dt;
                        minSleepTime = Math.Min(minSleepTime, b.SleepTime);
                    }
                }

                if (minSleepTime >= Settings.TimeToSleep)
                {
                    for (int i = 0; i < BodyCount; ++i)
                    {
                        Body b = Bodies[i];
                        b.Awake = false;
                    }
                }
            }
        }

        internal void SolveTOI(ref TimeStep subStep)
        {
            _contactSolver.Reset(_contacts, ContactCount, subStep.dtRatio, false);

            // Solve position constraints.
            const float kTOIBaumgarte = 0.75f;
            for (int i = 0; i < Settings.TOIPositionIterations; ++i)
            {
                bool contactsOkay = _contactSolver.SolvePositionConstraintsTOI(kTOIBaumgarte);
                if (contactsOkay)
                {
                    break;
                }

                if (i == Settings.TOIPositionIterations - 1)
                {
                    i += 0;
                }
            }

            // Leap of faith to new safe state.
            for (int i = 0; i < BodyCount; ++i)
            {
                Body body = Bodies[i];
                body.Sweep.A0 = body.Sweep.A;
                body.Sweep.C0 = body.Sweep.C;
            }

            // No warm starting is needed for TOI events because warm
            // starting impulses were applied in the discrete solver.
            _contactSolver.InitializeVelocityConstraints();

            // Solve velocity constraints.
            for (int i = 0; i < Settings.TOIVelocityIterations; ++i)
            {
                _contactSolver.SolveVelocityConstraints();
            }

            // Don't store the TOI contact forces for warm starting
            // because they can be quite large. 
            //TODO dh..  until confirmed by Ian Quist or 3.3  is there a consequence to this?..  Warm starting carries information from last frame over in box2d in warmstarting it uses VelocityInternal.. i don't think its stored in the solver itself.  I can see that the state is Reset in the solver
			//before the normal solving that uses warmstarting.. on TOI it does not use it either.
            //HANDLEBULLETCOLLIDE
         //   _contactSolver.StoreImpulses();   ///Shadow play mod.. this might give us the correct impulse on the collision event.  Comes from user complain on the forum
            //and Ian Quist's advice.     //however.. don't know if side effects.  also  don't know if the contact position is even correct.
            //TESTED 5/11/2013.. commenting this line StoreImpulses does seem to work to provide the normal at collision .. don't see any side effect.. however since bullet is more than one meter off  
            //collide,  the manifold points are not the projected CCD collision point .  off by a frame or two.    need to use a ray to get the collide point ( TODO OPTIMIZATION.. not sure if still true that ray is needed..TODO I think it is not and can clean rays out from bullet wound and bruise code)
            // Integrate positions.
            for (int i = 0; i < BodyCount; ++i)
            {
                Body b = Bodies[i];

                if (b.BodyType == BodyType.Static)
                {
                    continue;
                }

                // Check for large velocities.
                float translationx = subStep.dt*b.LinearVelocityInternal.X;
                float translationy = subStep.dt*b.LinearVelocityInternal.Y;
                float dot = translationx*translationx + translationy*translationy;
                if (dot > Settings.MaxTranslationSquared)
                {
                    float norm = 1f/(float) Math.Sqrt(dot);
                    float value = Settings.MaxTranslation*subStep.inv_dt;
                    b.LinearVelocityInternal.X = value*(translationx*norm);
                    b.LinearVelocityInternal.Y = value*(translationy*norm);
                }

                float rotation = subStep.dt*b.AngularVelocity;
                if (rotation*rotation > Settings.MaxRotationSquared)
                {
                    if (rotation < 0.0)
                    {
                        b.AngularVelocityInternal = -subStep.inv_dt*Settings.MaxRotation;
                    }
                    else
                    {
                        b.AngularVelocityInternal = subStep.inv_dt*Settings.MaxRotation;
                    }
                }

                // Integrate
                b.Sweep.C.X += subStep.dt*b.LinearVelocityInternal.X;
                b.Sweep.C.Y += subStep.dt*b.LinearVelocityInternal.Y;
                b.Sweep.A += subStep.dt*b.AngularVelocityInternal;


                //todo parallel here..


                // Compute new transform
                b.SynchronizeTransform();

                //HANDLEBULLETCOLLIDE
                // Note: shapes are synchronized later.  // shadowplay mod dh.. note on Note:  <-- This causes a  problem on Collide with any velocity, its has shapes a frame off..   There is no apparent reason to report before syncing fixtures, so moving the reporting  to after they are synced
            }

          //  Report(_contactSolver.Constraints);//  Shadowplay Mod  moved this out .. see call to ReportCurrentContactSolver, 
        }

#region ShadowPlay Mod
        public void ReportCurrentContactSolver()
        {
              Report(_contactSolver.Constraints);  //was needed for correct TOI positions, suggested by Ian Quist 
        }
#endregion 

        public void Add(Body body)
        {
            Debug.Assert(BodyCount < Bodies.Length); ;
            Bodies[BodyCount++] = body;
        }

        public void Add(Contact contact)
        {

            #region ShadowPlay Mods 
        //    Debug.Assert(ContactCount < _contactCapacity);
            #endregion
            _contacts[ContactCount++] = contact;
          
        }

        public void Add(Joint joint)
        {
            Debug.Assert(JointCount < _jointCapacity);
            _joints[JointCount++] = joint;
        }

        private void Report(ContactConstraint[] constraints)
        {
            if (_contactManager == null)
                return;

            for (int i = 0; i < ContactCount; ++i)
            {
                Contact c = _contacts[i];

                if (c.FixtureA == null || c.FixtureB == null)//shadow play mod should not be needed but  break, cut respawn issue.. low impact 
                    continue;

                if (c.FixtureA.AfterCollision != null)              //ShadowPlay Mods  TODO is it reported once per pair?  or are we counting twice below
                    c.FixtureA.AfterCollision(c.FixtureA, c.FixtureB, c);

                if (c.FixtureB.AfterCollision != null)
                    c.FixtureB.AfterCollision(c.FixtureB, c.FixtureA, c);

                if (_contactManager.PostSolve != null)
                {
                    ContactConstraint cc = constraints[i];

                    _contactManager.PostSolve(c, cc);
                }

            }
        }
    }
}