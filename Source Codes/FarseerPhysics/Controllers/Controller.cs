﻿using System.Runtime.Serialization;
using FarseerPhysics.Dynamics;
using System;

namespace FarseerPhysics.Controllers
{
    [Flags]
    public enum ControllerType
    {
        GravityController = (1 << 0),
        VelocityLimitController = (1 << 1),
        AbstractForceController = (1 << 2),

        BuoyancyController =(1 << 3)
        #region ShadowPlayMod
        , PointGravityController = (1 << 3),
        PhysicsSoundsController = (1 << 4),
        WindDrag= ( 1 <<5),
        #endregion


    }

    public class FilterControllerData : FilterData
    {
        private ControllerType _type;

        public FilterControllerData(ControllerType type)
        {
            _type = type;
        }

        public override bool IsActiveOn(Body body)
        {
            if (body.ControllerFilter.IsControllerIgnored(_type))
                return false;

            return base.IsActiveOn(body);
        }
    }

    public class ControllerFilter
    {
        public ControllerType ControllerFlags;

        /// <summary>
        /// Ignores the controller. The controller has no effect on this body.
        /// </summary>
        /// <param name="controller">The controller type.</param>
        public void IgnoreController(ControllerType controller)
        {
            ControllerFlags |= controller;
        }

        /// <summary>
        /// Restore the controller. The controller affects this body.
        /// </summary>
        /// <param name="controller">The controller type.</param>
        public void RestoreController(ControllerType controller)
        {
            ControllerFlags &= ~controller;
        }

        /// <summary>
        /// Determines whether this body ignores the the specified controller.
        /// </summary>
        /// <param name="controller">The controller type.</param>
        /// <returns>
        /// 	<c>true</c> if the body has the specified flag; otherwise, <c>false</c>.
        /// </returns>
        public bool IsControllerIgnored(ControllerType controller)
        {
            return (ControllerFlags & controller) == controller;
        }
    }

    [DataContract(Name = "Controller", Namespace = "http://ShadowPlay")]
    public abstract class Controller
    {
        [DataMember]
        public bool Enabled;
        public FilterControllerData FilterData;

        public World World;

        public Controller(ControllerType controllerType)
        {
            FilterData = new FilterControllerData(controllerType);
        }

        public abstract void Update(float dt);
    }
}