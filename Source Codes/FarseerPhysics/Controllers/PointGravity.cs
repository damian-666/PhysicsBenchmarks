/*
 * Simple gravity controller on a point.
 * Built primarily for Planet entity. 
 * 
 * Gravity point can be attached to a body. 
 * Attached body in here should not affect gravity force. 
 * Normally body with greater mass will give more gravity force, but not for this case.
 * If not attached to a body, gravity source position must be updated manually.
 * 
 * Copyright Shadowplay Studios, 2010.
 */

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Farseer.Xna.Framework;
using FarseerPhysics.Common;
using FarseerPhysics.Dynamics;


namespace FarseerPhysics.Controllers
{
    [DataContract(Name = "PointGravity", Namespace = "http://ShadowPlay")]
    public class PointGravity : Controller
    {
        private Vector2 _position;

        const float GravityStrength = 10000f;



        public PointGravity(Body body = null, float strength = GravityStrength, float maxRadius = float.MaxValue, float minRadius = float.MinValue) :
            base(ControllerType.PointGravityController)
        {
            MinRadius = minRadius;
            MaxRadius = maxRadius;
            Strength = strength;
            AttachedBody = body;
        }

        /// <summary>
        /// [Optional] Attached body. Can be used to move this point gravity 
        /// position according to attached body.
        /// </summary>
        [DataMember]
        public Body AttachedBody { get; set; }

        /// <summary>
        /// Default is 0.01. Distance below MinRadius will get the same gravity as MinRadius.
        /// </summary>
        [DataMember]
        public float MinRadius { get; set; }

        /// <summary>
        /// Distance above MaxRadius will get no gravity force.
        /// </summary>
        [DataMember]
        public float MaxRadius { get; set; }

        [DataMember]
        public float Strength { get; set; }

        [DataMember]
        public GravityType GravityType { get; set; }

        [DataMember]
        public Vector2 Position
        {
            get { return _position; }
            set
            {
                // setting position only valid if no body attached
                if (AttachedBody == null) _position = value;

                // when serialized
                else _position = AttachedBody.Position;
            }
        }


        public override void Update(float dt)
        {
            Vector2 f = Vector2.Zero;

            // if attached to a body
            if (AttachedBody != null)
            {
                // gravity position must follow body position
                _position = AttachedBody.WorldCenter;

                // if attached body is inactive, this gravity controller should 
                // not generate any gravity.
                if (!AttachedBody.Enabled) return;
            }




            //TDOO best to add up all the gravity controllers... pass all bodies  once and take the sum of all
            //so we can cal a total gravity field at a world point.... like windforce thing..

           


            //TODO xref all bodies over a certain size for microgravity

            Parallel.ForEach(
                World.BodyList, body1 =>
                    {
                      if (!body1.Enabled || body1.IgnoreGravity) return;
                      if (AttachedBody != null)
                      {
                          if (body1 == AttachedBody || (body1.IsStatic && AttachedBody.IsStatic))
                              return;
                      }

                      Vector2 d = _position - body1.WorldCenter;
                      float r2 = d.LengthSquared();

                      if (r2 < Settings.Epsilon)
                          return;

                      float r = d.Length();

                      if (r > MaxRadius) return;
                      if (r < MinRadius) r = MinRadius;

                      switch (GravityType)
                      {
                          case GravityType.DistanceSquared:
                              f = Strength / r2 / (float)Math.Sqrt(r2) * body1.Mass * d;
                              break;
                          case GravityType.Linear:
                              f = Strength / r2 * body1.Mass * d;
                              break;
                      }

                      body1.ApplyForce(ref f);
                  }
            );

        }

    }
}