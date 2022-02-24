using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Data.Entity;
using System.Diagnostics;

namespace Core.Data.Animations
{


    //TODO maybe add a Detach Listeners to force all derived classes and their descendant to implement releaselisteners like on end effect ( lifecycle management.. leaks) .. 
    //effects can pile up over the life on an object on a long level they cant be GC.. till unlisten.



    // public abstract class Effect<T>: Effect
    // {
    //     public T UserData;
    //  }


    // interface IEffect<T>
    ////{
    //    void OnUpdate( in< T> param);  // TODO FUTURE need to brush up on my C# must be a way to automatically pass the type in these OnUpdate 
    // Dont want to get too deep into c# due to possible ortability and the compiler / parser in the tool is not just the same
    //}

    //or a generic Effect on T type... as in effect on body..   effect on plugin..

    /// <summary>
    /// Base class for effect .   These classes contant no  persistent model data  but are tightly coupled with spirit
    /// Used by plugins and spirit for transient or repeated, or cyclical effects
    /// </summary>
    public abstract class Effect
    {
        protected double _elapsedTime = 0;
        protected double _duration = 0;

        public Spirit Parent;  //todo.. make  interface IEffectsOwner


        public Action<Effect> OnEndEffect;
        public Action<Effect> OnUpdateEffect;  //TODO make a templated verison of effect.. like IUpdate<T>     Icollection<T>  
        public Action OnUpdate;

        public Func<bool> OnRequestCancel;  //client can return true if effect should remove itself

        public Func<Effect, bool> CanEndEffect;// if caller returns false, Delay will keep polling until its true.

        public string Name;  //this is the key of this collection, each must be named to avoid duplicates

        public bool Left;  // does this go with left or right side

        public object UserData;   //TODO templated version?

        /// <summary>
        ///ElapsedTime in seconds
        /// </summary>
        public double ElapsedTime
        {
            get
            {
                return _elapsedTime;
            }
        }


        /// <summary>
        /// Effect with a unique name, continues until
        /// </summary>
        /// <param name="sp"></param>
        /// <param name="name"></param>
        public Effect(Spirit sp, string name)
        {
            Parent = sp;
            Name = name;

#if DEBUG
            if (!sp.Effects.CheckAdd(this))
            {
                WarnAboutConstructorsLeaking(name);
            }
#else
            sp.Effects.Add(this);
#endif

        }

        //this can be bad.. construct  effects that dont get updated since orphaned..  example would be a windField, the wind field will linger.
        //TODO remove, I think circular refs are taken care of, when a new level is loaded , no refs to teh spirits and the effects so should not leak
        private void WarnAboutConstructorsLeaking(string name)
        {
#if DEBUG
            if (GetType() != typeof(Effect) && GetType() != typeof(Delay) && GetType() != typeof(SetBias)&&  GetType() != typeof(SelfCollide)&&  GetType() != typeof(OrgansAction)) //putting safe ones here
            {
                Debug.WriteLine("CHEK IF effect exists outside , outside of contructor, if constructor does something that must be undone " + this.GetType().ToString() + " " + name);
            }
#endif
        }



        /// <summary>
        /// Effect with a unique name
        /// </summary>
        /// <param name="sp"></param>
        /// <param name="name"></param> 
        /// <param name="replaceExisitng"></param>
        public Effect(Spirit sp, string name, double duration, bool replaceExisting) : this(sp, name, duration)
        {
            if (replaceExisting)
            {

#if DEBUG
                WarnAboutConstructorsLeaking(name);
#endif

                sp.Effects.AddOrReplace(this);
            }
            else
            {

                if (this.GetType() != typeof(Effect))
                {
                    Debug.WriteLine("CHEK IF EXISTS IF outside of contructor. ");
                }

                sp.Effects.Add(this);
            }
        }



        /// <summary>
        /// Effect of finite duration with a unique name
        /// </summary>
        /// <param name="sp"></param>
        /// <param name="name"></param>
        /// <param name="duration">duration of effect in seconds</param>
        public Effect(Spirit sp, string name, double duration) :
            this(sp, name)
        {
            _duration = duration;
        }


        /// <summary>
        /// Update, Every time slice of virtual tiem
        /// </summary>
        /// <param name="dt"> Time step in sec ( time step of the physics engine) virtual time, can vary depending on loading</param>
        virtual public void Update(double dt)
        {
            _elapsedTime += dt;

            if ((ElapsedTime > Duration && Duration != 0)
                && (CanEndEffect == null || CanEndEffect(this)))
            {
                Finish();
                return;
            }

            if (OnUpdate != null)
                OnUpdate();

            if (OnUpdateEffect != null)
                OnUpdateEffect(this);
        }

        /// <summary>
        /// Finish effect immediately.  Otherwise on duration is done it removes itself.
        /// </summary>
        virtual public void Finish()
        {
            if (OnEndEffect != null)
                OnEndEffect(this);

            OnUpdate = null;  //not sure if these are needed.. might prevent leaks due to object lifetime
            OnEndEffect = null;
            OnUpdateEffect = null;

            Parent.Effects.Remove(this);  
        }

        /// <summary>
        /// Reset duration, start over the timer
        /// </summary>
        virtual public void Reset()
        {
            _elapsedTime = 0;
        }

        /// <summary>
        /// Duration in sec, after this effect will remove itself from collection
        /// </summary>
        public double Duration
        {
            get
            {
                return _duration;
            }

            set  //allow to extend.
            {
                 _duration = value;          
            }

        }

    }

}
