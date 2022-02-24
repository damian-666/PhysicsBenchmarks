using Core.Data.Interfaces;
using FarseerPhysics.Common;
using FarseerPhysics.Dynamics;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace FarseerPhysics.Common
{

    /// <summary>
    /// Additional information about how  to draw a body .  GeneralVertices can be copies from original by tool and modified or remand a ref to parent
    /// </summary>
    [DataContract(Name = "ViewModel", Namespace = "http://ShadowPlay", IsReference = true)]
    public class ViewModel : NotifyPropertyBase
    {
        [DataMember]
        Body parent;

        

        /// <summary>
        /// This are initialized when a ViewModel is made for a body, can be edited in tool later
        /// </summary>
        [DataMember]
        public Vertices GeneralVertices { get; set; } 

        protected bool isSplineFit = false;

        public ViewModel(Body parent)
        {
            this.parent = parent;
            GeneralVertices = parent.GeneralVertices;

        }


        [DataMember]
        int DrawOrder { get; set; }

        [DataMember]
        public bool IsSplineFit
        {
            //TODO check nez, myra velcro,  for examples v
            get => isSplineFit; 
            set { isSplineFit = value; FirePropertyChanged(); }
        }

        //TODO add color info, stroke with, etc

        public int ZOrder { set { parent.ZIndex = value; FirePropertyChanged(); } get => parent.ZIndex; }

        public Body Parent {get => parent; }

        //TODO color and stroke info


    }
}
